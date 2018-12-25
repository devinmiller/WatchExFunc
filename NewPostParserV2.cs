using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CotB.WatchExchange.Models;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions;
using Microsoft.Azure.WebJobs.Extensions.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Microsoft.Azure.Documents.Linq;

namespace CotB.WatchExchange
{
    public static class NewPostParserV2
    {
        // Create a single, static HttpClient
        // https://docs.microsoft.com/en-us/azure/azure-functions/manage-connections
        private static HttpClient httpClient = new HttpClient();

        [FunctionName("NewPostParserV2")]
        public static async Task Run(
            [TimerTrigger("0 */5 0-5,13-23 * * *")]TimerInfo myTimer, 
            [CosmosDB("WatchExchange", "Posts", ConnectionStringSetting = "WexConnCosmos")]DocumentClient documentClient,
            [CosmosDB("WatchExchange", "Posts", ConnectionStringSetting = "WexConnCosmos")]IAsyncCollector<dynamic> documentOutput,
            [Queue("notifications", Connection = "WexConn")]IAsyncCollector<Notification> queueOutput,
            [Blob("images", FileAccess.ReadWrite, Connection = "WexConn")]CloudBlobContainer blobOutput,
            ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            log.LogInformation($"Fetching post data from Reddit");

            // Pull json data back from the /r/WatchExchange feed sorted by newest posts
            HttpResponseMessage response = await httpClient.GetAsync("https://www.reddit.com/r/watchexchange/new.json");

            // Check if the response includes a success status code
            if(response.IsSuccessStatusCode)
            {
                // Read response content into string
                string result = await response.Content.ReadAsStringAsync();
                // Take the string content and deserialize
                Listing listing = JsonConvert.DeserializeObject<Listing>(result);

                List<PostData> posts = listing?.Data?.Posts?
                    .Select(post => post.Data)
                    .ToList();

                int total = 0;

                if(posts != null)
                {
                    Uri collectionUri = UriFactory.CreateDocumentCollectionUri("WatchExchange", "Posts");

                    foreach (PostData post in posts)
                    {
                        FeedOptions feedOptions = new FeedOptions()
                        {
                            EnableCrossPartitionQuery = true
                        };

                        IQueryable<PostData> query = documentClient
                            .CreateDocumentQuery<PostData>(collectionUri, feedOptions)
                            .Where(p => p.Id == post.Id)
                            .AsQueryable();

                        //Check if the retrieve operation returned a result
                        if(!query.ToList().Any())
                        {
                            log.LogInformation($"Adding new post {post.Id} to cosmos db collection");

                            //Add entity to cosmos db document if not found
                            await documentOutput.AddAsync(post);

                            //Create notification entity
                            Notification notification = new Notification(post.Id, post.Title);

                            string imageUrl = await UploadImage(post, blobOutput);

                            if(!string.IsNullOrEmpty(imageUrl))
                            {
                                notification.ImageUrl = imageUrl;
                            }

                            log.LogInformation($"Adding new post {post.Id} to storage queue");

                            //Add new notification entity to queue
                            await queueOutput.AddAsync(notification);

                            total++;
                        }
                    }

                    log.LogInformation($"Found a total of {total} new posts");
                }
            }
            else
            {
                log.LogError($"Error fetching posts: {response.StatusCode} - {response.ReasonPhrase}.");
            }
        }

        private static async Task<string> UploadImage(PostData post, CloudBlobContainer blobOutput)
        {
            //Self post will not include an image (I think)
            if(!post.IsSelf)
            {
                //Regex for determining if link is an image
                Regex imageRegex = new Regex(@"\.(jpg|gif|png)$");
                
                Uri postLink = null;

                postLink = GetImageUri(post);

                //Check if link is an image based on the regex
                bool isImageFile = imageRegex.IsMatch(postLink.Segments.Last());

                if(isImageFile)
                {
                    //Stream image from link
                    using(Stream stream = await httpClient.GetStreamAsync(postLink))
                    {
                        //Create block blob reference
                        CloudBlockBlob blob = blobOutput.GetBlockBlobReference($"{post.Id}_{postLink.Segments.Last()}");
                        
                        //Write image stream to blob block
                        await blob.UploadFromStreamAsync(stream);

                        return blob.Uri.ToString();
                    }
                }
            } 

            return string.Empty;
        }

        private static Uri GetImageUri(PostData post)
        {
            if(post.Preview.Enabled)
            {
                return new Uri(post.Preview.Images.First().Source.Url);
            }

            if(post.SecureMedia != null)
            {
                var url = post.SecureMedia.Data.ThumbnailUrl;

                return new Uri(url.Remove(url.IndexOf('?')));
            }
            
            return new Uri(post.Url);
        }
    }
}
