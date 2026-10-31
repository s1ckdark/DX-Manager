using System;
using System.Collections.Generic;
using System.Linq;
using DexManager.Models;

namespace DexManager.Services
{
    internal static class WirelessMdnsResolver
    {
        private const string ConnectService = "_adb-tls-connect._tcp";

        internal static AdbDeviceInfo FindVerifiedConnectedDevice(
            string identity, DeviceRegistrySnapshot snapshot,
            IList<AdbDeviceInfo> liveDevices, Func<string, string> readIdentity)
        {
            if (string.IsNullOrWhiteSpace(identity) || readIdentity == null) return null;
            var live = (liveDevices ?? new List<AdbDeviceInfo>())
                .Where(device => device != null && device.IsAuthorized).ToList();
            var known = snapshot == null ? null : snapshot.FindByIdentity(identity);
            var preferred = known == null || known.Transports == null
                ? new string[0] : known.Transports.Where(t => t != null).Select(t => t.Serial).ToArray();
            // A registry snapshot can outlive a disconnect or IP reassignment.
            // It orders candidates, but never proves current ownership.
            foreach (var device in live.OrderBy(d => Array.IndexOf(preferred, d.Serial) >= 0 ? 0 : 1))
            {
                if (string.Equals(identity, readIdentity(device.Serial), StringComparison.OrdinalIgnoreCase))
                    return device;
            }
            return null;
        }

        internal static bool CanRefreshProfile(DeviceWirelessConnectionProfile profile,
            string expectedHost, int expectedPort)
        {
            return profile != null && profile.Mode == AdbConnectionMode.Wireless &&
                profile.WirelessPort == expectedPort && string.Equals(
                    NormalizeHost(profile.WirelessHost), NormalizeHost(expectedHost),
                    StringComparison.OrdinalIgnoreCase);
        }

        internal static bool CanRefreshConnection(ConnectionSettings connection,
            string expectedHost, int expectedPort)
        {
            return connection != null && connection.Mode == AdbConnectionMode.Wireless &&
                connection.WirelessPort == expectedPort && string.Equals(
                    NormalizeHost(connection.WirelessHost), NormalizeHost(expectedHost),
                    StringComparison.OrdinalIgnoreCase);
        }

        internal static AdbMdnsServiceRecord ResolveEndpointForIdentity(
            string deviceIdentity,
            string savedEndpoint,
            IEnumerable<AdbMdnsServiceRecord> services)
        {
            var serial = ExtractSerialIdentity(deviceIdentity);
            if (serial.Length == 0) return null;

            var prefix = "adb-" + serial;
            var candidates = UniqueServices((services ??
                Enumerable.Empty<AdbMdnsServiceRecord>())
                .Where(service => IsConnectService(service) &&
                    IsInstanceForIdentity(service.InstanceName, prefix)));
            if (candidates.Count == 1) return candidates[0];
            if (candidates.Count < 2) return null;

            var savedHost = ParseHost(savedEndpoint);
            if (savedHost.Length == 0) return null;
            var sameHost = candidates.Where(service => string.Equals(
                service.Host,
                savedHost,
                StringComparison.OrdinalIgnoreCase)).ToArray();
            return sameHost.Length == 1 ? sameHost[0] : null;
        }

        internal static AdbMdnsServiceRecord ResolveEndpointForPairingHost(
            string pairingHost,
            IEnumerable<AdbMdnsServiceRecord> services)
        {
            var host = NormalizeHost(pairingHost);
            if (host.Length == 0) return null;
            var candidates = UniqueServices((services ??
                Enumerable.Empty<AdbMdnsServiceRecord>())
                .Where(service => IsConnectService(service) &&
                    string.Equals(
                        NormalizeHost(service.Host),
                        host,
                        StringComparison.OrdinalIgnoreCase)));
            return candidates.Count == 1 ? candidates[0] : null;
        }

        internal static AdbMdnsServiceRecord ResolveConnectedService(
            string connectedSerial,
            IEnumerable<AdbMdnsServiceRecord> services)
        {
            var serial = (connectedSerial ?? string.Empty).Trim().TrimEnd('.');
            if (!serial.EndsWith(
                "." + ConnectService,
                StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var matches = UniqueServices((services ??
                Enumerable.Empty<AdbMdnsServiceRecord>())
                .Where(IsConnectService)
                .Where(service => string.Equals(
                    BuildMdnsSerial(service),
                    serial,
                    StringComparison.OrdinalIgnoreCase)));
            return matches.Count == 1 ? matches[0] : null;
        }

        internal static string BuildMdnsSerial(
            AdbMdnsServiceRecord service)
        {
            if (service == null) return string.Empty;
            return service.InstanceName.Trim().TrimEnd('.') + "." +
                service.ServiceType.Trim().TrimEnd('.');
        }

        internal static string BuildEndpoint(
            AdbMdnsServiceRecord service)
        {
            if (service == null || string.IsNullOrWhiteSpace(service.Host) ||
                service.Port < 1 || service.Port > 65535)
            {
                return string.Empty;
            }
            var host = NormalizeHost(service.Host);
            return host.IndexOf(':') >= 0
                ? "[" + host + "]:" + service.Port
                : host + ":" + service.Port;
        }

        private static List<AdbMdnsServiceRecord> UniqueServices(
            IEnumerable<AdbMdnsServiceRecord> services)
        {
            var result = new List<AdbMdnsServiceRecord>();
            var endpoints = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var service in services)
            {
                var endpoint = BuildEndpoint(service);
                if (endpoint.Length > 0 && endpoints.Add(endpoint))
                    result.Add(service);
            }
            return result;
        }

        private static bool IsConnectService(AdbMdnsServiceRecord service)
        {
            return service != null && string.Equals(
                service.ServiceType.Trim().TrimEnd('.'),
                ConnectService,
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsInstanceForIdentity(
            string instanceName,
            string prefix)
        {
            var instance = (instanceName ?? string.Empty).Trim().TrimEnd('.');
            return string.Equals(
                    instance,
                    prefix,
                    StringComparison.OrdinalIgnoreCase) ||
                instance.StartsWith(
                    prefix + "-",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtractSerialIdentity(string identity)
        {
            var value = (identity ?? string.Empty).Trim();
            const string prefix = "serial:";
            return value.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase)
                ? value.Substring(prefix.Length).Trim()
                : string.Empty;
        }

        private static string ParseHost(string endpoint)
        {
            var value = (endpoint ?? string.Empty).Trim();
            if (value.StartsWith("[", StringComparison.Ordinal))
            {
                var end = value.IndexOf("]:", StringComparison.Ordinal);
                return end > 1 ? value.Substring(1, end - 1) : string.Empty;
            }
            var separator = value.LastIndexOf(':');
            return separator > 0
                ? value.Substring(0, separator)
                : string.Empty;
        }

        private static string NormalizeHost(string host)
        {
            var value = (host ?? string.Empty).Trim();
            return value.StartsWith("[", StringComparison.Ordinal) &&
                value.EndsWith("]", StringComparison.Ordinal)
                ? value.Substring(1, value.Length - 2)
                : value;
        }
    }
}
