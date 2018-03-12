namespace LibSpeedLoad.Core.Exceptions
{
    public class IntegrityException : SpeedLoadException
    {
        public IntegrityException()
        {
        }

        public IntegrityException(string message) : base(message)
        {
        }
    }
}