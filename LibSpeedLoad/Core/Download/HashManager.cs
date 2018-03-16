using System;

namespace LibSpeedLoad.Core.Download
{
    /**
     * Keeps track of file hash data.
     * This is a singleton because why not.
     */
    public sealed class HashManager
    {
        private const string HashFileFormat = "HashFile{0}.hsh";

        private static readonly Lazy<HashManager> Lazy = new Lazy<HashManager>(() => new HashManager());

        private HashManager()
        {
        }

        public static HashManager Instance => Lazy.Value;
    }
}