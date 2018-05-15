using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using LibSpeedLoad.Core.Download;
using LibSpeedLoad.Core.Exceptions;
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

        private readonly HashManager _hashManager;

        /**
         * Constructs the source.
         */
        public PatchCDNSource(PatchDownloadOptions downloadOptions)
        {
            _downloadOptions = downloadOptions ?? throw new ArgumentNullException(nameof(downloadOptions));
            _hashManager = new HashManager("sbrw");
        }

        public override Task Download()
        {
            _hashManager.Reset();
            
            return Task.Run(async () =>
            {
                var patch = await FetchPatch();

                var fileList = await FetchPatchManifest(patch.PatchId);
                var rootUrl = string.Format(PatchRootFormat, patch.PatchId);

                fileList.Files = fileList.Files.Select(f =>
                {
                    f.FullPath = Path.Combine(_downloadOptions.GameDirectory, f.Path);

                    return f;
                }).ToList();
                
                await _downloader.StartDownload(rootUrl, fileList.Files);
                
                foreach (var file in fileList.Files)
                {
                    _hashManager.Put(file.FullPath, file.Hash256);
                }
            });
        }

        public override Task VerifyHashes()
        {
            return Task.Run(async () =>
            {
                var patch = await FetchPatch();
                var fileList = await FetchPatchManifest(patch.PatchId);

                foreach (var file in fileList.Files)
                {
                    var fullPath = Path.Combine(_downloadOptions.GameDirectory, file.Path, file.Name);
                    
                }
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

            return JsonConvert.DeserializeObject<FileList>(data);
        }
    }
}