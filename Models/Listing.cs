using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CotB.WatchExchange.Models
{
    public class Listing
    {
        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("data")]
        public ListingData Data { get; set; }
    }
}