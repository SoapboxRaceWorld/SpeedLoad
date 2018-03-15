using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using LibSpeedLoad.Core.Exceptions;
using LibSpeedLoad.Core.Utils;

namespace LibSpeedLoad.Core.Download.Sources.StaticCDN
{
    /**
     * Downloads files from the BBX CDN.
     */
    public class CDNDownloader : Downloader<FileInfo>
    {
        private readonly HttpClient _client = new HttpClient();
        private readonly ConcurrentDictionary<string, byte[]> _dataMap = new ConcurrentDictionary<string, byte[]>();

        // Used for LZMA decompression
        private readonly IntPtr _propsSizePtr = new IntPtr(5);

        public override Task StartDownload(string url, IEnumerable<FileInfo> files)
        {
            return Task.Run(async () =>
            {
                var reader = await FetchSection(url);

                foreach (var fileInfo in files)
                {
                    if (File.Exists(fileInfo.FullPath))
                    {
                        continue;
                    }

                    _dataMap[fileInfo.FullPath] = new byte[fileInfo.Length];

                    reader.BaseStream.Position = fileInfo.Offset;

                    Directory.CreateDirectory(fileInfo.Path);

                    // Is it compressed?
                    if (fileInfo.CompressedLength != -1)
                    {
                        await HandleCompressedFile(fileInfo, reader, url);
                    }
                    else
                    {
                        await HandleUncompressedFile(fileInfo, reader, url);
                    }

                    _dataMap[fileInfo.FullPath] = null;
                }
            });
        }

        private async Task<BinaryReader> FetchSection(string url)
        {
            var response = await _client.GetAsync(url);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new DownloaderWebException(
                    $"Failed to retrieve {url}: {response.StatusCode.ToString()}");
            }

            var fileData = await response.Content.ReadAsByteArrayAsync();
            var inputReader = new BinaryReader(new MemoryStream(fileData));

            fileData = null;

            return inputReader;
        }

        // Extract a file that isn't LZMA-compressed.
        private async Task HandleUncompressedFile(FileInfo fileInfo, BinaryReader binaryReader, string sectionUrl)
        {
//            Console.WriteLine($"[FILE-{fileInfo.Section} {fileInfo.FullPath}]: handling uncompressed file");

            // A multipart file can span multiple sections,
            // so we have to keep track of how much has been read. 
            // This code is kind of irritating and semi-confusing, but it works. 
            if (binaryReader.BaseStream.Position + fileInfo.Length > binaryReader.BaseStream.Length)
            {
//                Console.WriteLine(
//                    $"    Multi-part file detected @ 0x{binaryReader.BaseStream.Position:X8} (total {fileInfo.Length}, reader has {binaryReader.BaseStream.Length - binaryReader.BaseStream.Position})");

                var bytesRead = 0;
                var maxBytesRead = fileInfo.Length;
                var curSection = fileInfo.Section;

                // Here we're reading fragments until we've got everything.
                // First we read as much data from the current section as we can.
                var bytes = new List<byte>();
                var readFromMaster = (int) (binaryReader.BaseStream.Length - binaryReader.BaseStream.Position);

                bytes.AddRange(binaryReader.ReadBytes(readFromMaster));
                bytesRead += readFromMaster;

                // Then, we continuously jump to the next section, until all the data is retrieved.
                while (bytesRead < maxBytesRead)
                {
                    // Create a new section URL that gives us access to the next section.
                    var newSectUrl = sectionUrl.Replace($"section{fileInfo.Section}",
                        $"section{++curSection}");
                    // Fetch a BinaryReader for the new section.
                    var newSectReader = await FetchSection(newSectUrl);

                    // Calculate the remaining number of bytes to read.
                    // This takes into account the length of the section as opposed to the total length of the file.
                    var bytesToRead = (int) Math.Min(newSectReader.BaseStream.Length,
                        maxBytesRead - bytes.Count);
                    bytes.AddRange(newSectReader.ReadBytes(bytesToRead));
                    bytesRead += bytesToRead;
                }

                _dataMap[fileInfo.FullPath] = bytes.ToArray();
                using (var outStream = new FileStream(fileInfo.FullPath, FileMode.Create))
                {
                    outStream.Write(_dataMap[fileInfo.FullPath], 0, bytes.Count);
                }

                bytes.Clear();
            }
            else
            {
                // Thankfully there are some files that are small and don't require reading multiple sections at a time.
                binaryReader.Read(_dataMap[fileInfo.FullPath], 0, _dataMap[fileInfo.FullPath].Length);

                using (var fileStream = new FileStream(fileInfo.FullPath, FileMode.Create))
                {
                    fileStream.Write(_dataMap[fileInfo.FullPath], 0,
                        _dataMap[fileInfo.FullPath].Length);
                }
            }
        }

        // Extract a LZMA-compressed file.
        private async Task HandleCompressedFile(FileInfo fileInfo, BinaryReader binaryReader, string sectionUrl)
        {
//            Console.WriteLine($"[FILE-{fileInfo.Section} {fileInfo.FullPath}]: handling compressed file");

            // A multipart file can span multiple sections,
            // so we have to keep track of how much has been read. 
            // This code is kind of irritating and semi-confusing, but it works. 
            if (binaryReader.BaseStream.Position + fileInfo.CompressedLength > binaryReader.BaseStream.Length)
            {
//                Console.WriteLine(
//                    $"    Multi-part file detected @ 0x{binaryReader.BaseStream.Position:X8} (total {fileInfo.CompressedLength}, reader has {binaryReader.BaseStream.Length - binaryReader.BaseStream.Position})");

                var bytesRead = 0;
                var maxBytesRead = fileInfo.CompressedLength - 13; // header is 13 bytes
                var curSection = fileInfo.Section;

                var props = binaryReader.ReadBytes(5);
                binaryReader.BaseStream.Seek(8, SeekOrigin.Current);

                // Here we're reading fragments until we've got everything.
                // First we read as much data from the current section as we can.
                var bytes = new List<byte>();
                var readFromMaster = (int) (binaryReader.BaseStream.Length - binaryReader.BaseStream.Position);

                bytes.AddRange(binaryReader.ReadBytes(readFromMaster));
                bytesRead += readFromMaster;

                // Then, we continuously jump to the next section, until all the data is retrieved.
                while (bytesRead < maxBytesRead)
                {
                    // Create a new section URL that gives us access to the next section.
                    var newSectUrl = sectionUrl.Replace($"section{fileInfo.Section}",
                        $"section{++curSection}");
                    // Fetch a BinaryReader for the new section.
                    var newSectReader = await FetchSection(newSectUrl);

                    // Calculate the remaining number of bytes to read.
                    // This takes into account the length of the section as opposed to the total length of the file.
                    var bytesToRead = (int) Math.Min(newSectReader.BaseStream.Length,
                        maxBytesRead - bytes.Count);
                    bytes.AddRange(newSectReader.ReadBytes(bytesToRead));
                    bytesRead += bytesToRead;
                }

                var destLen = new IntPtr(fileInfo.Length);
                var srcLen = new IntPtr(fileInfo.CompressedLength - 13);

                var decompressedOutput = new byte[destLen.ToInt32()];

                LZMA.LzmaUncompress(decompressedOutput, ref destLen, bytes.ToArray(),
                    ref srcLen, props,
                    _propsSizePtr);

                using (var outStream = new FileStream(fileInfo.FullPath, FileMode.Create))
                {
                    outStream.Write(decompressedOutput, 0, decompressedOutput.Length);
                }

                decompressedOutput = null;
                props = null;

                bytes.Clear();
            }
            else
            {
                // Thankfully there are some files that are small and don't require reading multiple sections at a time.

                var destLen = new IntPtr(fileInfo.Length);
                var srcLen = new IntPtr(fileInfo.CompressedLength - 13);

                var props = binaryReader.ReadBytes(5);
                binaryReader.BaseStream.Seek(8, SeekOrigin.Current);

                binaryReader.Read(_dataMap[fileInfo.FullPath], 0, _dataMap[fileInfo.FullPath].Length);

                var decompressedOutput = new byte[destLen.ToInt32()];

                LZMA.LzmaUncompress(decompressedOutput, ref destLen, _dataMap[fileInfo.FullPath],
                    ref srcLen, props,
                    _propsSizePtr);

                using (var outStream = new FileStream(fileInfo.FullPath, FileMode.Create))
                {
                    outStream.Write(decompressedOutput, 0, decompressedOutput.Length);
                }

                decompressedOutput = null;
                props = null;
            }
        }
    }
}