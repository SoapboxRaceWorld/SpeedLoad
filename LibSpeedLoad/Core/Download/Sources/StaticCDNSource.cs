using System;
using System.Collections.Async;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using LibSpeedLoad.Core.Download.Events;
using LibSpeedLoad.Core.Download.Sources.StaticCDN;
using LibSpeedLoad.Core.Exceptions;
using LibSpeedLoad.Core.Utils;

namespace LibSpeedLoad.Core.Download.Sources
{
    /**
     * A source that downloads files from the EA BBX CDN.
     * Surprisingly, all (or at least most) of the BBX files are still available,
     * including the files for NFS: World. In fact, you can access earlier builds!
     */
    public class StaticCdnSource : DownloaderSource
    {
        public struct Header
        {
            public ulong Length { get; set; }
            public ulong CompressedLength { get; set; }
            public ulong FirstCabinet { get; set; }
            public ulong LastCabinet { get; set; }
        }

        private class DownloadDatabase
        {
            public Header Header { get; set; }

            public List<StaticCDN.FileInfo> Files { get; } = new List<StaticCDN.FileInfo>();
        }

        // {0} = build
        // {1} = package
        private const string IndexUrlFormat = "http://static.cdn.ea.com/blackbox/u/f/NFSWO/{0}/client/{1}index.xml";

        // {0} = url including build+package
        // {1} = section ID
        private const string SectionUrlFormat = "{0}/section{1}.dat";

        private readonly string _gameIndexUrl;
        private readonly string _tracksIndexUrl;
        private readonly string _tracksHighIndexUrl;
        private readonly string _speechIndexUrl;

        private readonly CDNDownloadOptions _downloadOptions;
        private readonly List<CDNIndex> _indices;
        private readonly Dictionary<string, DownloadDatabase> _databaseCache;
        private readonly Dictionary<string, HashManager> _hashManagers;

        private CDNDownloader _downloader;

        public List<ProgressUpdated> ProgressUpdated { get; } = new List<ProgressUpdated>();

        /**
         * Constructs the source.
         */
        public StaticCdnSource(CDNDownloadOptions options)
        {
            _downloadOptions = options ?? throw new ArgumentNullException(nameof(options));

            _gameIndexUrl = string.Format(IndexUrlFormat, _downloadOptions.GameVersion, "");
            _tracksIndexUrl = string.Format(IndexUrlFormat, _downloadOptions.GameVersion, "Tracks/");
            _tracksHighIndexUrl = string.Format(IndexUrlFormat, _downloadOptions.GameVersion, "TracksHigh/");
            _speechIndexUrl =
                string.Format(IndexUrlFormat, _downloadOptions.GameVersion,
                    $"{_downloadOptions.GameLanguage}/");
            _databaseCache = new Dictionary<string, DownloadDatabase>();
            _hashManagers = new Dictionary<string, HashManager>();
            _indices = GetIndices();

            foreach (var index in _indices)
            {
                _databaseCache[index.Key] = FetchIndex(index.Url).Result;
                _hashManagers[index.Key] = new HashManager(index.Key);

                if (_hashManagers[index.Key].Exists)
                {
                    _hashManagers[index.Key].Load();
                }
            }
        }

        public override Task Download()
        {
            return Task.Run(async () =>
            {
                foreach (var index in _indices)
                {
                    var hashManager = new HashManager(index.Key);

                    if (hashManager.Exists) continue;

                    var database = await LoadIndex(index.Url, index.Key);

                    await database.Files
                        .GroupBy(f => f.Section)
                        .ParallelForEachAsync(async g =>
                        {
                            var sectionUrl = string.Format(SectionUrlFormat, index.Url.Replace("/index.xml", ""),
                                g.Key);

                            await _downloader.StartDownload(sectionUrl, g.ToList());
                        });

                    _downloader.Reset();

                    foreach (var file in database.Files)
                    {
                        hashManager.Put(file.FullPath, file.Hash);
                    }

                    hashManager.Save();
                }
            });
        }

        public override Task VerifyHashes()
        {
            return Task.Run(async () =>
            {
                await _indices.ParallelForEachAsync(async index =>
                {
                    var database = _databaseCache[index.Key];
                    var hashManager = _hashManagers[index.Key];

                    await database.Files.ParallelForEachAsync(async file =>
                    {
                        foreach (var listener in VerificationProgressUpdated)
                        {
                            listener.Invoke(file.FullPath, file.OriginalFullPath,
                                (uint)database.Files.IndexOf(file) + 1, (uint)database.Files.Count);
                        }

                        try
                        {
                            hashManager.Check(file.FullPath, DataUtil.ComputeHash(file.FullPath));
                        }
                        catch (IntegrityException e)
                        {
                            foreach (var listener in VerificationFailed)
                            {
                                listener.Invoke(e.FilePath, e.ExpectedHash, e.ActualHash);
                            }
                        }
                    }, 32);
                }, 32);
            });
        }

        private Task<DownloadDatabase> FetchIndex(string url)
        {
            return Task.Run(async () =>
            {
                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(url);

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        throw new DownloaderWebException(
                            $"Failed to retrieve index {url} - got code {response.StatusCode.ToString()}");
                    }

                    var data = await response.Content.ReadAsStringAsync();
                    var doc = new XmlDocument();

                    doc.Load(new StringReader(data));

                    var database = BuildDatabase(doc);

                    return database;
                }
            });
        }

        private Task<DownloadDatabase> LoadIndex(string url, string key)
        {
            return Task.Run(async () =>
            {
                var database = _databaseCache[key];

                Console.WriteLine($"{database.Header.Length} vs {database.Files.Sum(f => f.Length)}");

                _downloader = new CDNDownloader(database.Header);
                _downloader.ProgressUpdated.Clear();
                _downloader.ProgressUpdated.AddRange(ProgressUpdated);

                return database;
            });
        }

        /**
         * Internal function to build a DownloadDatabase.
         * This lets us keep track of the list of files to download from a store.
         */
        private DownloadDatabase BuildDatabase(XmlDocument document)
        {
            var headerElements = document.GetElementsByTagName("header");
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
                        ulong.Parse(headerEl["length"]?.InnerText ??
                                    throw new InvalidMetadataException("Missing length field in header?")),
                    CompressedLength =
                        ulong.Parse(headerEl["compressed"]?.InnerText ??
                                    throw new InvalidMetadataException("Missing compressed length field in header?")),
                    FirstCabinet = ulong.Parse(headerEl["firstcab"]?.InnerText ??
                                               throw new InvalidMetadataException("Missing firstcab field in header?")),
                    LastCabinet = ulong.Parse(headerEl["lastcab"]?.InnerText ??
                                              throw new InvalidMetadataException("Missing lastcab field in header?")),
                }
            };


            foreach (var fileElement in document.GetElementsByTagName("fileinfo").Cast<XmlElement>())
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

                database.Files.Add(new StaticCDN.FileInfo
                {
                    Path = fileElement.SelectSingleNode("path")?.InnerText
                        .Replace("CDShift", _downloadOptions.GameDirectory),
                    File = fileElement.SelectSingleNode("file")?.InnerText,
                    Hash = fileElement.SelectSingleNode("hash")?.InnerText,
                    Revision = uint.Parse(fileElement["revision"]?.InnerText ??
                                          throw new InvalidMetadataException("Missing revision field")),
                    Section = uint.Parse(fileElement["section"]?.InnerText ??
                                         throw new InvalidMetadataException("Missing section field")),
                    Offset = uint.Parse(fileElement["offset"]?.InnerText ??
                                        throw new InvalidMetadataException("Missing offset field")),
                    Length = uint.Parse(fileElement["length"]?.InnerText ??
                                        throw new InvalidMetadataException("Missing length field")),
                    CompressedLength =
                        fileElement.SelectSingleNode("compressed") != null
                            ? int.Parse(fileElement["compressed"]?.InnerText ??
                                        throw new InvalidMetadataException("Missing compressed length field"))
                            : -1,
                    OriginalPath = fileElement.SelectSingleNode("path")?.InnerText
                });
            }

            return database;
        }

        private List<CDNIndex> GetIndices()
        {
            var indices = new List<CDNIndex>();

            if (_downloadOptions.Download == DownloadData.All)
            {
                _downloadOptions.Download = DownloadData.GameBase | DownloadData.Tracks | DownloadData.Speech |
                                            DownloadData.TracksHigh;
            }

            if (_downloadOptions.Download.HasFlag(DownloadData.GameBase))
                indices.Add(new CDNIndex
                {
                    Key = "",
                    Url = _gameIndexUrl
                });
            if (_downloadOptions.Download.HasFlag(DownloadData.Tracks))
                indices.Add(new CDNIndex
                {
                    Key = "Tracks",
                    Url = _tracksIndexUrl
                });
            if (_downloadOptions.Download.HasFlag(DownloadData.TracksHigh))
                indices.Add(new CDNIndex
                {
                    Key = "TracksHigh",
                    Url = _tracksHighIndexUrl
                });
            if (_downloadOptions.Download.HasFlag(DownloadData.Speech))
                indices.Add(new CDNIndex
                {
                    Key = _downloadOptions.GameLanguage,
                    Url = _speechIndexUrl
                });

            return indices;
        }
    }
}