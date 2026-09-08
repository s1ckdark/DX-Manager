using System.ComponentModel;

namespace DexManager.ViewModels.Tests.Settings;

/// <summary>
/// 테스트용 IDeviceSelectionSource 구현. DeviceListViewModel 전체
/// (레지스트리·런타임 세션·디스패처)를 띄우지 않고 SelectedIdentity를
/// 직접 설정·변경할 수 있다.
/// </summary>
public sealed class FakeDeviceSelectionSource : IDeviceSelectionSource, INotifyPropertyChanged
{
    private string _selectedIdentity;

    public string SelectedIdentity
    {
        get => _selectedIdentity;
        set
        {
            if (_selectedIdentity == value) return;
            _selectedIdentity = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedIdentity)));
        }
    }

    /// <summary>
    /// SelectedIdentity 문자열은 그대로 둔 채 변경 통지만 올린다.
    /// DeviceListViewModel.Apply가 같은 폰의 행 인스턴스를 새로 만들어
    /// 다시 선택할 때(USB 흔들림, 전송 방식 전환) 실제로 일어나는 일이다 -
    /// SelectedIdentity 통지는 문자열이 아니라 DeviceViewModel "참조"가
    /// 바뀔 때 올라오기 때문이다.
    /// </summary>
    public void RaiseSelectedIdentityChangedWithoutChangingValue()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedIdentity)));
    }

    public event PropertyChangedEventHandler PropertyChanged;
}
