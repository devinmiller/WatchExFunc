using System;

namespace CotB.WatchExchange.Models.Queue
{
    public class Download
    {
        public Download(string id, string author, string thumbnailUrl, Preview preview)
        {
            Id = id;
            Author = author;
            ThumbnailUrl = thumbnailUrl;
            Preview = preview;
        }

        public string Id { get; set; }
        public string Author { get; set; }

        public string ThumbnailUrl { get; set; }
        public Preview Preview { get; set; }
    }
    
}