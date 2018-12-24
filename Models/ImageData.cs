using System;
using Newtonsoft.Json;

namespace CotB.WatchExchange.Models
{
    public class ImageData
    {
        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }
    }
}