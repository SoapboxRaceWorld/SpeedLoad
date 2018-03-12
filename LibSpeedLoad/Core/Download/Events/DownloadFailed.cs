using System;

namespace LibSpeedLoad.Core.Download.Events
{
    /**
     * Called when an error occurs during a download.
     */
    public delegate void DownloadFailed(Exception exception);
}