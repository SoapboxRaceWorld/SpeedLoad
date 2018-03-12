namespace LibSpeedLoad.Core.Exceptions
{
    public class CorruptDataException : SpeedLoadException
    {
        public CorruptDataException()
        {
        }

        public CorruptDataException(string message) : base(message)
        {
        }
    }
}