using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CotB.WatchExchange.Models
{
    public class Preview
    {
        [JsonProperty("images")]
        public List<Image> Images { get; set; }

        [JsonProperty("enabled")]
        public bool Enabled { get; set; }
    }
}