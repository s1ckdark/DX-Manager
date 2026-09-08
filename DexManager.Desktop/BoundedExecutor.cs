using System;
using System.Threading.Tasks;

namespace DexManager.Desktop;

/// <summary>
/// 시간 예산을 넘길 수 있는 동기 작업(주로 adb 호출과 프로세스 종료를
/// 포함하는 정리 로직)을 스레드풀에서 실행하고 예산만큼만 기다린다.
///
/// 신호 처리기(Program.cs의 PosixSignalRegistration 콜백)에서 쓰인다 -
/// 정리가 멈추면 신호 처리기도 함께 멈추고, 그러면 OS가 더 강하게
/// (SIGKILL) 끊어버릴 위험이 커진다. 예산을 넘기면 대기를 포기하고
/// <c>false</c>를 돌려주므로, 호출자는 그대로 반환해 프로세스 기본 종료가
/// 이어지게 할 수 있다 - action 자체가 백그라운드에서 계속 돌고 있어도
/// 프로세스가 죽으면 함께 사라진다.
/// </summary>
public static class BoundedExecutor
{
    /// <summary>
    /// <paramref name="action"/>을 스레드풀에서 실행한다. <paramref name="budget"/>
    /// 안에 끝나면 <c>true</c>, 넘기면 대기를 포기하고 <c>false</c>를
    /// 돌려준다(어느 쪽이든 예외를 던지지 않는다 - action이 던진 예외는
    /// 예산 안에 끝난 경우에만 그대로 전파된다).
    /// </summary>
    public static bool RunWithBudget(Action action, TimeSpan budget)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        var task = Task.Run(action);
        return task.Wait(budget);
    }
}
