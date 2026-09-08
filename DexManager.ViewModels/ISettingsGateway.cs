using DexManager.Models;

namespace DexManager.ViewModels;

/// <summary>
/// 설정 읽기·쓰기의 경계. 실제 구현은 ApplicationHost.UpdateSettings를 거쳐
/// 파일 저장과 EnsureDefaults 정규화를 유발하므로, 테스트는 stub을 넣어
/// 페이지 로직만 검증한다.
/// </summary>
public interface ISettingsGateway
{
    /// <summary>현재 설정. 편집용 사본을 만들 원본으로만 읽는다.</summary>
    AppSettings Current { get; }

    /// <summary>이 기기의 실행 프로필을 얻는다. 없으면 전역 기본에서 파생 생성.</summary>
    DeviceRunSettingsProfile GetRunProfile(string deviceIdentity);

    /// <summary>한 잠금 안에서 설정을 수정하고 저장한다.</summary>
    void Update(Action<AppSettings> mutate);
}
