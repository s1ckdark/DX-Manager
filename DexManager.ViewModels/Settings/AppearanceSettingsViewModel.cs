using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DexManager.Models;

namespace DexManager.ViewModels;

/// <summary>
/// 외관(테마) 설정 편집 페이지. 테마는 기기별이 아니라 전역 설정
/// (<see cref="AppSettings.Theme"/>)이므로 identity를 받지 않는다
/// (PathsSettingsViewModel과 같은 전역 편집 패턴).
///
/// 언어 선택(Task 8)이 이 파일에 추가될 예정이다. 그때까지 테마 관련
/// 로직만 다루고, 다른 관심사가 섞이지 않도록 baseline·HasChanges·
/// 저장 흐름을 테마 하나만으로 구성해 둔다.
/// </summary>
public sealed partial class AppearanceSettingsViewModel : ObservableObject
{
    private readonly ISettingsGateway _gateway;

    // 마지막으로 로드하거나 저장한 값의 스냅샷. HasChanges 계산 기준선이다.
    private AppTheme _baselineTheme;

    /// <param name="gateway">설정 읽기·쓰기 경계.</param>
    public AppearanceSettingsViewModel(ISettingsGateway gateway)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));

        SelectedTheme = _gateway.Current.Theme;

        CaptureBaseline();
    }

    /// <summary>편집 중인 테마 선택값.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private AppTheme _selectedTheme;

    /// <summary>
    /// 테마가 저장될 때 발생한다. 저장된 <see cref="AppTheme"/>을 인자로
    /// 넘긴다. Avalonia 타입을 몰라야 하므로 여기서는 순수 모델 값만
    /// 넘기고, 실제 화면 반영(ThemeVariant 매핑)은
    /// DexManager.Desktop의 ThemeApplier가 이 이벤트를 구독해 처리한다.
    /// </summary>
    public event EventHandler<AppTheme> ThemeSaved;

    /// <summary>마지막으로 불러오거나 저장한 값과 비교해 편집된 내용이 있는지.</summary>
    public bool HasChanges => SelectedTheme != _baselineTheme;

    private void CaptureBaseline()
    {
        _baselineTheme = SelectedTheme;
    }

    private bool CanSave() => true;

    /// <summary>
    /// 선택한 테마를 전역 설정에 저장하고, 구독자에게 즉시 재적용을
    /// 알린다.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        _gateway.Update(s => s.Theme = SelectedTheme);

        CaptureBaseline();
        OnPropertyChanged(nameof(HasChanges));

        ThemeSaved?.Invoke(this, SelectedTheme);
    }
}
