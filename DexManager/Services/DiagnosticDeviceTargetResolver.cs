using System;
using DexManager.Models;

namespace DexManager.Services
{
    internal static class DiagnosticDeviceTargetResolver
    {
        internal static string Resolve(
            PhysicalDeviceInfo device,
            DeviceRuntimeSessionSnapshot runtime,
            bool selected,
            string selectedSerial)
        {
            if (device == null || !device.IsConnected)
                return string.Empty;

            if (selected)
            {
                var selectedTransport = device.FindTransport(selectedSerial);
                if (selectedTransport != null &&
                    selectedTransport.IsAuthorized)
                {
                    return selectedTransport.Serial;
                }
            }

            if (runtime == null || !runtime.IsConnected)
                return string.Empty;

            var activeTransport = device.FindTransport(
                runtime.ActiveTransportSerial);
            return activeTransport != null && activeTransport.IsAuthorized
                ? activeTransport.Serial
                : string.Empty;
        }
    }
}
