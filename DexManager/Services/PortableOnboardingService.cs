using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace DexManager.Services
{
    public sealed class PortableOnboardingService
    {
        public const int CurrentNoticeVersion = 2;
        // Final release: respect the saved acknowledgement across launches.
        public const bool RepeatNoticeForUiReview = false;
        private readonly string _path;
        [DataContract] private sealed class State
        {
            [DataMember] public int FirstRunNoticeVersion { get; set; }
        }
        public PortableOnboardingService(string directory) { _path = Path.Combine(directory, "onboarding.json"); }
        public bool HasAcceptedAnyFirstRunNotice
        {
            get
            {
                try
                {
                    using (var stream = File.OpenRead(_path))
                    {
                        var state = (State)new DataContractJsonSerializer(typeof(State)).ReadObject(stream);
                        return state != null && state.FirstRunNoticeVersion > 0;
                    }
                }
                catch (IOException) { return false; }
                catch (SerializationException) { return false; }
                catch (UnauthorizedAccessException) { return false; }
            }
        }
        public bool ShouldShowNoticeForLaunch() { return RepeatNoticeForUiReview || RequiresNotice(); }
        public bool RequiresNotice()
        {
            try
            {
                using (var stream = File.OpenRead(_path))
                    return ((State)new DataContractJsonSerializer(typeof(State)).ReadObject(stream)).FirstRunNoticeVersion < CurrentNoticeVersion;
            }
            catch (IOException) { return true; }
            catch (SerializationException) { return true; }
            catch (UnauthorizedAccessException) { return true; }
        }
        public bool TryAccept(out string error)
        {
            error = null;
            var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                using (var stream = File.Create(temporary))
                    new DataContractJsonSerializer(typeof(State)).WriteObject(stream, new State { FirstRunNoticeVersion = CurrentNoticeVersion });
                if (File.Exists(_path)) File.Replace(temporary, _path, null);
                else File.Move(temporary, _path);
                return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }
    }
}
