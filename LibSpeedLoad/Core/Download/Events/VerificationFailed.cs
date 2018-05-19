namespace LibSpeedLoad.Core.Download.Events
{
    public delegate void VerificationFailed(
        string file,
        string expectedHash,
        string actualHash
    );
}