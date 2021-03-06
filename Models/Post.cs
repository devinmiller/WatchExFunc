using System;
using Newtonsoft.Json;

namespace CotB.WatchExchange.Models
{
    public class Post
    {
        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("data")]
        public PostData Data { get; set; }
    }
}