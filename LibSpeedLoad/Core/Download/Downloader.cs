using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace LibSpeedLoad.Core.Download
{
    /**
     * Handles downloading and decompressing files.
     */
    public class Downloader
    {
        private static class LZMA
        {
            [DllImport("EasyLZMA", CharSet = CharSet.None, ExactSpelling = false)]
            public static extern int LzmaUncompress(byte[] dest, ref IntPtr destLen, byte[] src, ref IntPtr srcLen,
                byte[] outProps, IntPtr outPropsSize);
        }

        private readonly HttpClient _client = new HttpClient();
        private readonly IntPtr _propsSizePtr = new IntPtr(5);

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
                        var compressedInput = inputReader.ReadBytes(fileInfo.CompressedLength - 13);
                        var decompressedOutput = new byte[destLen.ToInt32()];

                        LZMA.LzmaUncompress(decompressedOutput, ref destLen, compressedInput, ref srcLen, props,
                            _propsSizePtr);
                        Console.WriteLine("Decompressed");

                        using (var outStream = new FileStream(fileInfo.FullPath, FileMode.Create))
                        {
                            outStream.Write(decompressedOutput, 0, decompressedOutput.Length);
                        }

                        decompressedOutput = null;
                        compressedInput = null;

                        props = null;
                    }
                    else
                    {
                        inputReader.BaseStream.Position = fileInfo.Offset;
                        var read = new byte[fileInfo.Length];
                        inputReader.Read(read, 0, read.Length);

                        Console.WriteLine($"[uncompressed] create {fileInfo.Path} / {fileInfo.FullPath}");
                        Directory.CreateDirectory(fileInfo.Path);

                        using (var fileStream = new FileStream(fileInfo.FullPath, FileMode.Create))
                        {
                            fileStream.Write(read, 0, read.Length);
                        }
                    }
                }
            });
        }
    }
}