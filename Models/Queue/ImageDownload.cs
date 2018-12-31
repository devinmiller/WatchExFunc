using System;

namespace CotB.WatchExchange.Models.Queue
{
    public class ImageDownload
    {
        public ImageDownload(string id)
        {
            Id = id;
        }

        public string Id { get; set; }
    }
    
}