using System.IO;
using Newtonsoft.Json;

namespace LibSpeedLoad.Core.Utils
{
    /// <summary>
    /// Utility functions for reading and writing data to files.
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
    }
}