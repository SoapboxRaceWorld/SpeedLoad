using System;
using System.Threading.Tasks;
using LibSpeedLoad.Core.Download.Sources.StaticCDN;

namespace LibSpeedLoad.Core.Download.Sources
{
    /**
     * A source that downloads files from the EA BBX CDN.
     * Surprisingly, all (or at least most) of the BBX files are still available,
     * including the files for NFS: World. In fact, you can access earlier builds!
     */
    public class StaticCdnSource : DownloaderSource<FileInfo>
    {
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

        private readonly DownloadOptions _downloadOptions;

        /**
         * Constructs the source.
         */
        public StaticCdnSource(DownloadOptions options)
        {
            _downloadOptions = options ?? throw new ArgumentNullException(nameof(options));

            _gameIndexUrl = string.Format(IndexUrlFormat, _downloadOptions.GameVersion, "");
            _tracksIndexUrl = string.Format(IndexUrlFormat, _downloadOptions.GameVersion, "Tracks/");
            _tracksHighIndexUrl = string.Format(IndexUrlFormat, _downloadOptions.GameVersion, "TracksHigh/");
            _speechIndexUrl =
                string.Format(IndexUrlFormat, _downloadOptions.GameVersion,
                    $"{_downloadOptions.GameLanguage}/");
        }

        public override Task Download()
        {
            return Task.Run(() =>
            {
                Console.WriteLine($"Game Version: {_downloadOptions.GameVersion}");
                Console.WriteLine($"Download flags: {_downloadOptions.Download}");
            });
        }
    }
}