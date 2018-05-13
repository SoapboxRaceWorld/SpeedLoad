using System.Collections.Generic;
using System.Threading.Tasks;
using LibSpeedLoad.Core.Download.Events;

namespace LibSpeedLoad.Core.Download.Sources
{
    /**
     * The base class for a downloader.
     */
    public abstract class Downloader<TFi> where TFi : struct
    {
        public List<ProgressUpdated> ProgressUpdated { get; } = new List<ProgressUpdated>();
        
        public abstract Task StartDownload(string url, IEnumerable<TFi> files);
    }
}