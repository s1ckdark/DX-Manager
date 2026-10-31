using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace DexManager.Services
{
    [DataContract]
    public sealed class UpdateCheckState
    {
        [DataMember(Order = 1)] public string LastAttemptDay { get; set; }
        [DataMember(Order = 2)] public string LastNotifiedVersion { get; set; }
        [DataMember(Order = 3)] public string DismissedVersion { get; set; }
        [DataMember(Order = 4)] public string KnownVersion { get; set; }
        [DataMember(Order = 5)] public string KnownReleaseUrl { get; set; }

        public UpdateCheckState Copy()
        {
            return (UpdateCheckState)MemberwiseClone();
        }
    }

    public interface IUpdateCheckStateStore
    {
        UpdateCheckState Load();
        bool TrySave(UpdateCheckState state);
    }

    public sealed class FileUpdateCheckStateStore : IUpdateCheckStateStore
    {
        private readonly string _path;
        private readonly Action<string> _warning;
        private readonly object _sync = new object();
        private bool _readFailed;

        public FileUpdateCheckStateStore(string path, Action<string> warning)
        {
            _path = path;
            _warning = warning ?? delegate { };
        }

        public UpdateCheckState Load()
        {
            lock (_sync)
            {
                if (!File.Exists(_path)) return new UpdateCheckState();
                try
                {
                    if (new FileInfo(_path).Length > 32768)
                        throw new InvalidDataException("Update state is too large.");
                    using (var input = File.OpenRead(_path))
                        return (UpdateCheckState)new DataContractJsonSerializer(
                            typeof(UpdateCheckState)).ReadObject(input) ?? new UpdateCheckState();
                }
                catch
                {
                    _readFailed = true;
                    _warning("Update state could not be read; automatic checking is skipped.");
                    return new UpdateCheckState();
                }
            }
        }

        public bool TrySave(UpdateCheckState state)
        {
            lock (_sync)
            {
                if (_readFailed) return false;
                var temporary = _path + ".tmp";
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_path));
                    using (var stream = new FileStream(temporary, FileMode.Create,
                        FileAccess.Write, FileShare.None))
                    {
                        new DataContractJsonSerializer(typeof(UpdateCheckState))
                            .WriteObject(stream, state);
                        stream.Flush(true);
                    }
                    if (File.Exists(_path)) File.Replace(temporary, _path, null);
                    else File.Move(temporary, _path);
                    return true;
                }
                catch
                {
                    _warning("Update check state could not be saved.");
                    try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
                    return false;
                }
            }
        }
    }
}
