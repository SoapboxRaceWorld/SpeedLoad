namespace LibSpeedLoad.Core.Exceptions
{
    public class InvalidMetadataException : SpeedLoadException
    {
        public InvalidMetadataException()
        {
        }

        public InvalidMetadataException(string message) : base(message)
        {
        }
    }
}