using System;
using System.Collections.Generic;

namespace DexManager.Services
{
    /// <summary>
    /// 물리 기기 identity 하나에 <see cref="DeviceRuntimeServiceSet"/> 하나를
    /// 보장한다. 팩토리는 부를 때마다 새 세트를 만들므로, 중복 생성을 막는
    /// 책임이 호출자에게 있다 — 그 책임을 한 곳에 모은다.
    /// </summary>
    /// <remarks>
    /// 키가 serial이 아니라 identity인 이유: 연결 방식이 USB에서 무선으로
    /// 바뀌면 serial이 달라지지만 물리 기기와 서비스 instance ID는 유지된다.
    /// serial로 키를 잡으면 전환 때마다 런타임이 하나씩 더 생긴다.
    /// </remarks>
    public sealed class DeviceRuntimeCoordinator
    {
        private readonly DeviceRuntimeServiceFactory _factory;
        private readonly DeviceRuntimeSessionRegistry _sessions;
        private readonly object _sync = new object();

        private readonly Dictionary<string, DeviceRuntimeServiceSet> _byIdentity =
            new Dictionary<string, DeviceRuntimeServiceSet>(
                StringComparer.OrdinalIgnoreCase);

        public DeviceRuntimeCoordinator(
            DeviceRuntimeServiceFactory factory,
            DeviceRuntimeSessionRegistry sessions)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        }

        /// <summary>
        /// 이 identity의 런타임을 돌려준다. 없으면 만든다.
        /// <paramref name="serial"/>이 비어 있지 않으면 레지스트리에
        /// 기기↔인스턴스 결속을 갱신한다 — transport가 바뀌어도 같은
        /// 런타임이 새 serial로 다시 결속된다.
        /// </summary>
        public DeviceRuntimeServiceSet GetOrCreate(string identity, string serial)
        {
            if (string.IsNullOrWhiteSpace(identity))
            {
                throw new ArgumentException(
                    "A physical device identity is required.",
                    nameof(identity));
            }

            DeviceRuntimeServiceSet runtime;
            lock (_sync)
            {
                if (!_byIdentity.TryGetValue(identity, out runtime))
                {
                    runtime = _factory.Create();
                    _byIdentity.Add(identity, runtime);
                }
            }

            if (!string.IsNullOrWhiteSpace(serial))
            {
                _sessions.BindServiceInstance(serial, runtime.InstanceId);
            }

            return runtime;
        }

        /// <summary>
        /// 이 identity의 런타임이 이미 있으면 돌려준다. 만들지 않는다.
        /// 아직 아무것도 시작하지 않은 기기에 중지 명령이 들어왔을 때
        /// 빈 런타임을 만들지 않기 위해 쓴다.
        /// </summary>
        public bool TryGet(string identity, out DeviceRuntimeServiceSet runtime)
        {
            runtime = null;
            if (string.IsNullOrWhiteSpace(identity)) return false;
            lock (_sync) return _byIdentity.TryGetValue(identity, out runtime);
        }
    }
}
