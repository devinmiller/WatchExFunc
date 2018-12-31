using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CotB.WatchExchange.Models.Storage
{
    public class ListingData
    {
        [JsonProperty("modhash")]
        public string ModHash { get; set; }

        [JsonProperty("dist")]
        public int Count { get; set; }

        [JsonProperty("children")]
        public List<Post> Posts { get; set; }

        [JsonProperty("before")]
        public string Previous { get; set; }

        [JsonProperty("after")]
        public string Next { get; set; }
    }
}