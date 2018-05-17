using System;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json;

namespace LibSpeedLoad.Core.Utils
{
    /// <summary>
    /// Utility functions for reading and writing data to files,
    /// and for other file operations.
    /// </summary>
    public static class DataUtil
    {
        /// <summary>
        /// Read a JSON-encoded object from a file.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <typeparam name="T">The object type.</typeparam>
        /// <returns>The deserialized object.</returns>
        public static T ReadJson<T>(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(nameof(path));
            }

            return JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
        }

        /// <summary>
        /// Write a JSON-encoded object to a file.
        /// The file will be created if it does not exist, or overwritten if it does.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <param name="obj">The object to write to the file.</param>
        /// <typeparam name="T">The object type.</typeparam>
        public static void WriteJson<T>(string path, T obj)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(obj));
        }

        /// <summary>
        /// Compute a Base64-encoded MD5 hash of a file.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <returns>The file hash.</returns>
        public static string ComputeHash(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(nameof(path));
            }

            using (var stream = File.OpenRead(path))
            {
                using (var md5 = MD5.Create())
                {
                    return Convert.ToBase64String(md5.ComputeHash(stream));
                }
            }
        }

        /// <summary>
        /// Compute a Base64-encoded MD5 hash of a file.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <returns>The file hash.</returns>
        public static string ComputeHash256(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(nameof(path));
            }

            using (var stream = File.OpenRead(path))
            {
                using (var sha256 = SHA256.Create())
                {
                    return DebugUtil.Sha256ToString(sha256.ComputeHash(stream));
                }
            }
        }
    }
}