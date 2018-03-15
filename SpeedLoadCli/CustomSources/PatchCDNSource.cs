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
                var indexResponse = await _client.GetAsync(BaseUrl);

                if (indexResponse.StatusCode != HttpStatusCode.OK)
                {
                    return;
                }

                var data = await indexResponse.Content.ReadAsStringAsync();
                var patch = JsonConvert.DeserializeObject<Patch>(data);

                Console.WriteLine(
                    $"Patch: {patch.ID} ({patch.PatchId}) [{patch.Branch}] - created at {patch.CreatedAt}");

                var fileList = await FetchPatchManifest(patch.PatchId);
                var rootUrl = string.Format(PatchRootFormat, patch.PatchId);

                await _downloader.StartDownload(rootUrl, fileList.Files.Select(f =>
                {
                    f.FullPath = Path.Combine(_downloadOptions.GameDirectory, f.Path);

                    return f;
                }));
            });
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