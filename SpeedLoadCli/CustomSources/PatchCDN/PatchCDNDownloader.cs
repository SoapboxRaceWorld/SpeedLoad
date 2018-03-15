using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Easy.Common.Extensions;
using LibSpeedLoad.Core.Download.Sources;
using LibSpeedLoad.Core.Exceptions;
using LibSpeedLoad.Core.Utils;

namespace SpeedLoadCli.CustomSources.PatchCDN
{
    public class PatchCDNDownloader : Downloader<FileInfo>
    {
        private readonly HttpClient _client = new HttpClient();
        private readonly Dictionary<string, string> _hashDictionary = new Dictionary<string, string>();

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

                    _hashDictionary[Path.Combine(file.FullPath, file.Name)] = file.Hash256;
                }

                Parallel.ForEach(_hashDictionary, hp =>
                {
                    using (var sha256 = SHA256.Create())
                    using (var inStream = File.OpenRead(hp.Key))
                    {
                        var calcHash = DebugUtil.Sha256ToString(sha256.ComputeHash(inStream));

                        DebugUtil.EnsureCondition(
                            calcHash == hp.Value,
                            () => $"Hash for {hp.Key} is invalid! Expected {hp.Value}, got {calcHash}");
                    }
                });
            });
        }
    }
}