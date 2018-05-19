namespace LibSpeedLoad.Core.Download.Events
{
    public delegate void VerificationProgressUpdated(
        string file,
        string displayFile,
        uint fileNumber,
        uint totalFiles
    );
}