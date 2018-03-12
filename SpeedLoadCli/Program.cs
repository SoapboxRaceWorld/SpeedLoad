using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using LibSpeedLoad.Core;
using LibSpeedLoad.Core.Download;
using LibSpeedLoad.Core.Download.Sources;
using LibSpeedLoad.Core.Download.Sources.StaticCDN;
using LibSpeedLoad.Core.Utils;

namespace SpeedLoadCli
{
    internal static class Program
    {
        public static async Task Main(string[] args)
        {
            DebugUtil.EnsureCondition(args.Length == 1,
                () => "Invalid number of arguments! Please provide the full path to the desired game directory.");

            var downloader = new DownloadManager();

            if (!File.Exists($"{args[0]}/nfsw.exe"))
            {
                downloader.Sources.Add(new StaticCdnSource(new DownloadOptions
                {
                    Download = DownloadData.GameBase | DownloadData.Tracks |
                               DownloadData.Speech,
                    GameDirectory = args[0],
                    GameVersion = "1614b",
                    GameLanguage = "en"
                }));
            }

            downloader.DownloadCompleted.Add(() => { Console.WriteLine("Download completed!"); });
            downloader.DownloadFailed.Add(e => { Console.WriteLine($"Download failed: {e.Message}"); });

            await downloader.Download();
        }
    }
}