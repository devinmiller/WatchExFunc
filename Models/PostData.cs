using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CotB.WatchExchange.Models
{
    public class PostData
    {
        [JsonProperty("title")]
        public string Title { get; set; }
    }
}