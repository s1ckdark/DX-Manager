using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using DexManager.Models;

namespace DexManager.Services
{
    internal static class AdbMdnsOutputParser
    {
        internal static IList<AdbMdnsServiceRecord> Parse(string output)
        {
            var services = new List<AdbMdnsServiceRecord>();
            var lines = (output ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n');
            foreach (var rawLine in lines)
            {
                var line = (rawLine ?? string.Empty).Trim();
                if (line.Length == 0 || line.StartsWith(
                    "List of discovered mdns services",
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var columns = line.Split(
                    new[] { ' ', '\t' },
                    StringSplitOptions.RemoveEmptyEntries);
                if (columns.Length < 3) continue;

                string host;
                int port;
                if (!TrySplitEndpoint(
                    columns[columns.Length - 1],
                    out host,
                    out port) || !IsUsableHost(host))
                {
                    continue;
                }

                var serviceType = columns[columns.Length - 2].TrimEnd('.');
                var instanceName = string.Join(
                    " ",
                    columns.Take(columns.Length - 2));
                if (instanceName.Length == 0 || serviceType.Length == 0)
                    continue;

                services.Add(new AdbMdnsServiceRecord(
                    instanceName,
                    serviceType,
                    host,
                    port));
            }
            return services;
        }

        private static bool TrySplitEndpoint(
            string value,
            out string host,
            out int port)
        {
            host = string.Empty;
            port = 0;
            var endpoint = (value ?? string.Empty).Trim();
            if (endpoint.StartsWith("[", StringComparison.Ordinal))
            {
                var closing = endpoint.IndexOf("]:", StringComparison.Ordinal);
                if (closing <= 1 || !TryParsePort(
                    endpoint.Substring(closing + 2),
                    out port))
                {
                    return false;
                }
                host = endpoint.Substring(1, closing - 1);
            }
            else
            {
                var separator = endpoint.LastIndexOf(':');
                if (separator <= 0 || !TryParsePort(
                    endpoint.Substring(separator + 1),
                    out port))
                {
                    return false;
                }
                host = endpoint.Substring(0, separator);
            }
            return host.Length > 0 && port >= 1 && port <= 65535;
        }

        private static bool TryParsePort(string value, out int port)
        {
            return int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out port);
        }

        private static bool IsUsableHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host) ||
                !Regex.IsMatch(host, @"^[A-Za-z0-9._:%-]+$")) return false;
            if (string.Equals(host, "0", StringComparison.Ordinal))
                return false;
            IPAddress address;
            return !IPAddress.TryParse(host, out address) ||
                (!address.Equals(IPAddress.Any) &&
                 !address.Equals(IPAddress.IPv6Any));
        }
    }
}
