using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;
using DexManager.Models;

namespace DexManager.Services
{
    public interface IReleaseUpdateSource
    {
        Task<ReleaseUpdate> FetchLatestAsync(CancellationToken cancellation);
    }

    public sealed class ReleaseCheckBusyException : Exception { }

    public sealed class GitHubReleaseSource : IReleaseUpdateSource
    {
        public const string Endpoint =
            "https://api.github.com/repos/maze-mei/DX-Manager/releases/latest";
        public const int MaximumResponseBytes = 256 * 1024;
        private int _requestInProgress;

        [DataContract]
        private sealed class ReleaseResponse
        {
            [DataMember(Name = "tag_name")] public string Tag { get; set; }
            [DataMember(Name = "html_url")] public string Url { get; set; }
            [DataMember(Name = "draft")] public bool Draft { get; set; }
            [DataMember(Name = "prerelease")] public bool Prerelease { get; set; }
        }

        public static ReleaseUpdate ParseResponse(Stream stream)
        {
            using (var bounded = new MemoryStream())
            {
                var buffer = new byte[4096];
                int count;
                while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (bounded.Length + count > MaximumResponseBytes)
                        throw new InvalidDataException("GitHub release response is too large.");
                    bounded.Write(buffer, 0, count);
                }
                bounded.Position = 0;
                var response = (ReleaseResponse)new DataContractJsonSerializer(
                    typeof(ReleaseResponse)).ReadObject(bounded);
                if (response == null || response.Draft || response.Prerelease) return null;
                ReleaseUpdate update;
                if (!ReleaseUpdate.TryCreate(response.Tag, response.Url, out update))
                    throw new InvalidDataException("Invalid GitHub release metadata.");
                return update;
            }
        }

        public Task<ReleaseUpdate> FetchLatestAsync(CancellationToken cancellation)
        {
            if (Interlocked.CompareExchange(ref _requestInProgress, 1, 0) != 0)
                throw new ReleaseCheckBusyException();
            // Keep at most one physical request alive even if DNS cannot abort promptly.
            // UpdateCheckService enforces the separate overall UI-visible deadline.
            return Task.Run(delegate
            {
                try
                {
                    cancellation.ThrowIfCancellationRequested();
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                    var request = (HttpWebRequest)WebRequest.Create(Endpoint);
                    request.Method = "GET";
                    request.UserAgent = "DX-Manager/2.5.0";
                    request.Accept = "application/vnd.github+json";
                    request.Headers["X-GitHub-Api-Version"] = "2026-03-10";
                    request.AllowAutoRedirect = false;
                    request.Timeout = 5000;
                    request.ReadWriteTimeout = 5000;
                    request.AutomaticDecompression =
                        DecompressionMethods.GZip | DecompressionMethods.Deflate;
                    using (cancellation.Register(delegate { request.Abort(); }))
                    using (var response = (HttpWebResponse)request.GetResponse())
                    {
                        if (response.StatusCode != HttpStatusCode.OK ||
                            response.ContentLength > MaximumResponseBytes)
                            throw new InvalidDataException("Unexpected GitHub release response.");
                        using (var stream = response.GetResponseStream())
                            return ParseResponse(stream);
                    }
                }
                finally { Interlocked.Exchange(ref _requestInProgress, 0); }
            });
        }
    }
}
