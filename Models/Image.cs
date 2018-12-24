using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CotB.WatchExchange.Models
{
    public class Image
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("source")]
        public ImageData Source { get; set; }

        [JsonProperty("resolutions")]
        public List<ImageData> Resolutions { get; set; }
    }
}