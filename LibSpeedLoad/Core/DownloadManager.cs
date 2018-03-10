using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using LibSpeedLoad.Core.Download;
using LibSpeedLoad.Core.Utils;
using System.Collections.Async;
using System.Net;

namespace LibSpeedLoad.Core
{
    public class DownloadManager
    {
        // {0} = build
        // {1} = package
        private const string IndexUrlFormat = "http://static.cdn.ea.com/blackbox/u/f/NFSWO/{0}/client/{1}index.xml";

        // {0} = url including build+package
        // {1} = section ID
        private const string SectionUrlFormat = "{0}/section{1}.dat";

        private readonly DownloadOptions _downloadOptions;

        private struct Header
        {
            public long Length { get; set; }
            public long CompressedLength { get; set; }
            public long FirstCabinet { get; set; }
            public long LastCabinet { get; set; }
        }

        private class DownloadDatabase
        {
            public Header Header { get; set; }

            public List<FileInfo> Files { get; } = new List<FileInfo>();
        }

        public struct FileInfo
        {
            public string Path { get; set; }
            public string File { get; set; }
            public string Hash { get; set; }

            public uint Revision { get; set; }

            // section{section}.dat; this kinda makes sense in some way, it's just chunking the download... hmm...
            public uint Section { get; set; }

            // offset in sectionX.dat file; NOTE: this MUST ONLY be used on the decompressed file 
            public uint Offset { get; set; }
            public uint Length { get; set; }
            public int CompressedLength { get; set; }

            public string FullPath => $"{Path}/{File}";
        }

        [Flags]
        public enum DownloadData
        {
            All = 1,
            GameBase = 2,
            Tracks = 4,
            TracksHigh = 8,
            Speech = 16,
        }

        public class DownloadOptions
        {
            public DownloadData Download { get; set; }

            public string GameDirectory { get; set; }

            public string GameVersion { get; set; }

            // Primarily for speech
            public string GameLanguage { get; set; }
        }

        /**
         * Constructs a downloader.
         * 
         * The version parameter should be a build ID string; for example, "1614b" for the last client version.
         */
        public DownloadManager(DownloadOptions options)
        {
            _downloadOptions = options ?? throw new ArgumentNullException(nameof(options));

            _gameIndexUrl = string.Format(IndexUrlFormat, _downloadOptions.GameVersion, "");
            _tracksIndexUrl = string.Format(IndexUrlFormat, _downloadOptions.GameVersion, "Tracks/");
            _tracksHighIndexUrl = string.Format(IndexUrlFormat, _downloadOptions.GameVersion, "TracksHigh/");
            _speechIndexUrl =
                string.Format(IndexUrlFormat, _downloadOptions.GameVersion,
                    $"{_downloadOptions.GameLanguage}/"); // gotta add some sort of language setting
        }

        public Task Download()
        {
            return Task.Run(async () =>
            {
                Console.WriteLine($"Game Version: {_downloadOptions.GameVersion}");
                Console.WriteLine($"Download flags: {_downloadOptions.Download}");

                if (_downloadOptions.Download == DownloadData.All)
                {
                    Console.WriteLine("Downloading all data");

                    _downloadOptions.Download = DownloadData.GameBase | DownloadData.Tracks | DownloadData.Speech |
                                                DownloadData.TracksHigh;
                }

                if (_downloadOptions.Download.HasFlag(DownloadData.GameBase))
                {
                    Console.WriteLine("---------------------");
                    Console.WriteLine("Downloading game base");
                    await LoadIndex(_gameIndexUrl);
                    Console.WriteLine("---------------------");
                }
                
                if (_downloadOptions.Download.HasFlag(DownloadData.Tracks))
                {
                    Console.WriteLine("---------------------");
                    Console.WriteLine("Downloading tracks");
                    await LoadIndex(_tracksIndexUrl);
                    Console.WriteLine("---------------------");
                }
                
                if (_downloadOptions.Download.HasFlag(DownloadData.TracksHigh))
                {
                    Console.WriteLine("---------------------");
                    Console.WriteLine("Downloading TracksHigh");
                    await LoadIndex(_tracksHighIndexUrl);
                    Console.WriteLine("---------------------");
                }
                
                if (_downloadOptions.Download.HasFlag(DownloadData.Speech))
                {
                    Console.WriteLine("---------------------");
                    Console.WriteLine("Downloading speech");
                    await LoadIndex(_speechIndexUrl);
                    Console.WriteLine("---------------------");
                }
            });
        }

        private Task LoadIndex(string url)
        {
            Console.WriteLine($"loadIndex called with url: {url}");
            return Task.Run(async () =>
            {
                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(url);

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        throw new SpeedLoadException(
                            $"Index retrieval failed with code {response.StatusCode.ToString()}");
                    }

                    var data = await response.Content.ReadAsStringAsync();

                    var doc = new XmlDocument();

                    doc.Load(new StringReader(data));

                    var headerElements = doc.GetElementsByTagName("header");
                    DebugUtil.EnsureCondition(
                        headerElements.Count == 1,
                        () => "Failed to read header!");

                    var headerEl = headerElements[0];

                    DebugUtil.EnsureCondition(headerEl.HasChildNodes, () => "Invalid header: code 1");
                    DebugUtil.EnsureCondition(headerEl["length"] != null, () => "Invalid header: no length");
                    DebugUtil.EnsureCondition(headerEl["compressed"] != null,
                        () => "Invalid header: no compressed length");
                    DebugUtil.EnsureCondition(headerEl["firstcab"] != null, () => "Invalid header: no firstcab");
                    DebugUtil.EnsureCondition(headerEl["lastcab"] != null, () => "Invalid header: no lastcab");

                    var database = new DownloadDatabase
                    {
                        Header = new Header
                        {
                            Length =
                                long.Parse(headerEl["length"]?.InnerText ??
                                           throw new SpeedLoadException("Shouldn't ever see this v1")),
                            CompressedLength =
                                long.Parse(headerEl["compressed"]?.InnerText ??
                                           throw new SpeedLoadException("Shouldn't ever see this v2")),
                            FirstCabinet = long.Parse(headerEl["firstcab"]?.InnerText ??
                                                      throw new SpeedLoadException("Shouldn't ever see this v3")),
                            LastCabinet = long.Parse(headerEl["lastcab"]?.InnerText ??
                                                     throw new SpeedLoadException("Shouldn't ever see this v4")),
                        }
                    };


                    foreach (var fileElement in doc.GetElementsByTagName("fileinfo").Cast<XmlElement>())
                    {
                        DebugUtil.EnsureCondition(
                            fileElement.SelectSingleNode("path") != null,
                            () => "Invalid file info: No path key!");
                        DebugUtil.EnsureCondition(
                            fileElement.SelectSingleNode("file") != null,
                            () => "Invalid file info: No file key!");
                        DebugUtil.EnsureCondition(
                            fileElement.SelectSingleNode("hash") != null,
                            () => "Invalid file info: No hash key!");
                        DebugUtil.EnsureCondition(
                            fileElement.SelectSingleNode("revision") != null,
                            () => "Invalid file info: No revision key!");
                        DebugUtil.EnsureCondition(
                            fileElement.SelectSingleNode("section") != null,
                            () => "Invalid file info: No section key!");
                        DebugUtil.EnsureCondition(
                            fileElement.SelectSingleNode("offset") != null,
                            () => "Invalid file info: No offset key!");
                        DebugUtil.EnsureCondition(
                            fileElement.SelectSingleNode("length") != null,
                            () => "Invalid file info: No length key!");

                        database.Files.Add(new FileInfo
                        {
                            Path = fileElement.SelectSingleNode("path")?.InnerText,
                            File = fileElement.SelectSingleNode("file")?.InnerText,
                            Hash = fileElement.SelectSingleNode("hash")?.InnerText,
                            Revision = uint.Parse(fileElement["revision"]?.InnerText ??
                                                  throw new SpeedLoadException("File info error 1")),
                            Section = uint.Parse(fileElement["section"]?.InnerText ??
                                                 throw new SpeedLoadException("File info error 2")),
                            Offset = uint.Parse(fileElement["offset"]?.InnerText ??
                                                throw new SpeedLoadException("File info error 3")),
                            Length = uint.Parse(fileElement["length"]?.InnerText ??
                                                throw new SpeedLoadException("File info error 4")),
                            CompressedLength =
                                fileElement.SelectSingleNode("compressed") != null
                                    ? int.Parse(fileElement["compressed"]?.InnerText ??
                                                throw new SpeedLoadException("File info error 5"))
                                    : -1
                        });
                    }

                    await database.Files
                        .Select(f =>
                        {
                            f.Path = f.Path.Replace("CDShift", _downloadOptions.GameDirectory);

                            return f;
                        })
                        .GroupBy(f => f.Section)
                        .ParallelForEachAsync(async g =>
                        {
                            var sectionUrl = string.Format(SectionUrlFormat, url.Replace("/index.xml", ""), g.Key);
                            Console.WriteLine($"Download section: {sectionUrl} (count: {g.Count()})");
                            await _downloader.StartDownload(sectionUrl, g.ToList());
                        }, 50);
                }
            });
        }

        private readonly string _gameIndexUrl;
        private readonly string _tracksIndexUrl;
        private readonly string _tracksHighIndexUrl;
        private readonly string _speechIndexUrl;
        private readonly Downloader _downloader = new Downloader();
    }
}