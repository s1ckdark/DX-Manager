namespace DexManager.Models
{
    /// <summary>
    /// <c>dumpsys window</c> 파싱으로 얻은 기기 잠금 상태.
    /// </summary>
    /// <remarks>
    /// <see cref="Unknown"/>은 "잠기지 않음"과 다르다 — 알려진 필드를
    /// 하나도 찾지 못했다는 뜻이다. 이 값 자체는 fail-open 정책을 담지
    /// 않는다: 호출자가 <see cref="Locked"/>일 때만 막고, <see
    /// cref="Unknown"/>과 <see cref="Unlocked"/>는 똑같이 통과시켜야 한다.
    /// </remarks>
    public enum LockState
    {
        Unknown = 0,
        Locked = 1,
        Unlocked = 2
    }
}
