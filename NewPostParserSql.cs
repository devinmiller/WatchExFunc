using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Reddit = CotB.WatchExchange.Models;
using CotB.WatchExchange.Models.Queue;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Wex.Context;
using Wex.Context.Models;
using System.Web;

namespace WatchExFunc
{
    public static class NewPostParserSql
    {
        // Create a single, static HttpClient
        // https://docs.microsoft.com/en-us/azure/azure-functions/manage-connections
        private static HttpClient httpClient = new HttpClient();

        [FunctionName("NewPostParserSql")]
        public static async Task Run([TimerTrigger("0 */1 * * * *")]TimerInfo myTimer, 
            [Queue("notifications", Connection = "WexConn")]IAsyncCollector<Notification> notifications,
            [Queue("downloads", Connection = "WexConn")]IAsyncCollector<Download> downloads,
            ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            log.LogInformation($"Fetching post data from Reddit");

            // Pull json data back from the /r/WatchExchange feed sorted by newest posts
            HttpResponseMessage response = await httpClient.GetAsync("https://www.reddit.com/r/watchexchange/new.json");

            // Check if the response includes a success status code
            if (response.IsSuccessStatusCode)
            {
                // Read response content into string
                string result = await response.Content.ReadAsStringAsync();
                // Take the string content and deserialize
                Reddit.Listing listing = JsonConvert.DeserializeObject<Reddit.Listing>(result);
                // Flatten deserialized content into a list of post data
                List<Reddit.PostData> posts = listing?.Data?.Posts?
                    .Select(post => post.Data)
                    .ToList();

                List<Post> newPosts = new List<Post>();

                if (posts != null)
                {
                    using (WexContext context = new WexContext())
                    {
                        foreach (Reddit.PostData postData in posts)
                        {
                            if(!context.Posts.Where(x => x.RedditId == postData.Id).Any())
                            {
                                Post post = new Post()
                                {
                                    RedditId = postData.Id,
                                    
                                    CreatedUtc = postData.CreatedUtc,

                                    Author = postData.Author,
                                    Domain = postData.Domain,

                                    IsMeta = postData.IsMeta,
                                    IsSelf = postData.IsSelf,
                                    IsVideo = postData.IsVideo,

                                    LinkFlairText = postData.LinkFlairText,
                                    LinkFlairType = postData.LinkFlairType,

                                    Pinned = postData.Pinned,
                                    Stickied = postData.Stickied,

                                    Title = postData.Title,
                                    Name = postData.Name,
                                    Permalink = postData.Permalink,
                                    Url = postData.Url,
                                    SelfText = postData.SelfText,

                                    Images = new List<Image>()
                                };

                                if(postData.Preview != null && postData.Preview.Enabled)
                                {
                                    foreach (Reddit.Image image in postData.Preview.Images)
                                    {
                                        post.Images.Add(new Image()
                                        {
                                            ImageType = ImageType.Source,
                                            Url = image.Source.Url,
                                            Width = image.Source.Width,
                                            Height = image.Source.Height
                                        });

                                        post.Images = post.Images.Concat(
                                            image.Resolutions.Select(x => new Image()
                                            {
                                                ImageType = ImageType.Resolution,
                                                Url = x.Url,
                                                Width = x.Width,
                                                Height = x.Height
                                            })).ToList();
                                    }
                                }
                            
                                context.Attach(post);

                                newPosts.Add(post);
                            }
                        }

                       await context.SaveChangesAsync();

                        foreach (Post post in newPosts.Where(p => p.Images.Any()))
                        {
                            await downloads.AddAsync(new Download(post.Id));
                        }

                    }
                        
                    log.LogInformation($"Found a total of {newPosts.Count} new posts");
                }
            }
            else
            {
                log.LogError($"Error fetching posts: {response.StatusCode} - {response.ReasonPhrase}.");
            }
        }
    }
}
