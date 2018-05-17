using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using LibSpeedLoad.Core.Download;
using LibSpeedLoad.Core.Utils;
using System.Collections.Async;
using System.Net;
using LibSpeedLoad.Core.Download.Events;

namespace LibSpeedLoad.Core
{
    public class DownloadManager
    {
        public List<DownloadCompleted> DownloadCompleted { get; } = new List<DownloadCompleted>();
        public List<DownloadFailed> DownloadFailed { get; } = new List<DownloadFailed>();

        public List<DownloaderSource> Sources { get; }
        
        /**
         * Constructs a download manager.
         */
        public DownloadManager(List<DownloaderSource> sources = null)
        {
            Sources = sources ?? new List<DownloaderSource>();
        }

        public Task Download()
        {
            return Task.Run(async () =>
            {
                try
                {
                    foreach (var source in Sources)
                    {
                        await source.Download();
                    }

                    foreach (var handler in DownloadCompleted)
                    {
                        handler.DynamicInvoke();
                    }
                }
                catch (AggregateException e)
                {
                    foreach (var ex in e.InnerExceptions)
                    {
                        foreach (var handler in DownloadFailed)
                        {
                            handler.DynamicInvoke(ex);
                        }

                        break;
                    }
                }
                catch (Exception e)
                {
                    foreach (var handler in DownloadFailed)
                    {
                        handler.DynamicInvoke(e);
                    }
                }
            });
        }
        
        public Task VerifyHashes()
        {
            return Task.Run(async () =>
            {
                try
                {
                    foreach (var source in Sources)
                    {
                        await source.VerifyHashes();
                    }
                }
                catch (AggregateException e)
                {
                    foreach (var ex in e.InnerExceptions)
                    {
                        foreach (var handler in DownloadFailed)
                        {
                            handler.DynamicInvoke(ex);
                        }

                        break;
                    }
                }
                catch (Exception e)
                {
                    foreach (var handler in DownloadFailed)
                    {
                        handler.DynamicInvoke(e);
                    }
                }
            });
        }
    }
}