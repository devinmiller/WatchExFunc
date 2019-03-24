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
using Microsoft.WindowsAzure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.EntityFrameworkCore;

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
            ILogger log,
            ExecutionContext context)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            var config = new ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();


            log.LogInformation($"Executing check for deleted posts at {DateTime.Now}");

            await CheckForDeleted(config, log);

            log.LogInformation($"Executing check for missing images at {DateTime.Now}");

            await CheckForImages(downloads, log);
        }

        /// <summary>
        /// Will check for posts that have been deleted and remove the corresponding database
        /// and blob entries.
        /// </summary>
        private static async Task CheckForDeleted(IConfiguration config, ILogger log)
        {
            using (WexContext context = new WexContext())
            {
                var offset = DateTimeOffset.UtcNow.AddMinutes(-15).ToUnixTimeSeconds();

                var posts = context.Posts
                    .Where(x =>
                        x.CreatedUtc > offset &&
                        x.IsMeta == false &&
                        x.Stickied == false)
                    .Include(x => x.Images)
                    .ToList();

                foreach (var post in posts)
                {
                    log.LogInformation(
                        "Retrieving JSON for post {PostId} from {Permalink}",
                        post.Id, post.Permalink);

                    // Pull json data back from the /r/WatchExchange feed sorted by newest posts
                    HttpResponseMessage response = await httpClient.GetAsync($"https://www.reddit.com{post.Permalink}.json");

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

                        if (postData.SelfText == "[deleted]" && postData.Author == "[deleted]")
                        {
                            log.LogInformation("Found deleted post {PostID} from {Permalink}", post.Id, post.Permalink);

                            if (post.Images != null)
                            {
                                if (CloudStorageAccount.TryParse(config["WexConn"], out CloudStorageAccount storageAccount))
                                {
                                    string containerName = "images";
                                    CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();
                                    CloudBlobContainer cloudBlobContainer = cloudBlobClient.GetContainerReference(containerName);

                                    foreach (Image image in post.Images)
                                    {
                                        try
                                        {
                                            string fileName =
                                                $"{post.Id}_{post.RedditId}_{image.ImageType.ToString("g")}_{image.Width}_X_{image.Height}.jpg";

                                            CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(fileName);

                                            log.LogInformation("Deleting file {FileName} from blob storage container {ContainerName}", fileName, containerName);

                                            await cloudBlockBlob.DeleteIfExistsAsync();

                                            log.LogInformation("Delete image {ImageId} from database", image.Id);

                                            context.Images.Remove(image);
                                        }
                                        catch(Exception ex)
                                        {
                                            log.LogError(ex, "Error deleting image {ImageID}", image.Id);
                                        }
                                    }
                                }
                                else
                                {
                                    log.LogError("Unable to connect to blob storage with expected connection string: {ConnectionName}", "WexConn");
                                }
                            }

                            log.LogInformation("Delete post {PostId} from database", post.Id);

                            context.Posts.Remove(post);

                            await context.SaveChangesAsync();
                        }
                    }
                    else
                    {
                        log.LogWarning(
                            "Unable to retrieve JSON for {PostId} from {Permalink}: {Reason}",
                            post.Id, post.Permalink, response.ReasonPhrase);
                    }
                }
            }
        }

        /// <summary>
        /// TODO: Will check for posts with matching authors/titles, deleting the old of the two.
        /// The intent is to handle the following scenarios:
        /// 1.  A legitimate repost to get back on the front page. 
        /// 2.  A duplicate of a new post that wasn't deleted correctly.
        /// </summary>
        private static async Task CheckForDuplicates(IConfiguration config, ILogger log)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Recheck posts withinm the last 15 minutes for images.
        /// </summary>
        private static async Task CheckForImages(IAsyncCollector<Download> downloads, ILogger log)
        {
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
                    log.LogInformation(
                        "Retrieving JSON for post {PostId} from {Permalink}",
                        post.Id, post.Permalink);

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

                        if (postData?.Preview != null)
                        {
                            log.LogInformation(
                                "Found missing images for post {PostId} from {Permalink}",
                                post.Id, post.Permalink);

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
                    else
                    {
                        log.LogWarning(
                            "Unable to retrieve JSON for {PostId} from {Permalink}: {Reason}",
                            post.Id, post.Permalink, response.ReasonPhrase);
                    }
                }

                await context.SaveChangesAsync();

                foreach (Post post in newPosts.Where(p => p.Images.Any()))
                {
                    await downloads.AddAsync(new Download(post.Id));
                }

                log.LogInformation("Found a total of {PostCount} posts missing images.", newPosts.Count);
            }
        }
    }
}
