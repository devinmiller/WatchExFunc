using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CotB.WatchExchange.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions;
using Microsoft.Azure.WebJobs.Extensions.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;


namespace CotB.WatchExchange
{
    public static class NewPostParser
    {
        // Create a single, static HttpClient
        // https://docs.microsoft.com/en-us/azure/azure-functions/manage-connections
        private static HttpClient httpClient = new HttpClient();

        [FunctionName("NewPostParser")]
        public static async Task Run(
            [TimerTrigger("0 */1 * * * *")]TimerInfo myTimer, 
            [Table("Posts", Connection = "WexConn")]CloudTable input,
            [Table("Posts", Connection = "WexConn")]IAsyncCollector<PostData> tableOutput,
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
                    foreach (PostData post in posts)
                    {
                        //Create the retrieve operation that checks if entity exists
                        TableOperation fetchOp = TableOperation.Retrieve<PostData>("Post", post.Id);

                        //Execute the retrieve operation
                        TableResult fetchResult = await input.ExecuteAsync(fetchOp);

                        //Check if the retrieve operation returned a result
                        if(fetchResult.Result == null)
                        {
                            post.PartitionKey = "Post";
                            post.RowKey = post.Id;

                            log.LogInformation($"Adding new post {post.Id} to storage table");

                            //Add entity to storage table if not found
                            await tableOutput.AddAsync(post);

                            //Create notification entity
                            Notification notification = new Notification(post.Id, post.Title);

                            //Self post will not include an image (I think)
                            if(!post.IsSelf)
                            {
                                //Regex for determining if link is an image
                                Regex imageRegex = new Regex(@"\.(jpg|gif|png)$");
                                
                                Uri postLink = new Uri(post.Url);

                                //Check if link is an image based on the regex
                                bool isImageFile = imageRegex.IsMatch(postLink.Segments.Last());

                                if(isImageFile)
                                {
                                    //Stream image from link
                                    using(Stream stream = await httpClient.GetStreamAsync(post.Url))
                                    {
                                        //Create block blob reference
                                        CloudBlockBlob blob = blobOutput.GetBlockBlobReference($"{post.Id}_{postLink.Segments.Last()}");
                                        
                                        //Write image stream to blob block
                                        await blob.UploadFromStreamAsync(stream);

                                        notification.ImageUrl = blob.Uri.ToString();
                                    }
                                }
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
    }
}
