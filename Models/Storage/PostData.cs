using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace CotB.WatchExchange.Models.Storage
{
    public class PostData : TableEntity
    {
        private bool _previewEnabled;
        private string _previewUrl;

        private bool _mediaEnabled;
        private string _mediaUrl;

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; }

        [JsonProperty("create_utc")]
        public uint CreatedUtc { get; set; }

        [JsonProperty("domain")]
        public string Domain { get; set; }

        [JsonProperty("is_meta")]
        public bool IsMeta { get; set; }

        [JsonProperty("is_self")]
        public bool IsSelf { get; set; }

        [JsonProperty("is_video")]
        public bool IsVideo { get; set; }

        [JsonProperty("link_flair_text")]
        public string LinkFlairText { get; set; }

        [JsonProperty("link_flair_type")]
        public string LinkFlairType { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("permalink")]
        public string Permalink { get; set; }

        [JsonProperty("pinned")]
        public bool Pinned { get; set; }

        [JsonProperty("preview")]
        public Preview Preview { get; set; }

        public bool PreviewEnabled 
        { 
            get
            {
                if(Preview != null)
                {
                    return Preview.Enabled;
                }

                return _previewEnabled;
            }
            set
            {
                _previewEnabled = value;
            } 
        }

        public string PreviewUrl 
        { 
            get
            {
                if(Preview != null && Preview.Images.Any())
                {
                    return Preview.Images.First().Source.Url;
                }

                return _previewUrl;
            }
            set
            {
                _previewUrl = value;
            } 
        }

        [JsonProperty("secure_media")]
        public SecureMedia SecureMedia { get; set; }

        public bool SecureMediaEnabled 
        { 
            get
            {
                if(SecureMedia != null)
                {
                    return true;
                }

                return _mediaEnabled;
            }
            set
            {
                _mediaEnabled = value;
            } 
        }

        public string SecureMediaUrl 
        { 
            get
            {
                if(SecureMedia != null && SecureMedia.Data != null)
                {
                    return SecureMedia.Data.ThumbnailUrl;
                }

                return _mediaUrl;
            }
            set
            {
                _mediaUrl = value;
            } 
        }

        [JsonProperty("self_text")]
        public string SelfText { get; set; }

        [JsonProperty("thumbnail")]
        public string Thumbnail { get; set; }

        [JsonProperty("stickied")]
        public bool Stickied { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }
    }
}