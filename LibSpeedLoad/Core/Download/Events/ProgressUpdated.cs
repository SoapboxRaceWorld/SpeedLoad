namespace LibSpeedLoad.Core.Download.Events
{
    public delegate void ProgressUpdated(
        ulong length,
        ulong bytesDownloaded,
        ulong compressedLength,
        string file
    );
}