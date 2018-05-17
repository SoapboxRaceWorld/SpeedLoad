using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using LibSpeedLoad.Core.Download.Sources;
using LibSpeedLoad.Core.Exceptions;

namespace SpeedLoadCli.CustomSources.PatchCDN
{
    public class PatchCDNDownloader : Downloader<FileInfo>
    {
        private readonly HttpClient _client = new HttpClient();

        public override Task StartDownload(string url, IEnumerable<FileInfo> files)
        {
            return Task.Run(async () =>
            {
                var fileInfos = files as FileInfo[] ?? files.ToArray();

                foreach (var file in fileInfos)
                {
                    if (File.Exists(file.FullPath))
                    {
                        continue;
                    }

                    var webPath = $"{url}/{(file.Path == "" ? "" : file.Path)}{file.Name}";
                    var response = await _client.GetAsync(webPath);
                    
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        throw new DownloaderWebException(
                            $"Failed to retrieve file {webPath}: {response.StatusCode.ToString()}");
                    }

                    if (!File.Exists(file.FullPath))
                    {
                        Directory.CreateDirectory(file.FullPath);
                    }

                    var fileData = await response.Content.ReadAsByteArrayAsync();

                    using (var outStream = new FileStream(Path.Combine(file.FullPath, file.Name), FileMode.Create))
                    {
                        outStream.Write(fileData, 0, fileData.Length);
                    }

                }
            });
        }
    }
}