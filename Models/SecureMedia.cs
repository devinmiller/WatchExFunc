using System;
using Newtonsoft.Json;

namespace CotB.WatchExchange.Models
{
    public class SecureMedia
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("oembed")]
        public SecureMediaData Data { get; set; }
    }
}