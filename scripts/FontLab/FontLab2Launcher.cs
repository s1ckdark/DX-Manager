using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.6.2")]
internal static class FontLab2Launcher
{
    private static Process _active;
    [STAThread] private static void Main()
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        var form=new Form {Text="DX 글꼴 테스트 2",ClientSize=new Size(475,470),Font=new Font("Malgun Gothic",10F),StartPosition=FormStartPosition.CenterScreen,FormBorderStyle=FormBorderStyle.FixedSingle,MaximizeBox=false};
        form.Controls.Add(new Label {Text="A2부터 차례로 비교하세요. 처음에는 글자표가 열립니다.",Bounds=new Rectangle(14,14,445,28)});
        string[] labels={"A2 · 현재 Pretendard (비교 기준)","B2 · 현재 파일 + 작은 글자 표시 설정", "C2 · 공식 정적 Pretendard", "D2 · 공식 파일 + 작은 글자 표시 설정", "E2 · B2 파일 + GDI+ 회색 AA", "F2 · 현재 파일 + GDI+ 픽셀 보정 제외", "G2 · 맑은 고딕 (정상 기준)"};
        string[] folders={"A2-current","B2-current-smoothing","C2-official","D2-official-smoothing","E2-smoothing-gray","F2-gray-no-gridfit","G2-malgun"};
        for(int i=0;i<labels.Length;i++) {
            int index=i; var b=new Button {Text=labels[i],Bounds=new Rectangle(14,50+i*48,445,40),TextAlign=ContentAlignment.MiddleLeft};
            b.Click+=delegate {
                if(_active!=null && !_active.HasExited) {MessageBox.Show(form,"결과를 복사하고 현재 비교 창을 닫은 뒤 다음 항목을 여세요.","현재 테스트 실행 중");return;}
                string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,folders[index]);
                try {_active=Process.Start(new ProcessStartInfo {FileName=Path.Combine(folder,"DXFontLab.exe"),WorkingDirectory=folder,UseShellExecute=false});}
                catch(Exception ex) {MessageBox.Show(form,ex.Message,"실행 실패");}
            }; form.Controls.Add(b);
        }
        form.Controls.Add(new Label {Text="정상/깨짐 기록 → 결과 전체 복사 → 메모장에 붙여넣기\r\n종료 전 결과를 복사하세요. 폰 연결·글꼴 설치는 필요 없습니다.",Bounds=new Rectangle(14,394,445,64)});
        Application.Run(form);
    }
}
