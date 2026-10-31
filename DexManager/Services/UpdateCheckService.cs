using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using DexManager.Models;

namespace DexManager.Services
{
    public enum UpdateCheckOutcome { Available, Current, Failed, Busy, NotDue, Cancelled }

    public sealed class UpdateCheckResult
    {
        public UpdateCheckOutcome Outcome { get; private set; }
        public ReleaseUpdate Update { get; private set; }
        public bool Notify { get; private set; }

        public UpdateCheckResult(UpdateCheckOutcome outcome, ReleaseUpdate update = null,
            bool notify = false)
        {
            Outcome = outcome;
            Update = update;
            Notify = notify;
        }
    }

    public sealed class UpdateCheckService : IDisposable
    {
        private readonly IReleaseUpdateSource _source;
        private readonly IUpdateCheckStateStore _store;
        private readonly Func<DateTime> _today;
        private readonly Action<string> _log;
        private readonly Version _currentVersion;
        private readonly int _deadlineMs;
        private readonly object _stateSync = new object();
        private readonly CancellationTokenSource _shutdown = new CancellationTokenSource();
        private UpdateCheckState _state;
        private int _checking;
        private int _disposed;

        public UpdateCheckService(string currentVersion, IReleaseUpdateSource source,
            IUpdateCheckStateStore store, Func<DateTime> today = null,
            Action<string> log = null, int deadlineMs = 5000)
        {
            if (!ReleaseUpdate.TryParseVersion(currentVersion, out _currentVersion))
                throw new ArgumentException("Invalid current application version.");
            _source = source ?? throw new ArgumentNullException("source");
            _store = store ?? throw new ArgumentNullException("store");
            _today = today ?? delegate { return DateTime.Today; };
            _log = log ?? delegate { };
            _deadlineMs = Math.Max(20, deadlineMs);
            _state = _store.Load() ?? new UpdateCheckState();
        }

        public ReleaseUpdate KnownUpdate
        {
            get
            {
                lock (_stateSync)
                {
                    ReleaseUpdate update;
                    return ReleaseUpdate.TryCreate(_state.KnownVersion,
                        _state.KnownReleaseUrl, out update) &&
                        update.Version.CompareTo(_currentVersion) > 0 &&
                        !string.Equals(update.VersionText, _state.DismissedVersion,
                            StringComparison.Ordinal) ? update : null;
                }
            }
        }

        public Task<UpdateCheckResult> CheckAsync(bool automatic)
        {
            // Disk state, DNS, proxy discovery, TLS and response reading stay off the UI thread.
            return Task.Run(delegate { return CheckCoreAsync(automatic); });
        }

        private async Task<UpdateCheckResult> CheckCoreAsync(bool automatic)
        {
            if (_disposed != 0) return new UpdateCheckResult(UpdateCheckOutcome.Cancelled);
            if (Interlocked.CompareExchange(ref _checking, 1, 0) != 0)
                return new UpdateCheckResult(UpdateCheckOutcome.Busy);
            try
            {
                var day = _today().Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                lock (_stateSync)
                {
                    if (automatic && string.Equals(_state.LastAttemptDay, day,
                        StringComparison.Ordinal))
                        return new UpdateCheckResult(UpdateCheckOutcome.NotDue);
                    var attempted = _state.Copy();
                    attempted.LastAttemptDay = day;
                    // Persist before any networking, including offline failures.
                    // If persistence is unavailable, automatic requests fail closed.
                    var saved = _store.TrySave(attempted);
                    if (automatic && !saved)
                        return new UpdateCheckResult(UpdateCheckOutcome.NotDue);
                    _state = attempted;
                }
                var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
                var deferredCancellation = false;
                try
                {
                    Task<ReleaseUpdate> pending = null;
                    try
                    {
                        pending = _source.FetchLatestAsync(cancellation.Token);
                        var deadline = Task.Delay(_deadlineMs, cancellation.Token);
                        if (await Task.WhenAny(pending, deadline).ConfigureAwait(false) != pending)
                        {
                            deferredCancellation = true;
                            ThreadPool.QueueUserWorkItem(delegate
                            {
                                try { cancellation.Cancel(); } catch { }
                                finally { cancellation.Dispose(); }
                            });
                            ObserveFailure(pending);
                            _log("Update check ended at its overall deadline; no automatic retry.");
                            return new UpdateCheckResult(_disposed != 0
                                ? UpdateCheckOutcome.Cancelled : UpdateCheckOutcome.Failed);
                        }
                        var update = await pending.ConfigureAwait(false);
                        if (_disposed != 0) return new UpdateCheckResult(UpdateCheckOutcome.Cancelled);
                        lock (_stateSync)
                        {
                            var next = _state.Copy();
                            if (update == null || update.Version.CompareTo(_currentVersion) <= 0)
                            {
                                next.KnownVersion = null;
                                next.KnownReleaseUrl = null;
                                _store.TrySave(next);
                                _state = next;
                                return new UpdateCheckResult(UpdateCheckOutcome.Current);
                            }
                            var notify = automatic &&
                                !string.Equals(next.LastNotifiedVersion, update.VersionText,
                                    StringComparison.Ordinal) &&
                                !string.Equals(next.DismissedVersion, update.VersionText,
                                    StringComparison.Ordinal);
                            next.KnownVersion = update.VersionText;
                            // Keep the original tag in the cache so its URL can be revalidated.
                            next.KnownVersion = new Uri(update.ReleaseUrl).Segments[
                                new Uri(update.ReleaseUrl).Segments.Length - 1];
                            next.KnownVersion = Uri.UnescapeDataString(next.KnownVersion);
                            next.KnownReleaseUrl = update.ReleaseUrl;
                            if (notify) next.LastNotifiedVersion = update.VersionText;
                            _store.TrySave(next);
                            _state = next;
                            return new UpdateCheckResult(UpdateCheckOutcome.Available, update, notify);
                        }
                    }
                    catch (ReleaseCheckBusyException)
                    {
                        return new UpdateCheckResult(UpdateCheckOutcome.Busy);
                    }
                    catch
                    {
                        if (pending != null) ObserveFailure(pending);
                        _log("Update check could not complete; no automatic retry.");
                        return new UpdateCheckResult(_disposed != 0
                            ? UpdateCheckOutcome.Cancelled : UpdateCheckOutcome.Failed);
                    }
                }
                finally { if (!deferredCancellation) cancellation.Dispose(); }
            }
            finally { Interlocked.Exchange(ref _checking, 0); }
        }

        private static void ObserveFailure(Task task)
        {
            task.ContinueWith(failed => { failed.Exception.Handle(delegate { return true; }); },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        public void Dismiss(ReleaseUpdate update)
        {
            if (update == null) return;
            lock (_stateSync)
            {
                var next = _state.Copy();
                next.DismissedVersion = update.VersionText;
                _store.TrySave(next);
                _state = next;
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            ThreadPool.QueueUserWorkItem(delegate { try { _shutdown.Cancel(); } catch { } });
            // A late DNS response cannot produce a UI result or another physical request.
        }
    }
}
