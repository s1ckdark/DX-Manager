using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DexManager.Models;
using DexManager.Services;

namespace DexManager.ViewModels;

/// <summary>
/// 설정 창의 최상위 ViewModel. 전역 페이지 3개(Paths/Appearance/Interaction)와
/// 기기별 페이지 2개(DisplayStream/Slot)를 묶고, 대상 기기 선택을 추적하며
/// 전역 저장/취소를 조율한다. 이 타입 자체는 창을 그리지 않는다 - 창은
/// Desktop 레이어(Task 12)의 몫이고, 여기서는 다섯 페이지와 SaveAll/Cancel
/// 커맨드, 집계된 HasChanges만 제공한다.
///
/// 기기별 페이지는 <see cref="IDeviceSelectionSource.SelectedIdentity"/>가
/// 바뀔 때마다 새 identity로 다시 생성된다 - 각 페이지 생성자가 게이트웨이에서
/// 프로필을 로드하므로, 재생성이 곧 재로드다. 선택된 기기가 없으면(identity가
/// null) 기기별 페이지도 null이다 - 화면은 이 경우 해당 탭을 비활성화하거나
/// 안내 문구로 대체해야 한다(Task 12의 몫).
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly ISettingsGateway _gateway;
    private readonly IDeviceSelectionSource _deviceSelection;
    private readonly DeviceRuntimeSessionRegistry _sessions;
    private readonly IUiDispatcher _dispatcher;
    private bool _disposed;

    // 지금 화면에 올라와 있는 기기별 페이지 2개가 "어느 identity로"
    // 만들어졌는지. LoadDevicePages의 재생성 가드 기준이다(F-1).
    private string _devicePagesIdentity;
    private bool _devicePagesLoaded;

    /// <param name="gateway">설정 읽기·쓰기 경계. 다섯 페이지 전부가 공유한다.</param>
    /// <param name="deviceSelection">대상 기기 선택 출처. DeviceListViewModel
    /// 전체가 아니라 이 좁은 인터페이스만 필요로 한다.</param>
    /// <param name="sessions">대상 기기의 DeX 실행 여부를 읽는 출처. Phase 2의
    /// <see cref="DeviceViewModel"/>과 동일한 런타임 레지스트리를 공유한다 -
    /// 실행 중 변경은 다음 시작부터 적용된다는 Global Constraint를 화면이
    /// 정직하게 알릴 수 있도록 <see cref="IsTargetDexRunning"/>을 채운다.</param>
    /// <param name="dispatcher">레지스트리의 <c>Changed</c>는 런타임 스레드에서
    /// 오므로, 관측 가능한 상태를 건드리기 전에 UI 스레드로 마샬링한다.</param>
    public SettingsViewModel(
        ISettingsGateway gateway,
        IDeviceSelectionSource deviceSelection,
        DeviceRuntimeSessionRegistry sessions,
        IUiDispatcher dispatcher)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _deviceSelection = deviceSelection ?? throw new ArgumentNullException(nameof(deviceSelection));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        _paths = new PathsSettingsViewModel(_gateway);
        _appearance = new AppearanceSettingsViewModel(_gateway);
        _interaction = new InteractionSettingsViewModel(_gateway);
        AttachGlobalPageHandlers();

        LoadDevicePages(_deviceSelection.SelectedIdentity);

        _deviceSelection.PropertyChanged += OnDeviceSelectionPropertyChanged;

        // DeviceViewModel과 같은 순서: 슬롯/페이지를 먼저 만든 뒤 구독하고,
        // 마지막으로 현재 스냅샷을 한 번 적용해 초기 상태를 채운다.
        _sessions.Changed += OnSessionsChanged;
        ApplyRuntime(_sessions.Current);
    }

    /// <summary>전역 경로 설정 페이지.</summary>
    [ObservableProperty]
    private PathsSettingsViewModel _paths;

    /// <summary>전역 외관(테마·언어) 설정 페이지. Task 12는 이 인스턴스의
    /// ThemeSaved 이벤트를 직접 구독한다 - Cancel이 이 인스턴스를 교체하면
    /// 다시 구독해야 한다(클래스 문서의 Cancel 항목 참고).</summary>
    [ObservableProperty]
    private AppearanceSettingsViewModel _appearance;

    /// <summary>전역 상호작용(단축키) 설정 페이지.</summary>
    [ObservableProperty]
    private InteractionSettingsViewModel _interaction;

    /// <summary>선택된 기기의 화면/스트림 설정 페이지. 선택된 기기가 없으면 null.</summary>
    [ObservableProperty]
    private DisplayStreamSettingsViewModel _displayStream;

    /// <summary>선택된 기기의 슬롯 설정 페이지. 선택된 기기가 없으면 null.</summary>
    [ObservableProperty]
    private SlotSettingsViewModel _slot;

    /// <summary>다섯 페이지 중 하나라도 저장되지 않은 편집이 있는지.</summary>
    [ObservableProperty]
    private bool _hasChanges;

    /// <summary>
    /// SaveAll 또는 Cancel이 끝나면(각 커맨드의 기존 동작을 모두 마친 뒤)
    /// 발생한다. 이 타입은 Avalonia에 의존하지 않으므로 창을 직접 닫지
    /// 못한다 - Desktop 레이어(App.axaml.cs)가 이 이벤트를 구독해 설정
    /// 창을 닫는다. ShellViewModel.SettingsRequested,
    /// AppearanceSettingsViewModel.ThemeSaved와 같은 이벤트 패턴이다.
    /// </summary>
    public event EventHandler CloseRequested;

    /// <summary>
    /// 대상 기기(<see cref="IDeviceSelectionSource.SelectedIdentity"/>)에서
    /// 지금 이 순간 DeX가 실행 중인지. true여도 저장은 그대로 허용된다 -
    /// Global Constraint("실행 중 변경은 다음 시작부터")를 화면이 정직하게
    /// 알리기 위한 안내용 플래그일 뿐이다. 실제 문구는 Task 12(뷰)의 몫이다.
    /// 대상 기기가 없으면(identity가 비어 있으면) 항상 false다.
    /// </summary>
    [ObservableProperty]
    private bool _isTargetDexRunning;

    /// <summary>
    /// 다섯 페이지가 모두 저장 가능한(유효한) 상태인지. 검증이 없는
    /// 페이지(Paths/Appearance)는 항상 유효한 것으로 본다.
    ///
    /// Save 버튼은 <see cref="HasChanges"/>와 이 값을 함께 본다. HasChanges만
    /// 보고 열어두면, 유효하지 않은 페이지는 <see cref="SaveAll"/>이 조용히
    /// 건너뛴 채로 창이 닫혀 그 페이지의 편집이 아무 안내 없이 사라진다 -
    /// 인라인 검증 배너는 창과 함께 사라지므로 사용자는 무엇이 잘못됐는지
    /// 볼 기회조차 없다. 비활성화된 Save가 사용자를 그 자리에 붙잡아 둔다.
    /// </summary>
    [ObservableProperty]
    private bool _areAllPagesValid = true;

    /// <summary>
    /// 마지막 <see cref="SaveAll"/> 시도가 실패했을 때의 사용자 안내 문구.
    /// 성공하면 빈 문자열이다. Phase 2의
    /// <see cref="DeviceViewModel.LastCommandMessage"/>와 같은 패턴 -
    /// 뷰가 이 문자열을 TextBlock에 바인딩해 보여준다.
    /// </summary>
    [ObservableProperty]
    private string _saveErrorMessage = string.Empty;

    private void OnDeviceSelectionPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IDeviceSelectionSource.SelectedIdentity)) return;
        LoadDevicePages(_deviceSelection.SelectedIdentity);

        // 대상 기기가 바뀌었으니 새 identity 기준으로 다시 계산한다. 이
        // 핸들러는 SelectedIdentity의 setter가 직접 호출하므로 이미
        // 호출자의 스레드(대개 UI 스레드)에서 실행된다 - 레지스트리
        // Changed처럼 런타임 스레드에서 오는 게 아니라서 마샬링이 필요 없다.
        ApplyRuntime(_sessions.Current);
    }

    private void OnSessionsChanged(
        object sender,
        DeviceRuntimeRegistryChangedEventArgs e)
    {
        // 런타임 스레드에서 온다. 관측 가능한 상태를 건드리기 전에
        // UI 스레드로 넘긴다.
        var snapshot = e?.Snapshot;
        _dispatcher.Post(() => ApplyRuntime(snapshot));
    }

    private void ApplyRuntime(DeviceRuntimeRegistrySnapshot snapshot)
    {
        // Post는 비동기다. 구독을 해제해도 이미 큐에 들어간 클로저는
        // 되돌릴 수 없으므로 실행 시점에 다시 확인해야 한다.
        if (_disposed) return;

        var identity = _deviceSelection.SelectedIdentity;
        var session = string.IsNullOrEmpty(identity)
            ? null
            : snapshot?.FindByIdentity(identity);
        IsTargetDexRunning = session?.Dex?.IsRunning == true;
    }

    /// <summary>
    /// 기기별 페이지 2개를 주어진 identity로 (재)생성한다. identity가
    /// 비어 있으면(선택된 기기 없음) 두 페이지 모두 null로 만든다.
    ///
    /// 지금 올라와 있는 페이지가 이미 같은 identity로 만들어져 있으면
    /// 아무 것도 하지 않는다(재생성도, 재계산도 없다). SelectedIdentity
    /// 변경 통지는 identity "문자열"이 아니라 DeviceViewModel "참조"가
    /// 바뀔 때 올라오기 때문이다 - USB가 한 번 흔들리거나 전송 방식이
    /// 바뀌어 DeviceListViewModel.Apply가 "같은 폰"의 행을 새로 만들어
    /// 다시 선택하기만 해도 통지가 뜨고, 가드가 없으면 그때마다 사용자의
    /// 미저장 편집이 아무 안내 없이 디스크 값으로 되돌아간다(F-1).
    ///
    /// 의도적으로 남겨둔 범위: 사용자가 "정말로 다른 기기를" 고른 경우는
    /// 지금도 그대로 재로드하며, 그 탭의 미저장 편집은 버려진다. 그때
    /// 확인을 묻거나 편집을 identity별로 보관하려면 다이얼로그 이음새가
    /// 필요해 이번 라운드의 범위 밖으로 미뤘다.
    /// </summary>
    /// <param name="force">identity가 같아도 강제로 다시 만든다.
    /// Cancel이 "편집 버리기"를 페이지 재생성으로 구현하므로 그 경로는
    /// 가드를 우회해야 한다.</param>
    private void LoadDevicePages(string identity, bool force = false)
    {
        if (!force &&
            _devicePagesLoaded &&
            string.Equals(
                _devicePagesIdentity ?? string.Empty,
                identity ?? string.Empty,
                StringComparison.Ordinal))
        {
            return;
        }

        DetachDevicePageHandlers();

        DisplayStream = string.IsNullOrEmpty(identity)
            ? null
            : new DisplayStreamSettingsViewModel(identity, _gateway);
        Slot = string.IsNullOrEmpty(identity)
            ? null
            : new SlotSettingsViewModel(identity, _gateway);

        _devicePagesIdentity = identity;
        _devicePagesLoaded = true;

        AttachDevicePageHandlers();
        RecomputeHasChanges();
    }

    private void AttachGlobalPageHandlers()
    {
        Paths.PropertyChanged += OnPagePropertyChanged;
        Appearance.PropertyChanged += OnPagePropertyChanged;
        Interaction.PropertyChanged += OnPagePropertyChanged;
    }

    private void DetachGlobalPageHandlers()
    {
        Paths.PropertyChanged -= OnPagePropertyChanged;
        Appearance.PropertyChanged -= OnPagePropertyChanged;
        Interaction.PropertyChanged -= OnPagePropertyChanged;
    }

    private void AttachDevicePageHandlers()
    {
        if (DisplayStream != null) DisplayStream.PropertyChanged += OnPagePropertyChanged;
        if (Slot != null) Slot.PropertyChanged += OnPagePropertyChanged;
    }

    private void DetachDevicePageHandlers()
    {
        if (DisplayStream != null) DisplayStream.PropertyChanged -= OnPagePropertyChanged;
        if (Slot != null) Slot.PropertyChanged -= OnPagePropertyChanged;
    }

    // 페이지가 올리는 모든 PropertyChanged에 반응해 집계를 다시 계산한다.
    // "HasChanges" 알림만 걸러 듣고 싶었지만, PathsSettingsViewModel과
    // AppearanceSettingsViewModel은 편집 시 HasChanges 변경 통지를 올리지
    // 않는다(Revalidate가 없는 단순 페이지라 Save 시점에만 통지한다) -
    // 반면 DisplayStream/Slot/Interaction은 Revalidate에서 매번 통지한다.
    // 이 비일관성에 기대지 않도록, 어떤 프로퍼티가 바뀌었든 항상 다시
    // 계산한다 - RecomputeHasChanges는 각 페이지의 HasChanges를 그때그때
    // 새로 읽으므로 과호출이어도 결과는 항상 정확하다.
    private void OnPagePropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        RecomputeHasChanges();
    }

    private void RecomputeHasChanges()
    {
        HasChanges =
            Paths.HasChanges
            || Appearance.HasChanges
            || Interaction.HasChanges
            || (DisplayStream?.HasChanges ?? false)
            || (Slot?.HasChanges ?? false);

        // 유효성 집계는 HasChanges와 정확히 같은 계기로 다시 계산한다 -
        // Save 게이트가 두 값을 함께 보기 때문에, 둘의 갱신 시점이
        // 어긋나면 버튼이 한 박자 늦게 열리거나 닫힌다.
        // Paths/Appearance는 검증이 없는 페이지라 항상 유효로 본다.
        AreAllPagesValid =
            Interaction.IsValid
            && (DisplayStream?.IsValid ?? true)
            && (Slot?.IsValid ?? true);
    }

    /// <summary>
    /// 페이지별 SaveCommand를 각각 한 번씩 흘려보낸다. 유효하지 않은
    /// 페이지(SaveCommand.CanExecute(null)이 false, 예: 잘못된 입력)는
    /// 건너뛴다 - 바인딩이 이미 그 페이지의 저장 버튼을 비활성화하지만,
    /// 바인딩을 우회해 SaveAll이 강제로 불려도 잘못된 입력이 조용히
    /// 저장되지 않도록 여기서도 같은 조건을 존중한다.
    ///
    /// 모든 저장과 재계산이 끝난 뒤 <see cref="CloseRequested"/>를 올려
    /// Desktop 레이어가 설정 창을 닫게 한다. Save 버튼 자체가
    /// <see cref="HasChanges"/>와 <see cref="AreAllPagesValid"/>를 함께 보므로
    /// 유효하지 않은 페이지를 남긴 채로는 여기까지 올 수 없다.
    ///
    /// 페이지별 Save는 각각 게이트웨이를 통해 디스크까지 내려간다 -
    /// SettingsService.SaveCore는 디스크 가득참, 저장 잠금 타임아웃,
    /// 상위 버전 설정 파일에서 예외를 던진다. 그 예외가 RelayCommand를
    /// 뚫고 나가면 Avalonia UI 스레드에 처리기가 없어 앱이 그대로 죽으므로
    /// 여기서 반드시 붙잡는다. 실패하면 <see cref="CloseRequested"/>를
    /// 올리지 않는다 - 창은 열린 채로 남아야 하고(부분 저장 상태를 사용자가
    /// 볼 수 있어야 한다), 실패 사유는
    /// <see cref="SaveErrorMessage"/>로 화면에 드러낸다.
    /// </summary>
    [RelayCommand]
    private void SaveAll()
    {
        try
        {
            TrySave(Paths.SaveCommand);
            TrySave(Appearance.SaveCommand);
            TrySave(Interaction.SaveCommand);
            if (DisplayStream != null) TrySave(DisplayStream.SaveCommand);
            if (Slot != null) TrySave(Slot.SaveCommand);
        }
        catch (Exception ex)
        {
            // 중간에 끊겼으므로 일부 페이지는 이미 디스크에 반영됐다.
            // 남은 편집이 그대로 dirty로 보이도록 집계를 다시 계산한 뒤,
            // 창을 닫지 않고 사유만 알린다.
            RecomputeHasChanges();
            SaveErrorMessage = LocalizationService.Format(
                "Settings.SaveAllFailed",
                ex.Message);
            return;
        }

        SaveErrorMessage = string.Empty;
        RecomputeHasChanges();

        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private static void TrySave(IRelayCommand command)
    {
        if (command.CanExecute(null)) command.Execute(null);
    }

    /// <summary>
    /// 다섯 페이지를 모두 게이트웨이의 현재 값으로부터 다시 만들어 편집
    /// 사본을 버린다. 각 페이지의 편집은 Save 전까지 AppSettings에
    /// 반영되지 않으므로(페이지 생성자가 프로필을 복사해 편집 사본을
    /// 만들고, Save만 그 사본을 게이트웨이에 되돌려 쓴다), 페이지를
    /// 재생성하는 것이 곧 "원본은 그대로 두고 편집만 버리기"다.
    ///
    /// 주의: Appearance도 재생성 대상이다. Task 12가 Appearance.ThemeSaved를
    /// 구독해 두었다면, 그 구독은 옛 인스턴스에 걸려 있으므로 Cancel 이후
    /// 재구독해야 한다(SettingsViewModel의 Appearance 프로퍼티 변경 통지를
    /// 계기로 삼을 수 있다). 기기 선택 변경은 Appearance를 건드리지 않으므로
    /// 이 재구독 필요성은 Cancel에서만 발생한다.
    ///
    /// 기존 동작(페이지 재생성으로 편집 버리기)은 그대로 두고, 끝에서만
    /// <see cref="CloseRequested"/>를 올린다 - Desktop 레이어가 이를 듣고
    /// 설정 창을 닫는다.
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        DetachGlobalPageHandlers();

        Paths = new PathsSettingsViewModel(_gateway);
        Appearance = new AppearanceSettingsViewModel(_gateway);
        Interaction = new InteractionSettingsViewModel(_gateway);

        AttachGlobalPageHandlers();

        // Cancel의 "편집 버리기"는 페이지 재생성으로 구현돼 있다 -
        // identity가 그대로여도 반드시 다시 만들어야 한다(F-1 가드 우회).
        LoadDevicePages(_deviceSelection.SelectedIdentity, force: true);

        SaveErrorMessage = string.Empty;

        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Dispose가 이미 실행되었는지. ApplicationHost.IsDisposed와
    /// 같은 이유로 공개한다 - 소유자가 이 인스턴스를 계속 들고 있어야
    /// 하는지, 아니면 이미 정리된 채로 버려도 되는지를 테스트와 호출자가
    /// 관측할 수 있어야 한다(예: ShellViewModel.OpenSettings가 구독자
    /// 없이 만든 인스턴스를 즉시 Dispose하는지 검증).</summary>
    public bool IsDisposed => _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _deviceSelection.PropertyChanged -= OnDeviceSelectionPropertyChanged;
        _sessions.Changed -= OnSessionsChanged;
        DetachGlobalPageHandlers();
        DetachDevicePageHandlers();
    }
}
