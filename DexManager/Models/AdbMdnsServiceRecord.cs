namespace DexManager.Models
{
    public sealed class AdbMdnsServiceRecord
    {
        public AdbMdnsServiceRecord(
            string instanceName,
            string serviceType,
            string host,
            int port)
        {
            InstanceName = instanceName ?? string.Empty;
            ServiceType = serviceType ?? string.Empty;
            Host = host ?? string.Empty;
            Port = port;
        }

        public string InstanceName { get; private set; }
        public string ServiceType { get; private set; }
        public string Host { get; private set; }
        public int Port { get; private set; }
    }
}
