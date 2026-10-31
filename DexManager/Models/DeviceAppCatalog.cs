using System;
using System.Collections.Generic;

namespace DexManager.Models
{
    // UI-thread-owned catalogue. A delayed response belongs to the phone that
    // was queried, even when another phone is now selected.
    public sealed class DeviceAppCatalog
    {
        private readonly Dictionary<string, List<ScrcpyAppInfo>> _catalogues =
            new Dictionary<string, List<ScrcpyAppInfo>>(StringComparer.OrdinalIgnoreCase);

        public int Replace(string identity, string serial, IEnumerable<ScrcpyAppInfo> apps)
        {
            var key = GetKey(identity, serial);
            if (key.Length == 0) throw new ArgumentException("App catalogue has no device target.");
            var catalogue = new List<ScrcpyAppInfo>();
            var packages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var app in apps ?? new ScrcpyAppInfo[0])
            {
                if (app == null || string.IsNullOrWhiteSpace(app.PackageName)) continue;
                var copy = Copy(app);
                if (packages.Add(copy.PackageName)) catalogue.Add(copy);
            }
            _catalogues[key] = catalogue;
            return catalogue.Count;
        }

        public List<ScrcpyAppInfo> CreateItems(
            string identity, string serial, IEnumerable<RememberedAppSettings> recent,
            string selectedPackage, string selectedName, string noAppText)
        {
            var items = new List<ScrcpyAppInfo>
            {
                new ScrcpyAppInfo { Name = noAppText, PackageName = string.Empty }
            };
            var packages = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { string.Empty };
            List<ScrcpyAppInfo> catalogue;
            var loaded = _catalogues.TryGetValue(GetKey(identity, serial), out catalogue);
            foreach (var app in catalogue ?? new List<ScrcpyAppInfo>())
            {
                items.Add(Copy(app));
                packages.Add(app.PackageName);
            }
            foreach (var app in recent ?? new RememberedAppSettings[0])
            {
                if (app == null || string.IsNullOrWhiteSpace(app.PackageName)) continue;
                var package = app.PackageName.Trim();
                // Once queried, an uninstalled recent app is not a catalogue entry.
                if (!loaded && packages.Add(package))
                    items.Add(Copy(new ScrcpyAppInfo { Name = app.Name, PackageName = package }));
            }
            selectedPackage = (selectedPackage ?? string.Empty).Trim();
            if (packages.Add(selectedPackage))
                items.Add(Copy(new ScrcpyAppInfo { Name = selectedName, PackageName = selectedPackage }));
            return items;
        }

        private static string GetKey(string identity, string serial)
        {
            identity = (identity ?? string.Empty).Trim();
            if (identity.Length > 0) return "identity:" + identity;
            serial = (serial ?? string.Empty).Trim();
            return serial.Length == 0 ? string.Empty : "transport:" + serial;
        }

        private static ScrcpyAppInfo Copy(ScrcpyAppInfo app)
        {
            var package = (app.PackageName ?? string.Empty).Trim();
            return new ScrcpyAppInfo
            {
                Name = string.IsNullOrWhiteSpace(app.Name) ? package : app.Name.Trim(),
                PackageName = package,
                IsSystemApp = app.IsSystemApp
            };
        }
    }
}
