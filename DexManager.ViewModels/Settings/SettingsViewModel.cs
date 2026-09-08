using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
    private bool _disposed;

    /// <param name="gateway">설정 읽기·쓰기 경계. 다섯 페이지 전부가 공유한다.</param>
    /// <param name="deviceSelection">대상 기기 선택 출처. DeviceListViewModel
    /// 전체가 아니라 이 좁은 인터페이스만 필요로 한다.</param>
    public SettingsViewModel(ISettingsGateway gateway, IDeviceSelectionSource deviceSelection)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _deviceSelection = deviceSelection ?? throw new ArgumentNullException(nameof(deviceSelection));

        _paths = new PathsSettingsViewModel(_gateway);
        _appearance = new AppearanceSettingsViewModel(_gateway);
        _interaction = new InteractionSettingsViewModel(_gateway);
        AttachGlobalPageHandlers();

        LoadDevicePages(_deviceSelection.SelectedIdentity);

        _deviceSelection.PropertyChanged += OnDeviceSelectionPropertyChanged;
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

    private void OnDeviceSelectionPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IDeviceSelectionSource.SelectedIdentity)) return;
        LoadDevicePages(_deviceSelection.SelectedIdentity);
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
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _deviceSelection.PropertyChanged -= OnDeviceSelectionPropertyChanged;
        DetachGlobalPageHandlers();
        DetachDevicePageHandlers();
    }
}
