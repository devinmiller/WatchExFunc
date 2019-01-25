using System;

namespace CotB.WatchExchange.Models.Queue
{
    public class Notification
    {
        public Notification(string id, string title)
        {
            Id = id;
            Title = title;
        }

        public string Id { get; set; }
        public string Title { get; set; }
        public string ImageUrl { get; set; }
    }
}