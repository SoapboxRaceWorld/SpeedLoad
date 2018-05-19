namespace LibSpeedLoad.Core.Exceptions
{
    public class IntegrityException : SpeedLoadException
    {
        public string FilePath { get; }
        public string ExpectedHash { get; }
        public string ActualHash { get; }
        
        public IntegrityException(string filePath, string expectedHash, string actualHash)
        {
            FilePath = filePath;
            ExpectedHash = expectedHash;
            ActualHash = actualHash;
        }
    }
}