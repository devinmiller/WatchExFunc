using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Reddit = CotB.WatchExchange.Models;
using Wex.Context;
using System.Collections.Generic;
using Wex.Context.Models;
using CotB.WatchExchange.Models.Queue;

namespace WatchExFunc
{
    public static class NewPostVerifier
    {
        // Create a single, static HttpClient
        // https://docs.microsoft.com/en-us/azure/azure-functions/manage-connections
        private static HttpClient httpClient = new HttpClient();

        [FunctionName("NewPostVerifier")]
        public static async Task Run(
            [TimerTrigger("0 */15 * * * *")]TimerInfo myTimer,
            [Queue("downloads", Connection = "WexConn")]IAsyncCollector<Download> downloads, 
            ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            using (WexContext context = new WexContext())
            {
                var offset = DateTimeOffset.UtcNow.AddMinutes(-15).ToUnixTimeSeconds();

                var posts = context.Posts.Where(x => 
                    x.CreatedUtc > offset &&
                    x.IsMeta == false &&
                    x.Stickied == false &&
                    !x.Images.Any());

                List<Post> newPosts = new List<Post>();

                foreach (var post in posts)
                {
                    // Pull json data back from the /r/WatchExchange feed sorted by newest posts
                    HttpResponseMessage response = await httpClient.GetAsync($"https://www.reddit.com{post.Permalink}.json");

                    // Check if the response includes a success status code
                    if (response.IsSuccessStatusCode)
                    {
                        // Read response content into string
                        string result = await response.Content.ReadAsStringAsync();
                        // Take the string content and deserialize
                        List<Reddit.Listing> listing = JsonConvert.DeserializeObject<List<Reddit.Listing>>(result);
                        // Flatten deserialized content into a list of post data
                        Reddit.PostData postData = listing?.FirstOrDefault()?.Data.Posts
                            .Where(x => x.Kind == "t3")
                            .Select(x => x.Data)
                            .SingleOrDefault();

                        if (postData.Preview != null)
                        {
                            post.Images = new List<Image>();

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

                            newPosts.Add(post);
                        }
                    }
                }

                await context.SaveChangesAsync();

                foreach (Post post in newPosts.Where(p => p.Images.Any()))
                {
                    await downloads.AddAsync(new Download(post.Id));
                }

                log.LogInformation($"Found a total of {newPosts.Count} posts missing images.");
            }
        }
    }
}