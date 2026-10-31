using System.Resources;
using System.Reflection;

namespace DexManager.Services
{
    internal static class StoreUiText
    {
        private static readonly ResourceManager Resources = new ResourceManager(
            "DexManager.Resources.StoreStrings", Assembly.GetExecutingAssembly());
        public static string Get(string key, string fallback = "")
        {
            var value = Resources.GetString(key, LocalizationService.Culture);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }
    }
}
