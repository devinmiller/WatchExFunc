using System;
using Newtonsoft.Json;

namespace CotB.WatchExchange.Models
{
    public class SecureMediaData
    {
        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("provider_name")]
        public string ProviderName { get; set; }

        [JsonProperty("provider_url")]
        public string ProviderUrl { get; set; }

        [JsonProperty("thumbnail_url")]
        public string ThumbnailUrl { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }
    }
}