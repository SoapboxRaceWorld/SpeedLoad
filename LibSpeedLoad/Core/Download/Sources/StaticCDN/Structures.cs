using System;

namespace LibSpeedLoad.Core.Download.Sources.StaticCDN
{
    public struct FileInfo
    {
        public string Path { get; set; }
        public string File { get; set; }
        public string Hash { get; set; }

        public uint Revision { get; set; }

        // section{section}.dat
        public uint Section { get; set; }

        // offset in sectionX.dat file; NOTE: this MUST ONLY be used on a file once it's decompressed
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
}