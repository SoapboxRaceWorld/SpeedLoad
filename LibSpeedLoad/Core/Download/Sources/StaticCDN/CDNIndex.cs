namespace LibSpeedLoad.Core.Download.Sources.StaticCDN
{
    /// <summary>
    /// A simple value object for a download index.
    /// </summary>
    public class CDNIndex
    {
        /// <summary>
        /// A key to identify the index.
        /// </summary>
        public string Key { get; set; }
        
        /// <summary>
        /// The index URL.
        /// </summary>
        public string Url { get; set; }
    }
}