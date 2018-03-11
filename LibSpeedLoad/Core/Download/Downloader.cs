using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using LibSpeedLoad.Core.Utils;

namespace LibSpeedLoad.Core.Download
{
    /**
     * Handles downloading and decompressing files.
     */
    public class Downloader
    {
        private readonly HttpClient _client = new HttpClient();
        private readonly IntPtr _propsSizePtr = new IntPtr(5);

        private readonly ConcurrentDictionary<string, byte[]> _dataMap = new ConcurrentDictionary<string, byte[]>();
        
        /**
         * Download a section and extract the files
         */
        public Task StartDownload(string sectionUrl, List<DownloadManager.FileInfo> files)
        {
            return Task.Run(async () =>
            {
                var response = await _client.GetAsync(sectionUrl);
                
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new SpeedLoadException($"Section retrieval failed with code {response.StatusCode.ToString()}");
                }
                
                var fileData = await response.Content.ReadAsByteArrayAsync();
                var inputReader = new BinaryReader(new MemoryStream(fileData));
                var tmpData = new byte[fileData.Length];

                fileData.CopyTo(tmpData, 0);

                foreach (var fileInfo in files)
                {
                    if (File.Exists(fileInfo.FullPath))
                    {
                        Console.WriteLine($"Skipping {fileInfo.FullPath} because it's already downloaded");
                        continue;
                    }

                    _dataMap[fileInfo.FullPath] = new byte[fileInfo.Length];
                        //                    _dataMap.Add(fileInfo.FullPath, new byte[fileInfo.Length]);

                    if (fileInfo.CompressedLength != -1)
                    {
                        Console.WriteLine(
                            $"[ compressed: {sectionUrl} ] {fileInfo.FullPath} @ {fileInfo.Offset} ({fileInfo.CompressedLength} bytes)");

                        inputReader.BaseStream.Position = fileInfo.Offset;

                        Console.WriteLine(
                            $"[ compressed:{sectionUrl} ] {fileInfo.FullPath} -----> {fileInfo.Length} bytes uncompressed");

                        Console.WriteLine($"[compressed] create {fileInfo.Path} –– {fileInfo.FullPath}");
                        Directory.CreateDirectory(fileInfo.Path);

                        var destLen = new IntPtr(fileInfo.Length);
                        var srcLen = new IntPtr(fileInfo.CompressedLength - 13);

                        var props = inputReader.ReadBytes(5);
                        inputReader.BaseStream.Seek(8, SeekOrigin.Current);
                        inputReader.Read(_dataMap[fileInfo.FullPath], 0, _dataMap[fileInfo.FullPath].Length);
//                        var compressedInput = inputReader.ReadBytes(fileInfo.CompressedLength - 13);
                        var decompressedOutput = new byte[destLen.ToInt32()];

                        LZMA.AutoUncompress(ref decompressedOutput, ref destLen, _dataMap[fileInfo.FullPath], ref srcLen, props,
                            _propsSizePtr);
                        Console.WriteLine("Decompressed");

                        using (var outStream = new FileStream(fileInfo.FullPath, FileMode.Create))
                        {
                            outStream.Write(decompressedOutput, 0, decompressedOutput.Length);
                        }
                        
                        decompressedOutput = null;

                        props = null;
                    }
                    else
                    {
                        inputReader.BaseStream.Position = fileInfo.Offset;
                        inputReader.Read(_dataMap[fileInfo.FullPath], 0, _dataMap[fileInfo.FullPath].Length);

                        Console.WriteLine($"[uncompressed] create {fileInfo.Path} / {fileInfo.FullPath}");
                        Directory.CreateDirectory(fileInfo.Path);

                        using (var fileStream = new FileStream(fileInfo.FullPath, FileMode.Create))
                        {
                            fileStream.Write(_dataMap[fileInfo.FullPath], 0, _dataMap[fileInfo.FullPath].Length);
                        }
                    }
                }
            });
        }
    }
}