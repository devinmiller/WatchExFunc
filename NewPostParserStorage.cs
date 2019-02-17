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
using CotB.WatchExchange.Models.Queue;
using Microsoft.AspNetCore.Http;

namespace CotB.WatchExchange
{
    public static class NewPostParserStorage
    {
        // Create a single, static HttpClient
        // https://docs.microsoft.com/en-us/azure/azure-functions/manage-connections
        private static HttpClient httpClient = new HttpClient();

        [Disable]
        [FunctionName("NewPostParserStorage")]
        public static async Task Run(
            [TimerTrigger("0 */5 * * * *")]TimerInfo myTimer, 
            [Table("Posts", Connection = "WexConn")]CloudTable input,
            [Queue("notifications", Connection = "WexConn")]IAsyncCollector<Notification> notifications,
            [Queue("downloads", Connection = "WexConn")]IAsyncCollector<Download> downloads,
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
                // Flatten deserialized content into a list of post data
                List<PostData> posts = listing?.Data?.Posts?
                    .Select(post => post.Data)
                    .ToList();

                int total = 0;

                if(posts != null)
                {
                    foreach (PostData post in posts)
                    {
                        //Create the retrieve operation that checks if entity exists
                        TableOperation fetchOp = TableOperation.Retrieve<PostData>(post.Author, post.Id);

                        //Execute the retrieve operation
                        TableResult fetchResult = await input.ExecuteAsync(fetchOp);

                        //Check if the retrieve operation returned a result
                        if(fetchResult.Result == null)
                        {
                            post.PartitionKey = post.Author;
                            post.RowKey = post.Id;

                            log.LogInformation($"Adding new post {post.Id} to storage table");

                            // Create the TableOperation object that inserts the customer entity.
                            TableOperation insertOperation = TableOperation.Insert(post);

                            // Execute the insert operation.
                            TableResult insertResult = await input.ExecuteAsync(insertOperation);   

                            if(insertResult.HttpStatusCode == StatusCodes.Status204NoContent)
                            {
                                log.LogInformation($"Adding new post {post.Id} to queues");

                                //Add new entity to queues
                                await notifications.AddAsync(new Notification(post.Id, post.Title));
                                await downloads.AddAsync(new Download(post.Id, post.Author, post.Thumbnail, post.Preview));
                            }
                            else
                            {
                                log.LogWarning($"Unexpected response (${insertResult.HttpStatusCode}) from table storage.");
                            }                      

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
