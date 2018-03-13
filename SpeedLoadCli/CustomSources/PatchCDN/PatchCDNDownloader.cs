using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using LibSpeedLoad.Core.Download.Sources;
using LibSpeedLoad.Core.Exceptions;
using LibSpeedLoad.Core.Utils;

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
                    if (File.Exists(file.Path))
                    {
                        Console.WriteLine($"Skipping {file.Name}");
                        continue;
                    }

                    Console.WriteLine($"download {file.Name} to {file.Path}");

                    var fileUrl = $"{url}/{file.Name}";
                    var response = await _client.GetAsync(fileUrl);

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        throw new DownloaderWebException(
                            $"Failed to retrieve file {url}: {response.StatusCode.ToString()}");
                    }

                    var fileData = await response.Content.ReadAsByteArrayAsync();

                    using (var outStream = new FileStream(file.Path, FileMode.Create))
                    {
                        outStream.Write(fileData, 0, fileData.Length);
                    }
                }

                Parallel.ForEach(fileInfos, fileInfo =>
                {
                    using (var sha256 = SHA256.Create())
                    using (var inStream = File.OpenRead(fileInfo.Path))
                    {
                        var calcHash = DebugUtil.Sha256ToString(sha256.ComputeHash(inStream));
                        
                        DebugUtil.EnsureCondition(
                            calcHash == fileInfo.Hash,
                            () => $"Hash for {fileInfo.Name} is invalid! Expected {fileInfo.Hash}, got {calcHash}");
                    }
                });
            });
        }
    }
}