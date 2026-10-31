using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace DexManager.Services
{
    // Informational, local release notes. Independent of remote update checks and safety consent.
    public sealed class PortableUpdateNoticeService
    {
        private readonly Version _current;
        public string StatePath { get; private set; }
        [DataContract] private sealed class State
        {
            [DataMember] public string LastSeenVersion { get; set; }
        }

        public PortableUpdateNoticeService(string directory, Version current = null)
        {
            StatePath = Path.Combine(Path.GetFullPath(directory), "update-notice.json");
            _current = Normalize(current ?? typeof(PortableUpdateNoticeService).Assembly.GetName().Version);
        }

        public bool ShouldShowNotice(bool existingInstallation)
        {
            var previous = ReadVersion();
            return previous == null ? existingInstallation : _current > previous;
        }

        public bool TryMarkCurrentVersionSeen(out string error)
        {
            error = null;
            var previous = ReadVersion();
            if (previous != null && previous >= _current) return true;
            var temporary = StatePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StatePath));
                using (var stream = File.Create(temporary))
                    new DataContractJsonSerializer(typeof(State)).WriteObject(stream,
                        new State { LastSeenVersion = _current.ToString() });
                if (File.Exists(StatePath)) File.Replace(temporary, StatePath, null);
                else File.Move(temporary, StatePath);
                return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }

        private Version ReadVersion()
        {
            try
            {
                using (var stream = File.OpenRead(StatePath))
                {
                    var state = (State)new DataContractJsonSerializer(typeof(State)).ReadObject(stream);
                    Version value;
                    return state != null && Version.TryParse(state.LastSeenVersion, out value) ? Normalize(value) : null;
                }
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (SerializationException) { return null; }
        }

        private static Version Normalize(Version value)
        {
            return new Version(value.Major, value.Minor, Math.Max(0, value.Build), Math.Max(0, value.Revision));
        }
    }
}
