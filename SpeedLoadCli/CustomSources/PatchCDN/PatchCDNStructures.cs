using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SpeedLoadCli.CustomSources.PatchCDN
{
    public struct FileList
    {
        [JsonProperty("files")]
        public List<FileInfo> Files { get; set; }
    }
    
    public struct Patch
    {
        public uint ID { get; set; }
        
        public string PatchId { get; set; }
        
        public string Branch { get; set; }
        
        public bool IsPublished { get; set; }
        
        public string Description { get; set; }
        
        public DateTime CreatedAt { get; set; }
        
        public DateTime UpdatedAt { get; set; }
    }
    
    public struct FileInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("hash256")]
        public string Hash { get; set; }
        
        [JsonIgnore]
        public string Path { get; set; }
    }
    
    public class PatchDownloadOptions
    {
        public string GameDirectory { get; set; }
    }
}