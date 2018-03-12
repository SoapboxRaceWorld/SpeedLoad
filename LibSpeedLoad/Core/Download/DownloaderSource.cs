using System.Collections.Generic;
using System.Threading.Tasks;

namespace LibSpeedLoad.Core.Download
{
    /**
     * A downloader source.
     * 
     * A source is responsible for using a given URL
     * to download files.
     */
    public abstract class DownloaderSource<TFi> where TFi : struct
    {
        /**
         * Begin the download process.
         */
        public abstract Task Download();
    }
}