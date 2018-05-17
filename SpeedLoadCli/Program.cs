using System;
using System.IO;
using System.Threading.Tasks;
using LibSpeedLoad.Core;
using LibSpeedLoad.Core.Download.Sources;
using LibSpeedLoad.Core.Download.Sources.StaticCDN;
using LibSpeedLoad.Core.Utils;
using SpeedLoadCli.CustomSources;
using SpeedLoadCli.CustomSources.PatchCDN;

namespace SpeedLoadCli
{
    internal static class Program
    {
        public static async Task Main(string[] args)
        {
            DebugUtil.EnsureCondition(args.Length == 1,
                () => "Invalid number of arguments! Please provide the full path to the desired game directory.");

            var downloader = new DownloadManager();

//            if (!File.Exists($"{args[0]}/nfsw.exe"))
            {
                var staticCdnSource = new StaticCdnSource(new CDNDownloadOptions
                {
                    Download = DownloadData.GameBase | DownloadData.TracksHigh,
                    GameDirectory = args[0],
                    GameVersion = "1614b",
                    GameLanguage = "en"
                });
                
                staticCdnSource.ProgressUpdated.Add((length, downloaded, compressedLength, file) =>
                {
                    Console.WriteLine($"file: {file} - downloaded: {downloaded}/{length}");
                });
                
                downloader.Sources.Add(staticCdnSource);
            }
            
            downloader.Sources.Add(new PatchCDNSource(new PatchDownloadOptions
            {
                GameDirectory = args[0]
            }));

            downloader.DownloadCompleted.Add(() => { Console.WriteLine("Download completed!"); });
            downloader.DownloadFailed.Add(e =>
            {
                Console.WriteLine($"Download failed: {e.Message} (in {e.TargetSite.DeclaringType?.FullName}.{e.TargetSite?.Name})");
                Console.WriteLine(e.StackTrace);
            });

            await downloader.Download();
            await downloader.VerifyHashes();
        }
    }
}