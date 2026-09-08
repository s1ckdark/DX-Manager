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

    public event PropertyChangedEventHandler PropertyChanged;
}
