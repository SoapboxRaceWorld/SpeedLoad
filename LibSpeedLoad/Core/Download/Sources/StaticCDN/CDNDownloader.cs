using System;
using System.Collections.Async;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using LibSpeedLoad.Core.Exceptions;
using LibSpeedLoad.Core.Utils;
using SFileInfo = LibSpeedLoad.Core.Download.Sources.StaticCDN.FileInfo;

namespace LibSpeedLoad.Core.Download.Sources.StaticCDN
{
    /**
     * Downloads files from the BBX CDN.
     */
    public class CDNDownloader : Downloader<SFileInfo>
    {
        private readonly HttpClient _client = new HttpClient();

        // Used for LZMA decompression
        private readonly IntPtr _propsSizePtr = new IntPtr(5);

        private StaticCdnSource.Header _header;
        private ulong _bytesRead;

        /// <summary>
        /// Initialize the downloader.
        /// </summary>
        /// <param name="header">The database header.</param>
        public CDNDownloader(StaticCdnSource.Header header)
        {
            _header = header;
        }

        /// <summary>
        /// Reset the downloader.
        /// </summary>
        public void Reset()
        {
            _bytesRead = 0;
        }

        /// <summary>
        /// Set the database header.
        /// </summary>
        /// <param name="header"></param>
        public void SetHeader(StaticCdnSource.Header header)
        {
            _header = header;
        }

        public override Task StartDownload(string url, IEnumerable<SFileInfo> files)
        {
            return Task.Run(async () =>
            {
                var reader = await FetchSection(url);

                foreach (var fileInfo in files)
                {
                    if (File.Exists(fileInfo.FullPath))
                    {
                        _bytesRead += fileInfo.Length;

                        // Trigger the listeners even if the file already exists
                        foreach (var listener in ProgressUpdated)
                        {
                            listener.Invoke(_header.Length, _bytesRead, _header.CompressedLength,
                                fileInfo.OriginalFullPath);
                        }

                        continue;
                    }

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

                    _bytesRead += fileInfo.Length;

                    foreach (var listener in ProgressUpdated)
                    {
                        listener.Invoke(_header.Length, _bytesRead, _header.CompressedLength,
                            fileInfo.OriginalFullPath);
                    }
                }
                
                reader.Dispose();
            });
        }

        private async Task<BinaryReader> FetchSection(string url)
        {
//            Console.WriteLine($"fetch section: {url}");

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
        private async Task HandleUncompressedFile(SFileInfo fileInfo, BinaryReader binaryReader, string sectionUrl)
        {
//            Console.WriteLine($"[FILE-{fileInfo.Section} {fileInfo.FullPath}]: handling uncompressed file");

            // A multipart file can span multiple sections,
            // so we have to keep track of how much has been read. 
            // This code is kind of irritating and semi-confusing, but it works. 
            if (binaryReader.BaseStream.Position + fileInfo.Length > binaryReader.BaseStream.Length)
            {
//                Console.WriteLine(
//                    $"    Multi-part file detected @ 0x{binaryReader.BaseStream.Position:X8} (total {fileInfo.Length}, reader has {binaryReader.BaseStream.Length - binaryReader.BaseStream.Position})");

                uint bytesRead = 0;
                var maxBytesRead = fileInfo.Length;
                var curSection = fileInfo.Section;

                // Here we're reading fragments until we've got everything.
                // First we read as much data from the current section as we can.
                var bytes = new List<byte>();
                var readFromMaster = (int) (binaryReader.BaseStream.Length - binaryReader.BaseStream.Position);

                bytes.AddRange(binaryReader.ReadBytes(readFromMaster));
                bytesRead += (uint) readFromMaster;

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
                    bytesRead += (uint) bytesToRead;
                    
                    newSectReader.Dispose();
                }

                var writeData = bytes.ToArray();
                bytes.Clear();

                using (var outStream = new FileStream(fileInfo.FullPath, FileMode.Create))
                {
                    outStream.Write(writeData, 0, writeData.Length);
                }

                bytes = null;
            }
            else
            {
                var bytes = new byte[fileInfo.Length];
                // Thankfully there are some files that are small and don't require reading multiple sections at a time.
                binaryReader.Read(bytes, 0, bytes.Length);

                using (var fileStream = new FileStream(fileInfo.FullPath, FileMode.Create))
                {
                    fileStream.Write(bytes, 0,
                        bytes.Length);
                }

                bytes = null;
            }
        }

        // Extract a LZMA-compressed file.
        private async Task HandleCompressedFile(SFileInfo fileInfo, BinaryReader binaryReader, string sectionUrl)
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

                var inData = bytes.ToArray();
                bytes.Clear();
                var decompressedOutput = new byte[destLen.ToInt32()];

                LZMA.LzmaUncompress(decompressedOutput, ref destLen, inData,
                    ref srcLen, props,
                    _propsSizePtr);

                using (var outStream = new FileStream(fileInfo.FullPath, FileMode.Create))
                {
                    outStream.Write(decompressedOutput, 0, decompressedOutput.Length);
                }

                decompressedOutput = null;
                props = null;
                inData = null;
            }
            else
            {
                // Thankfully there are some files that are small and don't require reading multiple sections at a time.

                var destLen = new IntPtr(fileInfo.Length);
                var srcLen = new IntPtr(fileInfo.CompressedLength - 13);

                var props = binaryReader.ReadBytes(5);
                binaryReader.BaseStream.Seek(8, SeekOrigin.Current);

                var bytes = new byte[fileInfo.CompressedLength];
                
                binaryReader.Read(bytes, 0, bytes.Length);

                var decompressedOutput = new byte[destLen.ToInt32()];

                LZMA.LzmaUncompress(decompressedOutput, ref destLen, bytes,
                    ref srcLen, props,
                    _propsSizePtr);

                using (var outStream = new FileStream(fileInfo.FullPath, FileMode.Create))
                {
                    outStream.Write(decompressedOutput, 0, decompressedOutput.Length);
                }

                bytes = null;
                decompressedOutput = null;
                props = null;
            }
        }
    }
}