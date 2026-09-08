using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DexManager.Models;
using DexManager.Utils;

namespace DexManager.Services
{
    public sealed class DexOrchestrator
    {
        // WakeScreen 직후 DismissKeyguard 호출까지 두는 짧은 대기. 실기에서
        // 화면이 깨어나는 데 시간이 걸리는 것이 확인됐지만 정확한 소요
        // 시간은 측정하지 못했으므로, 이 저장소의 다른 UI 안정화 대기와
        // 같은 자릿수(150~250ms, VirtualDisplayService.cs)로 보수적이되
        // 짧게 잡는다.
        //
        // 폴로 바꾸지 않는다 - DismissKeyguardLockProbeBudgetMs와 달리,
        // 이 대기 다음에는 "너무 이르면 오판하는" 게이트가 없다. wake가
        // 부족하면 DismissKeyguard 자체가 무효(no-op)가 될 뿐이고, 그건
        // 상태를 다시 확인해도 되돌릴 수 없다(같은 dismiss를 다시 보내지
        // 않는 한, 폴은 그러지 않는다) - 그러니 폴로 바꿔도 정확성이
        // 좋아지지 않는다. interactiveState는 관측 가능하지만(dumpsys
        // window), 그걸 새로 파싱하는 비용을 들일 만한 실측 실패 사례가
        // 없다 - 이 값 자체가 이미 작고 네트워크에 의존하지 않는 로컬
        // 대기라, 있는 그대로 둔다.
        private const int WakeSettleDelayMs = 300;

        // DismissKeyguard 직후 잠금 프로브 예산과 재확인 간격. 이전에는
        // 고정 대기(150ms) 뒤 딱 한 번만 확인해서, dismiss 반영이 그보다
        // 느리면 프로브가 "아직 해제 중"인 기기를 Locked로 읽어 시작을
        // 막았다(fail-*closed* - 이 기능에서 유일하게 fail-open 규율을
        // 어기는 지점이었다). LockStatePoll로 바꿔 Locked가 관측되는
        // 동안만 재확인하고, Unlocked나 Unknown이 나오면(둘 다 이미
        // 판단이 끝난 상태다) 즉시 멈춘다.
        //
        // 예산 2000ms: 실기에서 해제가 1초 안쪽으로 끝나는 것이
        // 확인됐으므로(.omc/research/2026-09-08-realdevice-lock-findings.md
        // 9절), 그 두 배 이상의 여유를 준다 - 일시적으로 느린 기기도
        // 통과시키되, 시작 버튼을 누른 뒤 체감될 만큼 무한정 기다리지는
        // 않는다.
        // 간격 150ms: VirtualDisplayService.cs의 UI 안정화 대기와 같은
        // 자릿수. 매 확인이 adb 왕복(dumpsys trust, 필요하면 dumpsys
        // window)이므로 이보다 더 촘촘히 돌면 왕복 비용만 늘고 얻는 게
        // 없다.
        //
        // 공통 경로(대부분의 실기)에서는 오히려 더 빠르다 - 첫 확인이
        // 즉시 나가므로, dismiss가 이미 반영돼 있으면 예전처럼 150ms를
        // 무조건 태우지 않고 바로 다음 단계로 넘어간다.
        private const int DismissKeyguardLockProbeBudgetMs = 2000;
        private const int DismissKeyguardLockProbeIntervalMs = 150;

        private readonly AdbService _adbService;
        private readonly VirtualDisplayService _virtualDisplayService;
        private readonly ScrcpyService _scrcpyService;
        private readonly ScrcpyLaunchCoordinator _launchCoordinator;
        private readonly SettingsService _settingsService;
        private readonly LogService _logService;
        private readonly AppSettings _settings;
        private readonly DeviceRuntimeSessionRegistry _runtimeSessions;
        private readonly object _operationGate = new object();
        private readonly object _shutdownTaskLock = new object();
        private readonly ManualResetEvent _shutdownSignal =
            new ManualResetEvent(false);
        private int _shutdownRequested;
        private Task _shutdownTask;
        private ManagedDisplaySession _currentSession;
        private readonly List<DeferredDisplayCleanup>
            _pendingDisplayCleanup =
                new List<DeferredDisplayCleanup>();

        public DexOrchestrator(
            AdbService adbService,
            VirtualDisplayService virtualDisplayService,
            ScrcpyService scrcpyService,
            ScrcpyLaunchCoordinator launchCoordinator,
            SettingsService settingsService,
            LogService logService,
            AppSettings settings,
            DeviceRuntimeSessionRegistry runtimeSessions)
        {
            _adbService = adbService;
            _virtualDisplayService = virtualDisplayService;
            _scrcpyService = scrcpyService;
            _launchCoordinator = launchCoordinator;
            _settingsService = settingsService;
            _logService = logService;
            _settings = settings;
            _runtimeSessions = runtimeSessions ??
                throw new ArgumentNullException("runtimeSessions");
            _scrcpyService.RunningChanged +=
                ScrcpyService_RunningChanged;
        }

        public bool IsRunning
        {
            get { return _scrcpyService.IsRunning; }
        }

        public ManagedDisplaySession CurrentSession
        {
            get { return _currentSession; }
        }

        public bool HasDeferredDisplayCleanup
        {
            get
            {
                lock (_operationGate)
                {
                    return _pendingDisplayCleanup.Count > 0;
                }
            }
        }

        public bool IsCleanupComplete
        {
            get
            {
                lock (_operationGate)
                {
                    return !_scrcpyService.IsRunning &&
                        _currentSession == null &&
                        _pendingDisplayCleanup.Count == 0;
                }
            }
        }

        public bool IsShutdownRequested
        {
            get
            {
                return Interlocked.CompareExchange(
                    ref _shutdownRequested,
                    0,
                    0) != 0;
            }
        }

        public async Task<bool> StartAsync(
            string serial,
            string deviceIdentity,
            CancellationToken cancellationToken)
        {
            using (cancellationToken.Register(RequestShutdown))
            {
                var started = await Task.Run(delegate
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    lock (_operationGate)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return StartCore(serial, deviceIdentity);
                    }
                }, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return started;
            }
        }

        public Task StopAsync()
        {
            return Task.Run(delegate
            {
                lock (_operationGate) StopCore();
            });
        }

        public Task<bool> StopOrConfirmCleanupAsync()
        {
            return Task.Run(delegate
            {
                lock (_operationGate)
                {
                    if (!_scrcpyService.IsRunning &&
                        _currentSession == null)
                    {
                        return _pendingDisplayCleanup.Count == 0;
                    }
                    StopCore();
                    return _pendingDisplayCleanup.Count == 0;
                }
            });
        }

        public Task<bool> RetryDeferredCleanupAsync(
            string serial,
            string deviceIdentity)
        {
            return Task.Run(delegate
            {
                lock (_operationGate)
                    return RetryDeferredCleanupCore(
                        serial,
                        deviceIdentity);
            });
        }

        public Task<bool> CleanupConnectedOverlayAsync(
            string serial,
            string deviceIdentity)
        {
            return Task.Run(delegate
            {
                lock (_operationGate)
                {
                    string verifiedIdentity;
                    if (!CleanupConnectedTargetOverlay(
                        serial,
                        deviceIdentity,
                        out verifiedIdentity))
                        return false;
                    CompleteDeferredCleanupCore(
                        serial,
                        verifiedIdentity);
                    return true;
                }
            });
        }

        public void RequestShutdown()
        {
            _scrcpyService.RequestShutdown();
            if (Interlocked.Exchange(ref _shutdownRequested, 1) == 0)
                _shutdownSignal.Set();
        }

        public Task ShutdownAsync(
            string fallbackSerial,
            string fallbackIdentity)
        {
            RequestShutdown();
            lock (_shutdownTaskLock)
            {
                if (_shutdownTask == null ||
                    _shutdownTask.IsFaulted ||
                    _shutdownTask.IsCanceled)
                {
                    _shutdownTask = Task.Run(delegate
                    {
                        lock (_operationGate)
                            ShutdownCore(
                                fallbackSerial,
                                fallbackIdentity);
                    });
                }
                return _shutdownTask;
            }
        }

        public Task<bool> ApplyRuntimeSettingsAsync()
        {
            return Task.Run(delegate
            {
                lock (_operationGate)
                    return ApplyRuntimeSettingsCore();
            });
        }

        private bool StartCore(
            string requestedSerial,
            string deviceIdentity)
        {
            if (IsShutdownRequested) return false;
            if (_scrcpyService.IsRunning)
            {
                _logService.Warning(LocalizationService.Get(
                    "Log.Dex.AlreadyRunning"));
                return false;
            }

            var serial = requestedSerial;
            if (string.IsNullOrWhiteSpace(serial) ||
                !_adbService.IsAuthorizedDeviceConnected(serial))
            {
                throw new InvalidOperationException(
                    LocalizationService.Get(
                        "Error.Dex.NoAuthorizedDevice"));
            }
            // 실기(SM-F971N, One UI) 확인 - fix round 1: 깨우기(KEYCODE_
            // WAKEUP)와 키가드 해제(wm dismiss-keyguard)는 별개의 두
            // 단계이며 반드시 이 순서로 실행해야 한다. "wm dismiss-
            // keyguard"만으로는 기기가 잠들어 있는 동안 아무 효과가
            // 없었고, 깨우기만으로는 키가드가 풀리지 않았다(첫 실기
            // 관찰에서 풀린 것처럼 보였던 건 마침 그 시점에 Smart Lock
            // 신뢰가 막 재승인된 우연이었다 - 재현 시 키가드가 계속
            // 떠 있었다). 아래 잠금 게이트가 "깨우고 해제까지 시도한
            // 뒤"의 상태를 판단하도록 반드시 그 앞에 둔다. 두 메서드
            // 모두 내부에서 예외를 삼키고 false를 돌려주므로(IsDeviceLocked
            // 와 같은 fail-open 규율) 여기서 결과를 따로 검사할 필요가
            // 없다 - 어느 쪽이 실패해도 시작은 계속돼야 한다. 자격증명이
            // 진짜 필요한 기기는 dismiss가 프롬프트만 띄우고 실제로는
            // 풀리지 않는다 - 그건 아래 게이트가 dumpsys trust의
            // deviceLocked=1로 정확히 잡아야 할 정상 케이스다.
            _adbService.WakeScreen(serial);
            // 깨어나는 데 걸리는 시간(실기에서 관찰됨) - dismiss가 "아직
            // 잠든" 기기에 무효로 도착하지 않도록 대기한다. 값은 이
            // 저장소의 다른 UI 안정화 대기(150~250ms,
            // VirtualDisplayService.cs)와 같은 자릿수로 골랐다 - 사용자가
            // 체감할 정도로 시작을 늦추지 않으면서 One UI가 깨어날 여유를
            // 준다.
            Thread.Sleep(WakeSettleDelayMs);
            _adbService.DismissKeyguard(serial);
            // 키가드 해제는 즉시 반영되지 않을 수 있다. 고정 대기 뒤
            // 딱 한 번만 확인하면, 해제가 그 대기보다 느릴 때 "아직
            // 해제 중"인 기기를 Locked로 오판해 시작을 막는다(fail-
            // *closed* - 이미 풀리고 있는 폰에게 "먼저 잠금을 해제하라"고
            // 잘못 안내하는 것과 같다). LockStatePoll로 Locked가 관측되는
            // 동안만 짧게 재확인한다 - 예산과 간격의 근거는
            // DismissKeyguardLockProbeBudgetMs/IntervalMs 선언부 참고.
            var lockState = LockStatePoll.Until(
                delegate { return _adbService.IsDeviceLocked(serial); },
                TimeSpan.FromMilliseconds(DismissKeyguardLockProbeBudgetMs),
                TimeSpan.FromMilliseconds(DismissKeyguardLockProbeIntervalMs));
            // DeX는 새 가상 디스플레이를 만들어 그것만 미러링한다. 잠금
            // 화면은 항상 기본 디스플레이(0)에만 그려지므로 DeX 미러로는
            // 잠금을 풀 수 없다 - 그 우회는 불가능하다. 그래서 시작 전에
            // 잠겨 있음을 확신할 때만 막고, 판단이 애매하면(Unknown) 통과
            // 시킨다 - 파싱 공백이 정상적으로 될 시작을 막는 것이 더 나쁜
            // 실패 방향이기 때문이다(fail-open). LockStatePoll은 이
            // 판단을 언제 내릴지만 바꿀 뿐, Locked/Unlocked/Unknown 중
            // 어느 쪽으로 판단할지는 그대로 여기서 결정한다.
            //
            // 위치에 따른 부수 효과(의도적, 불변식 위반 아님): 이 게이트는
            // 아래의 CleanupStaleSession보다 먼저 중단하므로, 지연된
            // overlay 정리(CleanupNaturallyEndedSession이 해제에 실패해
            // DeferDisplayCleanup으로 미뤄둔 경우)를 이번 시작에 얹어
            // 처리하지 못한다. 그런 고아 overlay는 잠금 해제 후의 다음
            // 시작이나 앱 종료 시
            // (ShellViewModel.Dispose -> ApplicationHost.Dispose가
            // RuntimeFactory.CreatedInstances를 각자의 identity로 순회하며
            // 종료) 반드시 회수되므로 불변식 자체는 유지된다 - 다만
            // 고아가 남아 있는 시간 창은 이 게이트만큼 넓어졌다.
            if (lockState == LockState.Locked)
            {
                throw new InvalidOperationException(
                    LocalizationService.Get(
                        "Error.Dex.DeviceLocked"));
            }
            deviceIdentity = GetVerifiedDeviceIdentity(
                serial,
                deviceIdentity);
            if (string.IsNullOrWhiteSpace(deviceIdentity))
            {
                throw new InvalidOperationException(
                    "The physical-device identity could not be verified. " +
                    "Keep the phone connected and unlocked, then try again.");
            }
            CleanupStaleSession(serial, deviceIdentity);
            var runSettings = GetDeviceRunSettings(deviceIdentity);

            VirtualDisplayLease lease = null;
            var scrcpyStarted = false;
            try
            {
                _launchCoordinator.RunExclusive(delegate
                {
                    ThrowIfShutdownRequested();
                    lease = _virtualDisplayService.EnsureVirtualDisplay(
                        serial,
                        runSettings.VirtualDisplay,
                        _settings.Timing.VirtualDisplayDetectionTimeoutMs,
                        delegate { return IsShutdownRequested; });
                    CompleteDeferredCleanupCore(
                        serial,
                        deviceIdentity);
                    ThrowIfShutdownRequested();
                    _scrcpyService.Start(
                        runSettings.Scrcpy,
                        lease.DisplayId,
                        serial);
                    scrcpyStarted = true;
                });

                if (IsShutdownRequested)
                    throw new OperationCanceledException();

                if (!_scrcpyService.IsRunning)
                    throw new InvalidOperationException(
                        LocalizationService.Get(
                            "Error.Scrcpy.ExitedBeforeWindow"));

                TrackSession(
                    "DeX",
                    serial,
                    deviceIdentity,
                    lease);
                if (!_scrcpyService.IsRunning)
                    throw new InvalidOperationException(
                        LocalizationService.Get(
                            "Error.Scrcpy.ExitedBeforeWindow"));
                try
                {
                    SaveLastSuccess(
                        serial,
                        deviceIdentity,
                        lease.DisplayId);
                }
                catch (Exception saveException)
                {
                    _logService.Error(
                        LocalizationService.Get(
                            "Log.Dex.LastSuccessSaveFailed"),
                        saveException);
                }
                _logService.Info(LocalizationService.Get(
                    "Log.Dex.StartCompleted"));
                return true;
            }
            catch (OperationCanceledException ex)
            {
                lease = GetRetainedLease(ex, lease);
                CleanupFailedStart(
                    scrcpyStarted,
                    lease,
                    deviceIdentity);
                _logService.Info(LocalizationService.Get(
                    "Log.Dex.StartCancelled"));
                throw;
            }
            catch (Exception ex)
            {
                lease = GetRetainedLease(ex, lease);
                CleanupFailedStart(
                    scrcpyStarted,
                    lease,
                    deviceIdentity);
                _logService.Error(
                    LocalizationService.Get("Log.Dex.StartFailed"),
                    ex);
                throw;
            }
        }

        private void StopCore()
        {
            var session = _currentSession;
            Exception stopException = null;
            try
            {
                _scrcpyService.Stop();
            }
            catch (Exception ex)
            {
                stopException = ex;
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Dex.StopProcessFailed"),
                    ex);
            }

            if (_scrcpyService.IsRunning)
            {
                if (stopException != null) throw stopException;
                throw new InvalidOperationException(
                    LocalizationService.Get(
                        "Error.Scrcpy.StopTimeout"));
            }

            if (session != null)
            {
                if (!ReleaseDisplayLease(
                    session.DisplayLease,
                    session.DeviceIdentity))
                    DeferDisplayCleanup(session);
            }
            ClearSession(session);
            _logService.Info(LocalizationService.Get(
                "Log.Dex.StopCleanupCompleted"));
            if (stopException != null) throw stopException;
        }

        private bool ApplyRuntimeSettingsCore()
        {
            if (IsShutdownRequested) return false;
            try
            {
                var serial = _currentSession == null
                    ? string.Empty
                    : _currentSession.Serial;
                if (string.IsNullOrWhiteSpace(serial) ||
                    !_adbService.IsAuthorizedDeviceConnected(serial))
                {
                    _logService.Warning(LocalizationService.Get(
                        "Log.Dex.ApplyDeferredNoDevice"));
                    return false;
                }

                _logService.Info(LocalizationService.Get(
                    "Log.Dex.RemovingDisplayForApply"));
                var session = _currentSession;
                _scrcpyService.Stop();
                if (session != null &&
                    !ReleaseDisplayLease(
                        session.DisplayLease,
                        session.DeviceIdentity))
                {
                    DeferDisplayCleanup(session);
                    throw new InvalidOperationException(
                        LocalizationService.Get(
                            "Error.Dex.DisplayResetFailed"));
                }
                ClearSession(session);

                if (_shutdownSignal.WaitOne(1000)) return false;
                var deviceIdentity = session == null
                    ? string.Empty
                    : session.DeviceIdentity;
                if (!StartCore(serial, deviceIdentity)) return false;
                if (!_scrcpyService.IsRunning) return false;
                _logService.Info(LocalizationService.Get(
                    "Log.Dex.ApplyCompleted"));
                return true;
            }
            catch (Exception ex)
            {
                _logService.Error(
                    LocalizationService.Get("Log.Dex.ApplyFailed"),
                    ex);
                throw;
            }
        }

        private void ShutdownCore(
            string fallbackSerial,
            string fallbackIdentity)
        {
            var session = _currentSession;
            Exception stopException = null;
            try
            {
                _scrcpyService.Stop();
            }
            catch (Exception ex)
            {
                stopException = ex;
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Dex.StopProcessFailed"),
                    ex);
            }

            if (_scrcpyService.IsRunning)
            {
                if (stopException != null) throw stopException;
                throw new InvalidOperationException(
                    LocalizationService.Get(
                        "Error.Scrcpy.StopTimeout"));
            }

            if (session != null &&
                !ReleaseDisplayLease(
                    session.DisplayLease,
                    session.DeviceIdentity))
            {
                DeferDisplayCleanup(session);
            }
            else if (session == null &&
                     (!string.IsNullOrWhiteSpace(fallbackSerial) ||
                      HasStableIdentity(fallbackIdentity)))
            {
                // 세션도 없고 대상 serial·identity도 없으면 회수할 overlay도,
                // 명령을 보낼 기기도 없다. 이때의 예외는 실패가 아니라 대상
                // 부재이므로 시도하지 않는다. 대상이 하나라도 있으면 기존
                // 동작 그대로 시도하고, 실패하면 그대로 던진다.
                string verifiedIdentity;
                if (!CleanupConnectedTargetOverlay(
                    fallbackSerial,
                    fallbackIdentity,
                    out verifiedIdentity))
                {
                    throw new InvalidOperationException(
                        LocalizationService.Get(
                            "Error.Dex.DisplayResetFailed"));
                }
                CompleteDeferredCleanupCore(
                    fallbackSerial,
                    verifiedIdentity);
            }
            ClearSession(session);
            _logService.Info(LocalizationService.Get(
                "Log.Dex.ShutdownCleanupCompleted"));
            if (stopException != null) throw stopException;
        }

        private void ScrcpyService_RunningChanged(
            object sender,
            EventArgs e)
        {
            if (_scrcpyService.IsRunning) return;
            Task.Run(delegate
            {
                lock (_operationGate)
                {
                    if (!_scrcpyService.IsRunning)
                        CleanupNaturallyEndedSession();
                }
            });
        }

        private void CleanupNaturallyEndedSession()
        {
            var session = _currentSession;
            if (session == null) return;
            if (!ReleaseDisplayLease(
                session.DisplayLease,
                session.DeviceIdentity))
            {
                DeferDisplayCleanup(session);
                _logService.Warning(LocalizationService.Get(
                    "Log.Dex.NaturalExitCleanupDeferred"));
                return;
            }

            ClearSession(session);
            _logService.Info(LocalizationService.Get(
                "Log.Dex.NaturalExitCleanupCompleted"));
        }

        private void CleanupStaleSession(
            string nextSerial,
            string nextDeviceIdentity)
        {
            var stale = _currentSession;
            if (stale == null) return;
            if (string.Equals(
                stale.Serial,
                nextSerial,
                StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    stale.DeviceIdentity,
                    nextDeviceIdentity,
                    StringComparison.OrdinalIgnoreCase))
            {
                // A reconnect must be evaluated by EnsureVirtualDisplay.
                // Preserve the cleanup evidence until that comparison and
                // any required recreation have actually succeeded.
                DeferDisplayCleanup(stale);
                return;
            }
            if (ReleaseDisplayLease(
                stale.DisplayLease,
                stale.DeviceIdentity))
            {
                ClearSession(stale);
                return;
            }

            DeferDisplayCleanup(stale);
            if (string.Equals(
                stale.Serial,
                nextSerial,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    LocalizationService.Get(
                        "Error.Dex.DisplayResetFailed"));
            }
        }

        private void CleanupFailedStart(
            bool scrcpyStarted,
            VirtualDisplayLease lease,
            string deviceIdentity)
        {
            if (scrcpyStarted || _scrcpyService.IsRunning)
            {
                try
                {
                    _scrcpyService.Stop();
                }
                catch (Exception cleanupException)
                {
                    _logService.Error(
                        LocalizationService.Get(
                            "Log.Dex.StopProcessFailed"),
                        cleanupException);
                }
            }

            if (_scrcpyService.IsRunning)
            {
                if (_currentSession == null && lease != null)
                    TrackSession(
                        "DeX",
                        lease.Serial,
                        deviceIdentity,
                        lease);
                return;
            }

            try
            {
                if (lease != null &&
                    !ReleaseDisplayLease(
                        lease,
                        deviceIdentity))
                {
                    DeferDisplayCleanup(
                        lease,
                        deviceIdentity);
                    ClearSession(_currentSession);
                    return;
                }
                ClearSession(_currentSession);
            }
            catch (Exception cleanupException)
            {
                _logService.Error(
                    LocalizationService.Get(
                        "Log.Dex.ShutdownCleanupFailed"),
                    cleanupException);
            }
        }

        private bool RetryDeferredCleanupCore(
            string serial,
            string deviceIdentity)
        {
            if (string.IsNullOrWhiteSpace(serial)) return true;
            var verifiedIdentity = GetVerifiedDeviceIdentity(
                serial,
                deviceIdentity);
            // Keep the entry until EnsureVirtualDisplay or an explicit Reset
            // succeeds for this verified physical device.
            return !string.IsNullOrWhiteSpace(verifiedIdentity);
        }

        private void CompleteDeferredCleanupCore(
            string serial,
            string deviceIdentity)
        {
            var pendingEntries = GetMatchingDeferredCleanupEntries(
                serial,
                deviceIdentity);
            if (pendingEntries.Count == 0) return;
            RemoveDeferredCleanupEntries(pendingEntries);
            _logService.Info(LocalizationService.Format(
                "Log.Dex.DeferredCleanupCompleted",
                serial));
        }

        private List<DeferredDisplayCleanup>
            GetMatchingDeferredCleanupEntries(
            string serial,
            string deviceIdentity)
        {
            var matches = new List<DeferredDisplayCleanup>();
            if (!HasStableIdentity(deviceIdentity)) return matches;
            foreach (var pending in _pendingDisplayCleanup)
            {
                if (HasStableIdentity(pending.DeviceIdentity) &&
                    string.Equals(
                        pending.DeviceIdentity,
                        deviceIdentity,
                        StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(pending);
                }
            }
            return matches;
        }

        private void RemoveDeferredCleanupEntries(
            IList<DeferredDisplayCleanup> entries)
        {
            for (var index = 0; index < entries.Count; index++)
                _pendingDisplayCleanup.Remove(entries[index]);
        }

        private static bool HasStableIdentity(string identity)
        {
            return !string.IsNullOrWhiteSpace(identity) &&
                !PhysicalDeviceRegistry.IsTemporaryIdentity(identity);
        }

        private bool ReleaseDisplayLease(
            VirtualDisplayLease lease,
            string expectedDeviceIdentity)
        {
            if (lease == null) return true;
            string verifiedIdentity;
            var cleanupSerial = FindVerifiedCleanupTransport(
                lease.Serial,
                expectedDeviceIdentity,
                out verifiedIdentity);
            if (string.IsNullOrWhiteSpace(cleanupSerial))
                return false;

            if (string.Equals(
                cleanupSerial,
                lease.Serial,
                StringComparison.OrdinalIgnoreCase))
            {
                return _virtualDisplayService.Release(lease);
            }

            var reset = _virtualDisplayService.Reset(cleanupSerial);
            if (reset) lease.OwnsOverlaySetting = false;
            return reset;
        }

        private bool CleanupConnectedTargetOverlay(
            string serial,
            string expectedDeviceIdentity,
            out string verifiedDeviceIdentity)
        {
            var cleanupSerial = FindVerifiedCleanupTransport(
                serial,
                expectedDeviceIdentity,
                out verifiedDeviceIdentity);
            if (string.IsNullOrWhiteSpace(cleanupSerial))
                return false;

            return _virtualDisplayService.Reset(cleanupSerial);
        }

        private string FindVerifiedCleanupTransport(
            string preferredSerial,
            string expectedDeviceIdentity,
            out string verifiedDeviceIdentity)
        {
            verifiedDeviceIdentity = GetVerifiedDeviceIdentity(
                preferredSerial,
                expectedDeviceIdentity);
            if (!string.IsNullOrWhiteSpace(verifiedDeviceIdentity))
                return preferredSerial;

            if (!HasStableIdentity(expectedDeviceIdentity))
                return string.Empty;

            IList<AdbDeviceInfo> devices;
            if (!_adbService.TryGetDevices(false, out devices) ||
                devices == null)
            {
                return string.Empty;
            }

            foreach (var device in devices)
            {
                if (device == null ||
                    device.Status != AdbDeviceStatus.Device ||
                    string.IsNullOrWhiteSpace(device.Serial) ||
                    string.Equals(
                        device.Serial,
                        preferredSerial,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var liveIdentity = _adbService.GetDeviceIdentity(
                    device.Serial);
                if (!string.Equals(
                    liveIdentity,
                    expectedDeviceIdentity,
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                verifiedDeviceIdentity = liveIdentity;
                return device.Serial;
            }
            return string.Empty;
        }

        private string GetVerifiedDeviceIdentity(
            string serial,
            string expectedDeviceIdentity)
        {
            if (string.IsNullOrWhiteSpace(serial) ||
                !_adbService.IsAuthorizedDeviceConnected(serial))
            {
                return string.Empty;
            }

            var liveIdentity = _adbService.GetDeviceIdentity(serial);
            if (!HasStableIdentity(liveIdentity)) return string.Empty;
            if (HasStableIdentity(expectedDeviceIdentity) &&
                !string.Equals(
                    expectedDeviceIdentity,
                    liveIdentity,
                    StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }
            return liveIdentity;
        }

        private void DeferDisplayCleanup(ManagedDisplaySession session)
        {
            if (session == null) return;
            DeferDisplayCleanup(
                session.DisplayLease,
                session.DeviceIdentity);
            ClearSession(session);
        }

        private void DeferDisplayCleanup(
            VirtualDisplayLease lease,
            string deviceIdentity)
        {
            if (lease == null || string.IsNullOrWhiteSpace(lease.Serial))
                return;
            var normalizedIdentity = deviceIdentity ?? string.Empty;
            for (var index = 0;
                index < _pendingDisplayCleanup.Count;
                index++)
            {
                var pending = _pendingDisplayCleanup[index];
                if (string.Equals(
                        pending.Lease.Serial,
                        lease.Serial,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        pending.DeviceIdentity,
                        normalizedIdentity,
                        StringComparison.OrdinalIgnoreCase))
                {
                    pending.Lease = lease;
                    _logService.Warning(LocalizationService.Format(
                        "Log.Dex.DeferredCleanupStored",
                        lease.Serial));
                    return;
                }
            }
            _pendingDisplayCleanup.Add(new DeferredDisplayCleanup
            {
                DeviceIdentity = normalizedIdentity,
                Lease = lease
            });
            _logService.Warning(LocalizationService.Format(
                "Log.Dex.DeferredCleanupStored",
                lease.Serial));
        }

        private sealed class DeferredDisplayCleanup
        {
            public string DeviceIdentity { get; set; }
            public VirtualDisplayLease Lease { get; set; }
        }

        private static VirtualDisplayLease GetRetainedLease(
            Exception error,
            VirtualDisplayLease current)
        {
            if (current != null || error == null) return current;
            return error.Data[VirtualDisplayService.RetainedLeaseDataKey]
                as VirtualDisplayLease;
        }

        private void ThrowIfShutdownRequested()
        {
            if (IsShutdownRequested)
                throw new OperationCanceledException();
        }

        private void SaveLastSuccess(
            string serial,
            string deviceIdentity,
            int displayId)
        {
            _settingsService.UpdateAndSave(_settings, delegate(
                AppSettings settings)
            {
                var runSettings = GetDeviceRunSettings(
                    settings,
                    deviceIdentity);
                runSettings.LastSuccess.Width =
                    runSettings.VirtualDisplay.Width;
                runSettings.LastSuccess.Height =
                    runSettings.VirtualDisplay.Height;
                runSettings.LastSuccess.Dpi =
                    runSettings.VirtualDisplay.Dpi;
                runSettings.LastSuccess.AdbPath = _adbService.AdbPath;
                runSettings.LastSuccess.ScrcpyPath =
                    _scrcpyService.ScrcpyPath;
                runSettings.LastSuccess.ScrcpyArguments =
                    _scrcpyService.BuildArguments(
                        runSettings.Scrcpy,
                        displayId,
                        serial);
                runSettings.LastSuccess.DisplayId = displayId;
                runSettings.LastSuccess.SavedAtUtc =
                    DateTime.UtcNow.ToString("o");
            });
        }

        private void TrackSession(
            string mode,
            string serial,
            string deviceIdentity,
            VirtualDisplayLease lease)
        {
            _currentSession = new ManagedDisplaySession
            {
                Mode = mode,
                Serial = serial,
                DeviceIdentity = deviceIdentity ?? string.Empty,
                AppPackage = GetDeviceRunSettings(deviceIdentity)
                    .Scrcpy.StartAppPackage,
                DisplayId = lease.DisplayId,
                ScrcpyProcessId = _scrcpyService.CurrentProcessId,
                CreatedAtUtc = DateTime.UtcNow.ToString("o"),
                DisplayLease = lease
            };
            _runtimeSessions.SetDexSession(serial, _currentSession);
            _logService.Info(LocalizationService.Format(
                "Log.Dex.SessionStarted",
                _currentSession));
        }

        private DeviceRunSettingsProfile GetDeviceRunSettings(
            string deviceIdentity)
        {
            return GetDeviceRunSettings(
                _settings,
                deviceIdentity);
        }

        private DeviceRunSettingsProfile GetDeviceRunSettings(
            AppSettings settings,
            string deviceIdentity)
        {
            if (!string.IsNullOrWhiteSpace(deviceIdentity))
                return settings.GetOrCreateDeviceRunSettings(
                    deviceIdentity);
            return new DeviceRunSettingsProfile
            {
                DeviceIdentity = string.Empty,
                VirtualDisplay = settings.VirtualDisplay,
                Scrcpy = settings.Scrcpy,
                LastSuccess = settings.LastSuccess,
                SingleWindowSlots = settings.SingleWindowSlots,
                SingleWindowAppProfiles =
                    settings.SingleWindowAppProfiles
            };
        }

        private void ClearSession(ManagedDisplaySession session)
        {
            if (session == null ||
                !ReferenceEquals(_currentSession, session)) return;
            _logService.Info(LocalizationService.Format(
                "Log.Dex.SessionEnded",
                session));
            _runtimeSessions.SetDexSession(session.Serial, null);
            _currentSession = null;
        }
    }
}
