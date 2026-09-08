using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DexManager.Models;
using DexManager.Services;

namespace DexManager.ViewModels;

/// <summary>
/// 외관(테마·언어) 설정 편집 페이지. 둘 다 기기별이 아니라 전역 설정
/// (<see cref="AppSettings.Theme"/>, <see cref="AppSettings.Language"/>)
/// 이므로 identity를 받지 않는다(PathsSettingsViewModel과 같은 전역 편집
/// 패턴).
///
/// 테마와 언어는 적용 경로가 다르다: 테마는 Avalonia의 ThemeVariant로
/// 매핑해야 해서 DexManager.Desktop의 ThemeApplier가 ThemeSaved 이벤트를
/// 구독해 처리한다(이 뷰모델은 Avalonia를 몰라야 하므로). 반면
/// <see cref="LocalizationService"/>는 DexManager.Core에 있어 Avalonia를
/// 참조하지 않으므로, 이 뷰모델이 저장과 동시에 직접 Apply를 호출한다.
/// 단, LocalizationService.Apply는 CultureInfo만 바꿀 뿐 이미 그려진
/// Avalonia XAML 문자열을 다시 그리지 못한다 — 그래서 언어 변경은 새로
/// 만들어지는 문자열에만 반영되고, 실행 중인 창은 재시작해야 반영된다
/// (<see cref="LanguageRestartNotice"/> 참고).
/// </summary>
public sealed partial class AppearanceSettingsViewModel : ObservableObject
{
    private readonly ISettingsGateway _gateway;

    // 마지막으로 로드하거나 저장한 값의 스냅샷. HasChanges 계산 기준선이다.
    private AppTheme _baselineTheme;
    private AppLanguage _baselineLanguage;

    /// <param name="gateway">설정 읽기·쓰기 경계.</param>
    public AppearanceSettingsViewModel(ISettingsGateway gateway)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));

        SelectedTheme = _gateway.Current.Theme;
        SelectedLanguage = _gateway.Current.Language;

        CaptureBaseline();
    }

    /// <summary>편집 중인 테마 선택값.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private AppTheme _selectedTheme;

    /// <summary>편집 중인 언어 선택값.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private AppLanguage _selectedLanguage;

    /// <summary>
    /// 테마가 저장될 때 발생한다. 저장된 <see cref="AppTheme"/>을 인자로
    /// 넘긴다. Avalonia 타입을 몰라야 하므로 여기서는 순수 모델 값만
    /// 넘기고, 실제 화면 반영(ThemeVariant 매핑)은
    /// DexManager.Desktop의 ThemeApplier가 이 이벤트를 구독해 처리한다.
    /// </summary>
    public event EventHandler<AppTheme> ThemeSaved;

    /// <summary>마지막으로 불러오거나 저장한 값과 비교해 편집된 내용이 있는지.</summary>
    public bool HasChanges => SelectedTheme != _baselineTheme || SelectedLanguage != _baselineLanguage;

    /// <summary>
    /// 언어 변경의 적용 범위를 설정 화면에 정직하게 알리는 문구.
    /// LocalizationService.Apply는 새로 생성되는 문자열에는 즉시 반영되지만,
    /// 이미 그려진 Avalonia 창의 텍스트는 다시 그리지 않는다 — 따라서 이
    /// 문구 자체는 "즉시 적용됨"을 절대 주장하지 않고, 재시작 후 완전히
    /// 반영된다고만 말한다. resx의 Settings.LanguageRestart 키를 그대로
    /// 쓰므로 이 문구 자체도 현재 선택된 언어로 표시된다.
    /// </summary>
    public string LanguageRestartNotice => LocalizationService.Get("Settings.LanguageRestart");

    private void CaptureBaseline()
    {
        _baselineTheme = SelectedTheme;
        _baselineLanguage = SelectedLanguage;
    }

    private bool CanSave() => true;

    /// <summary>
    /// 선택한 테마·언어를 전역 설정에 저장한다. 테마는 구독자(Desktop의
    /// ThemeApplier)에게 ThemeSaved로 알려 즉시 재적용시키고, 언어는
    /// LocalizationService.Apply를 직접 호출해 새로 생성되는 문자열부터
    /// 새 언어를 쓰게 한다(이미 그려진 창은 재시작 전까지 그대로다).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        _gateway.Update(s =>
        {
            s.Theme = SelectedTheme;
            s.Language = SelectedLanguage;
        });

        LocalizationService.Apply(SelectedLanguage);

        CaptureBaseline();
        OnPropertyChanged(nameof(HasChanges));
        OnPropertyChanged(nameof(LanguageRestartNotice));

        ThemeSaved?.Invoke(this, SelectedTheme);
    }
}
