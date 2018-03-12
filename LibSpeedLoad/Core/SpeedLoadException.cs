using System;

namespace LibSpeedLoad.Core
{
    public class SpeedLoadException : Exception
    {
        public SpeedLoadException()
        {
        }
        
        public SpeedLoadException(string message) : base(message)
        {
        }
    }
}