using System;
using System.Drawing;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using DexManager.Forms;
using DexManager.Models;
using DexManager.Services;

internal static class UpdateNoticePreview
{
    private static readonly Assembly Product = typeof(MainForm).Assembly;
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread] private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        LocalizationService.Apply(AppLanguage.Korean);
        var host = new Form
        {
            Text = "업데이트 안내 미리보기 · 인터넷 연결 없이 확인",
            StartPosition = FormStartPosition.CenterScreen,
            ClientSize = new Size(640, 226), Font = new Font("맑은 고딕", 10),
            FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false
        };
        var theme = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList,
            Bounds = new Rectangle(24, 20, 160, 32) };
        theme.Items.AddRange(new object[] { "밝은 테마", "다크 테마" });
        var note = new Label
        {
            Text = "예시 버전 2.0.0 → 2.0.1로 실제 안내창 모양을 확인합니다.\n업데이트 조회·설치나 사용자 설정 변경은 하지 않습니다.",
            Bounds = new Rectangle(24, 68, 594, 54)
        };
        host.Controls.AddRange(new Control[] { theme, note });
        Action apply = delegate
        {
            var paletteType = Product.GetType("DexManager.Forms.ThemeColors");
            var palette = paletteType.GetMethod("Use").Invoke(null,
                new object[] { theme.SelectedIndex == 1 ? AppTheme.Dark : AppTheme.Light });
            host.BackColor = (Color)palette.GetType().GetProperty("WindowBackground").GetValue(palette, null);
            note.ForeColor = (Color)palette.GetType().GetProperty("TextPrimary").GetValue(palette, null);
        };
        theme.SelectedIndexChanged += delegate { apply(); };
        theme.SelectedIndex = 0;
        ReleaseUpdate release;
        ReleaseUpdate.TryCreate("v2.0.1",
            "https://github.com/maze-mei/DX-Manager/releases/tag/v2.0.1", out release);
        var outcomes = new[] { UpdateCheckOutcome.Available, UpdateCheckOutcome.Current,
            UpdateCheckOutcome.Failed };
        var titles = new[] { "새 버전 안내", "최신 상태 안내", "오프라인 안내" };
        for (var i = 0; i < outcomes.Length; i++)
        {
            var outcome = outcomes[i];
            var button = new Button { Text = titles[i], Bounds = new Rectangle(24 + i * 196, 148, 184, 42) };
            button.Click += delegate
            {
                var type = Product.GetType("DexManager.Forms.UpdateCheckDialog");
                Func<Task<UpdateCheckResult>> check = () => Task.FromResult(
                    new UpdateCheckResult(outcome, outcome == UpdateCheckOutcome.Available ? release : null));
                using (var dialog = (Form)Activator.CreateInstance(type, Hidden, null,
                    new object[] { "2.0.0", check }, null)) dialog.ShowDialog(host);
            };
            host.Controls.Add(button);
        }
        Application.Run(host);
    }
}
