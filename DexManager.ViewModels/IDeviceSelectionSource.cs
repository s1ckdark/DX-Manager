using System.ComponentModel;

namespace DexManager.ViewModels;

/// <summary>
/// 설정 창(<see cref="SettingsViewModel"/>)이 대상 기기의 identity를
/// 알아내는 데 필요한 최소 인터페이스. <see cref="DeviceListViewModel"/>
/// 전체(레지스트리·런타임 세션·디스패처 의존)를 끌어오지 않고
/// 선택된 기기의 identity와 그 변경 알림만 노출한다 - 테스트에서는
/// 이 인터페이스만 구현한 가벼운 stub으로 대체할 수 있다.
/// </summary>
public interface IDeviceSelectionSource : INotifyPropertyChanged
{
    /// <summary>현재 선택된 기기의 영속 식별자. 선택된 기기가 없으면 null.</summary>
    string SelectedIdentity { get; }
}
