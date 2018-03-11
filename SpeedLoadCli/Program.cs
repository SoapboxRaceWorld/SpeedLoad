using System;
using System.Threading.Tasks;
using LibSpeedLoad.Core;
using LibSpeedLoad.Core.Utils;

namespace SpeedLoadCli
{
    internal static class Program
    {
        public static async Task Main(string[] args)
        {
            DebugUtil.EnsureCondition(args.Length == 1, () => "Invalid number of arguments! Please provide the full path to the desired game directory.");
            
            var downloader = new DownloadManager(new DownloadManager.DownloadOptions
            {
                Download = DownloadManager.DownloadData.GameBase | DownloadManager.DownloadData.Tracks | DownloadManager.DownloadData.Speech,
                GameDirectory = args[0],
                GameVersion = "1614b",
                GameLanguage = "en"
            });
            
            await downloader.Download();
            
            Console.WriteLine("Completed!");
        }
    }
}