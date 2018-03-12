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

        private readonly object _readLock = new object();

        /**
         * Download a section and extract the files
         */
        public async Task StartDownload(string sectionUrl, IEnumerable<object> files)
        {
//            var reader = await FetchSection(sectionUrl);
//
//            foreach (var fileInfo in files)
//            {
//                if (File.Exists(fileInfo.FullPath))
//                {
//                    Console.WriteLine($"Skipping {fileInfo.FullPath} because it's already downloaded");
//                    continue;
//                }
//
//                lock (_readLock)
//                {
//                    _dataMap[fileInfo.FullPath] = new byte[fileInfo.Length];
//                }
//
//                reader.BaseStream.Position = fileInfo.Offset;
//
//                // Is it compressed?
//                if (fileInfo.CompressedLength != -1)
//                {
//                    Console.WriteLine($"[FILE-{fileInfo.Section} {fileInfo.FullPath}]: Compressed file detected");
//
//                    // A multipart file can span multiple sections,
//                    // so we have to keep track of how much has been read. 
//                    // This code is kind of irritating and semi-confusing, but it works. 
//                    if (reader.BaseStream.Position + fileInfo.CompressedLength > reader.BaseStream.Length)
//                    {
//                        Console.WriteLine(
//                            $"    Multi-part file detected @ 0x{reader.BaseStream.Position:X8} (total {fileInfo.CompressedLength}, reader has {reader.BaseStream.Length - reader.BaseStream.Position})");
//
//                        var bytesRead = 0;
//                        var maxBytesRead = fileInfo.CompressedLength - 13; // header is 13 bytes
//                        var curSection = fileInfo.Section;
//
//                        var props = reader.ReadBytes(5);
//                        reader.BaseStream.Seek(8, SeekOrigin.Current);
//
//                        var bytes = new List<byte>();
//                        var readFromMaster = (int) (reader.BaseStream.Length - reader.BaseStream.Position);
//
//                        bytes.AddRange(reader.ReadBytes(readFromMaster));
//                        bytesRead += readFromMaster;
//
//                        Console.WriteLine($"[compressed] Create {fileInfo.Path} –– {fileInfo.FullPath}");
//                        Directory.CreateDirectory(fileInfo.Path);
//
//                        while (bytesRead < maxBytesRead)
//                        {
//                            var newSectUrl = sectionUrl.Replace($"section{fileInfo.Section}",
//                                $"section{++curSection}");
//                            Console.WriteLine($"[{fileInfo.FullPath}]: Next section = {newSectUrl}");
//                            var newSectReader = await FetchSection(newSectUrl);
//
//                            var bytesToRead = (int) (Math.Min(newSectReader.BaseStream.Length,
//                                maxBytesRead - bytes.Count));
//                            bytes.AddRange(newSectReader.ReadBytes(bytesToRead));
//                            bytesRead += bytesToRead;
//                        }
//
//                        curSection = fileInfo.Section;
//
//                        var destLen = new IntPtr(fileInfo.Length);
//                        var srcLen = new IntPtr(fileInfo.CompressedLength - 13);
//
//                        var decompressedOutput = new byte[destLen.ToInt32()];
//
//                        LZMA.AutoUncompress(ref decompressedOutput, ref destLen, bytes.ToArray(),
//                            ref srcLen, props,
//                            _propsSizePtr);
//                        Console.WriteLine("Decompressed");
//
//                        using (var outStream = new FileStream(fileInfo.FullPath, FileMode.Create))
//                        {
//                            outStream.Write(decompressedOutput, 0, decompressedOutput.Length);
//                        }
//
//                        decompressedOutput = null;
//                        props = null;
//
//                        bytes.Clear();
//                    }
//                    else
//                    {
//                        Console.WriteLine(
//                            $"[ compressed:{sectionUrl} ] {fileInfo.FullPath} -----> {fileInfo.Length} bytes uncompressed");
//
//                        Console.WriteLine($"[compressed] create {fileInfo.Path} –– {fileInfo.FullPath}");
//                        Directory.CreateDirectory(fileInfo.Path);
//
//                        var destLen = new IntPtr(fileInfo.Length);
//                        var srcLen = new IntPtr(fileInfo.CompressedLength - 13);
//
//                        var props = reader.ReadBytes(5);
//                        reader.BaseStream.Seek(8, SeekOrigin.Current);
//
//                        lock (_readLock)
//                        {
//                            reader.Read(_dataMap[fileInfo.FullPath], 0, _dataMap[fileInfo.FullPath].Length);
//                        }
//
//                        var decompressedOutput = new byte[destLen.ToInt32()];
//
//                        LZMA.AutoUncompress(ref decompressedOutput, ref destLen, _dataMap[fileInfo.FullPath],
//                            ref srcLen, props,
//                            _propsSizePtr);
//                        Console.WriteLine("Decompressed");
//
//                        using (var outStream = new FileStream(fileInfo.FullPath, FileMode.Create))
//                        {
//                            outStream.Write(decompressedOutput, 0, decompressedOutput.Length);
//                        }
//
//                        decompressedOutput = null;
//                        props = null;
//                    }
//                }
//                else
//                {
//                    // Uncompressed files can be multi-part, too!
//                    // We basically just do the same thing that we do with compressed multipart files.
//                    if (reader.BaseStream.Position + fileInfo.Length > reader.BaseStream.Length)
//                    {
//                        Console.WriteLine(
//                            $"    Multi-part file detected @ 0x{reader.BaseStream.Position:X8} (total {fileInfo.Length}, reader has {reader.BaseStream.Length - reader.BaseStream.Position})");
//
//                        var bytesRead = 0;
//                        var maxBytesRead = fileInfo.Length;
//                        var curSection = fileInfo.Section;
//
//                        var bytes = new List<byte>();
//                        var readFromMaster = (int) (reader.BaseStream.Length - reader.BaseStream.Position);
//
//                        bytes.AddRange(reader.ReadBytes(readFromMaster));
//                        bytesRead += readFromMaster;
//
//                        Console.WriteLine($"[uncompressed] Create {fileInfo.Path} –– {fileInfo.FullPath}");
//                        Directory.CreateDirectory(fileInfo.Path);
//
//                        while (bytesRead < maxBytesRead)
//                        {
//                            var newSectUrl = sectionUrl.Replace($"section{fileInfo.Section}",
//                                $"section{++curSection}");
//                            Console.WriteLine($"[{fileInfo.FullPath}]: Next section = {newSectUrl}");
//                            var newSectReader = await FetchSection(newSectUrl);
//
//                            var bytesToRead = (int) (Math.Min(newSectReader.BaseStream.Length,
//                                maxBytesRead - bytes.Count));
//                            bytes.AddRange(newSectReader.ReadBytes(bytesToRead));
//                            bytesRead += bytesToRead;
//                        }
//
//                        curSection = fileInfo.Section;
//
//                        lock (_readLock)
//                        {
//                            _dataMap[fileInfo.FullPath] = bytes.ToArray();
//                            using (var outStream = new FileStream(fileInfo.FullPath, FileMode.Create))
//                            {
//                                outStream.Write(_dataMap[fileInfo.FullPath], 0, bytes.Count);
//                            }
//                        }
//
//                        bytes.Clear();
//                    }
//                    else
//                    {
//                        // Thankfully there are some files that are small and don't require reading multiple sections at a time.
//                        lock (_readLock)
//                        {
//                            reader.Read(_dataMap[fileInfo.FullPath], 0, _dataMap[fileInfo.FullPath].Length);
//
//                            Console.WriteLine($"[uncompressed] create {fileInfo.Path} / {fileInfo.FullPath}");
//                            Directory.CreateDirectory(fileInfo.Path);
//
//                            using (var fileStream = new FileStream(fileInfo.FullPath, FileMode.Create))
//                            {
//                                fileStream.Write(_dataMap[fileInfo.FullPath], 0,
//                                    _dataMap[fileInfo.FullPath].Length);
//                            }
//                        }
//                    }
//                }
//
//                lock (_readLock)
//                {
//                    _dataMap[fileInfo.FullPath] = null;
//                }
//
//                using (var inStream = File.OpenRead(fileInfo.FullPath))
//                {
//                    using (var md5 = MD5.Create())
//                    {
//                        var hashCalc = Convert.ToBase64String(md5.ComputeHash(inStream));
//                        DebugUtil.EnsureCondition(
//                            hashCalc == fileInfo.Hash,
//                            () => $"Expected hash {fileInfo.Hash} for {fileInfo.FullPath}, got {hashCalc}");
//                    }
//                }
//            }
        }

        private async Task<BinaryReader> FetchSection(string url)
        {
//
            Console.WriteLine($"[INFO] Fetch section: {url}");

            var response = await _client.GetAsync(url);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new SpeedLoadException(
                    $"Failed to retrieve {url}: {response.StatusCode.ToString()}");
            }

            var fileData = await response.Content.ReadAsByteArrayAsync();
            var inputReader = new BinaryReader(new MemoryStream(fileData));

            fileData = null;

            return inputReader;
        }
    }
}