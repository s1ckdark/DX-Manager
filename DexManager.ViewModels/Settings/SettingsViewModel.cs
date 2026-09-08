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
    /// </summary>
    private void LoadDevicePages(string identity)
    {
        DetachDevicePageHandlers();

        DisplayStream = string.IsNullOrEmpty(identity)
            ? null
            : new DisplayStreamSettingsViewModel(identity, _gateway);
        Slot = string.IsNullOrEmpty(identity)
            ? null
            : new SlotSettingsViewModel(identity, _gateway);

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
    }

    /// <summary>
    /// 페이지별 SaveCommand를 각각 한 번씩 흘려보낸다. 유효하지 않은
    /// 페이지(SaveCommand.CanExecute(null)이 false, 예: 잘못된 입력)는
    /// 건너뛴다 - 바인딩이 이미 그 페이지의 저장 버튼을 비활성화하지만,
    /// 바인딩을 우회해 SaveAll이 강제로 불려도 잘못된 입력이 조용히
    /// 저장되지 않도록 여기서도 같은 조건을 존중한다.
    ///
    /// 모든 저장과 재계산이 끝난 뒤 <see cref="CloseRequested"/>를 올려
    /// Desktop 레이어가 설정 창을 닫게 한다 - 유효하지 않아 건너뛴 페이지가
    /// 있어도 나머지 페이지가 저장됐다면 창은 닫힌다(그 페이지의 편집은
    /// 조용히 버려진다; 표준 다이얼로그 관례를 따른 선택이다).
    /// </summary>
    [RelayCommand]
    private void SaveAll()
    {
        TrySave(Paths.SaveCommand);
        TrySave(Appearance.SaveCommand);
        TrySave(Interaction.SaveCommand);
        if (DisplayStream != null) TrySave(DisplayStream.SaveCommand);
        if (Slot != null) TrySave(Slot.SaveCommand);

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

        LoadDevicePages(_deviceSelection.SelectedIdentity);

        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

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
