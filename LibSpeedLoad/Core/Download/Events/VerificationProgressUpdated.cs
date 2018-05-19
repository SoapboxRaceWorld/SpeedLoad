namespace LibSpeedLoad.Core.Download.Events
{
    public delegate void VerificationProgressUpdated(
        string file,
        uint fileNumber,
        uint totalFiles
    );
}