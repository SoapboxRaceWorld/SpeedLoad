using System.Collections.Generic;
using System.Threading.Tasks;

namespace LibSpeedLoad.Core.Download.Sources
{
    /**
     * The base class for a downloader.
     */
    public abstract class Downloader<TFi> where TFi : struct
    {
        public abstract Task StartDownload(string url, IEnumerable<TFi> files);
    }
}