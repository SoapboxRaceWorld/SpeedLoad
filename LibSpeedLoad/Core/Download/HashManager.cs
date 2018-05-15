using System;
using System.Collections.Generic;
using LibSpeedLoad.Core.Utils;
using Newtonsoft.Json;

namespace LibSpeedLoad.Core.Download
{
    /// <summary>
    /// The structure of a hash entry.
    /// </summary>
    public struct HashFile
    {
        /// <summary>
        /// The full relative path to the file.
        /// </summary>
        public string FilePath { get; set; }
        
        /// <summary>
        /// The base64-encoded hash of the file.
        /// </summary>
        public string Hash { get; set; }
    }

    /// <summary>
    /// A list of hash entries.
    /// </summary>
    public struct HashList
    {
        [JsonProperty("Files")]
        public List<HashFile> Files { get; set; }
    }
    
    /// <summary>
    /// A manager class to handle loading and saving hash files.
    /// </summary>
    public class HashManager
    {
        private const string HashFileFormat = "HashFile{0}.hsh";

        /// <summary>
        /// The identifier of the hash file.
        /// </summary>
        private readonly string _id;
        
        /// <summary>
        /// A mapping of file paths to hashes.
        /// </summary>
        private readonly Dictionary<string, string> _hashMap;

        /// <summary>
        /// The full hash file name.
        /// </summary>
        public string HashFile => string.Format(HashFileFormat, _id);  

        /// <summary>
        /// Initialize the hash manager.
        /// </summary>
        /// <param name="id">The file identifier.</param>
        public HashManager(string id)
        {
            _id = id;
            _hashMap = new Dictionary<string, string>();
        }

        /// <summary>
        /// Load the hash mapping data from the file.
        /// </summary>
        public void Load()
        {
            _hashMap.Clear();

            var list = DataUtil.ReadJson<HashList>(HashFile);

            foreach (var hashEntry in list.Files)
            {
                _hashMap[hashEntry.FilePath] = hashEntry.Hash;
            }
        }

        /// <summary>
        /// Get the hash for a file.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public string Get(string filePath)
        {
            if (!_hashMap.ContainsKey(filePath))
            {
                throw new ArgumentException($"No value found for key {filePath}");
            }
            
            return _hashMap[filePath];
        }

        /// <summary>
        /// Set a file's hash.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <param name="hash">The hash.</param>
        public void Put(string filePath, string hash)
        {
            _hashMap[filePath] = hash;
        }

        /// <summary>
        /// Save the hash mapping data to the file.
        /// </summary>
        public void Save()
        {
            var list = new HashList();
            
            foreach (var keyValuePair in _hashMap)
            {
                list.Files.Add(new HashFile
                {
                    FilePath = keyValuePair.Key,
                    Hash = keyValuePair.Value
                });
            }
            
            DataUtil.WriteJson(HashFile, list);
        }

        /// <summary>
        /// Reset the hash table.
        /// </summary>
        public void Reset()
        {
            _hashMap.Clear();
        }
    }
}