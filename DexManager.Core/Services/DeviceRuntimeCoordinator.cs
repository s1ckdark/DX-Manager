using System;
using System.Collections.Generic;

namespace DexManager.Services
{
    /// <summary>
    /// 코디네이터가 런타임 하나에 대해 기억하고 있는 결속 정보.
    /// 정리 경로가 "이 런타임은 어느 기기 것인가"를 되묻는 데 쓴다.
    /// </summary>
    public sealed class DeviceRuntimeBinding
    {
        internal DeviceRuntimeBinding(string identity, string serial)
        {
            Identity = identity ?? string.Empty;
            Serial = serial ?? string.Empty;
        }

        /// <summary>
        /// 이 런타임을 소유한 물리 기기 identity. 비어 있지 않다.
        /// </summary>
        public string Identity { get; private set; }

        /// <summary>
        /// 마지막으로 결속된 transport serial. 아직 한 번도 serial과 함께
        /// 요청된 적이 없으면 빈 문자열이다 — 그때는 identity만으로 기기를
        /// 다시 찾아야 한다.
        /// </summary>
        public string Serial { get; private set; }
    }

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

        // instance ID → 결속. _byIdentity의 역방향이다.
        // DeviceRuntimeServiceFactory.CreatedInstances는 런타임 목록만 주고
        // identity 결속을 잃어버리므로, 정리 경로가 런타임마다 자기 기기를
        // 되찾으려면 이 조회가 필요하다. 객체 비교 대신 instance ID를 키로
        // 잡아 사전 조회 한 번으로 끝낸다.
        private readonly Dictionary<Guid, DeviceRuntimeBinding> _byInstance =
            new Dictionary<Guid, DeviceRuntimeBinding>();

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
                RecordBinding(runtime.InstanceId, identity, serial);
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

        /// <summary>
        /// 이 instance ID의 런타임을 코디네이터가 알고 있으면 그 결속을
        /// 돌려준다. 만들지 않는다.
        /// </summary>
        /// <remarks>
        /// 코디네이터를 거치지 않고 팩토리에서 직접 만든 런타임(TUI가 쓰는
        /// 단일 런타임)에 대해서는 <c>false</c>를 돌려준다. 호출자는 그때
        /// 자기가 들고 있던 대체값을 그대로 써야 한다 — 그래야 TUI의 정리
        /// 동작이 지금과 똑같이 유지된다.
        /// </remarks>
        public bool TryGetBinding(
            Guid instanceId,
            out DeviceRuntimeBinding binding)
        {
            binding = null;
            if (instanceId == Guid.Empty) return false;
            lock (_sync) return _byInstance.TryGetValue(instanceId, out binding);
        }

        /// <summary>
        /// 역방향 결속을 갱신한다. 반드시 <c>_sync</c>를 잡은 채로 부른다.
        /// <paramref name="serial"/>이 비어 있으면 이전에 기억한 serial을
        /// 유지한다 — serial 없이 부른 호출이 이미 알던 결속을 지우면
        /// 안 되기 때문이다.
        /// </summary>
        private void RecordBinding(
            Guid instanceId,
            string identity,
            string serial)
        {
            var nextSerial = serial;
            if (string.IsNullOrWhiteSpace(nextSerial))
            {
                DeviceRuntimeBinding previous;
                nextSerial = _byInstance.TryGetValue(instanceId, out previous)
                    ? previous.Serial
                    : string.Empty;
            }

            _byInstance[instanceId] =
                new DeviceRuntimeBinding(identity, nextSerial);
        }
    }
}
