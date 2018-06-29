using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Easy.Common.Extensions;
using LibSpeedLoad.Core.Download;
using LibSpeedLoad.Core.Exceptions;
using LibSpeedLoad.Core.Utils;
using Newtonsoft.Json;
using SpeedLoadCli.CustomSources.PatchCDN;

namespace SpeedLoadCli.CustomSources
{
    /**
     * A demo source to download data from the SBRW patch CDN.
     */
    public class PatchCDNSource : DownloaderSource
    {
        private const string BaseUrl = "https://launcher.soapboxrace.world/patch";

        // {0} = patchId
        private const string PatchRootFormat = "https://launcher.soapboxrace.world/patch/{0}";
        private const string PatchManifestFormat = "https://launcher.soapboxrace.world/patch/{0}/manifest.json";

        private readonly HttpClient _client = new HttpClient();
        private readonly PatchCDNDownloader _downloader = new PatchCDNDownloader();
        private readonly PatchDownloadOptions _downloadOptions;

        /**
         * Constructs the source.
         */
        public PatchCDNSource(PatchDownloadOptions downloadOptions)
        {
            _downloadOptions = downloadOptions ?? throw new ArgumentNullException(nameof(downloadOptions));
        }

        public override Task Download()
        {
            return Task.Run(async () =>
            {
                var patch = await FetchPatch();

                var fileList = await FetchPatchManifest(patch.PatchId);
                var rootUrl = string.Format(PatchRootFormat, patch.PatchId);

                await _downloader.StartDownload(rootUrl, fileList.Files);

                fileList.Files.GroupBy(f => f.HashGroup)
                    .ForEach(group =>
                    {
                        var hm = new HashManager(group.Key);

                        hm.CreateIfMissing();
                        hm.Load();

                        foreach (var file in group)
                        {
                            hm.Put(file.FullFile, file.Hash);
                        }

                        hm.Save();
                    });
            });
        }

        public override Task VerifyHashes()
        {
            return Task.Run(async () =>
            {
                var patch = await FetchPatch();
                var fileList = await FetchPatchManifest(patch.PatchId);

                fileList.Files.GroupBy(f => f.HashGroup)
                    .ForEach(group =>
                    {
                        var hm = new HashManager(group.Key);

                        hm.Load();

                        foreach (var file in group)
                        {
                            foreach (var listener in VerificationProgressUpdated)
                            {
                                listener.Invoke(file.FullFile, file.WebPath, (uint)group.ToList().IndexOf(file) + 1, (uint)group.Count());
                            }

                            try
                            {
                                hm.Check(file.FullFile, DataUtil.ComputeHash(file.FullFile));
                            }
                            catch (IntegrityException e)
                            {
                                foreach (var listener in VerificationFailed)
                                {
                                    listener.Invoke(e.FilePath, e.ExpectedHash, e.ActualHash);
                                }
                            }
                        }
                    });
            });
        }

        private async Task<Patch> FetchPatch()
        {
            var indexResponse = await _client.GetAsync(BaseUrl);

            indexResponse.EnsureSuccessStatusCode();

            var data = await indexResponse.Content.ReadAsStringAsync();
            var patch = JsonConvert.DeserializeObject<Patch>(data);

            return patch;
        }

        private async Task<FileList> FetchPatchManifest(string id)
        {
            var manifestUrl = string.Format(PatchManifestFormat, id);
            var manifestResponse = await _client.GetAsync(manifestUrl);

            if (manifestResponse.StatusCode != HttpStatusCode.OK)
            {
                throw new DownloaderWebException(
                    $"Manifest retrieval for {id} failed with code {manifestResponse.StatusCode.ToString()} [{manifestUrl}]");
            }

            var data = await manifestResponse.Content.ReadAsStringAsync();

            var list = JsonConvert.DeserializeObject<FileList>(data);

            list.Files = list.Files.Select(f =>
            {
                f.FullPath = Path.Combine(_downloadOptions.GameDirectory, f.Path);
                f.FullFile = Path.Combine(f.FullPath, f.Name);
                f.WebPath = Path.Combine(f.Path, f.Name);

                return f;
            }).ToList();

            return list;
        }
    }
}