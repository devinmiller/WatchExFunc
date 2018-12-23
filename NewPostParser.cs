using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CotB.WatchExchange.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
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
            [TimerTrigger("0 */5 06-20 * * *")]TimerInfo myTimer, 
            [Table("Posts", Connection = "WexConn")]CloudTable input,
            [Table("Posts", Connection = "WexConn")]IAsyncCollector<PostData> tableOutput,
            [Queue("notifications", Connection = "WexConn")]IAsyncCollector<Notification> queueOutput,
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

                            log.LogInformation($"Adding new post {post.Id} to storage queue");

                            //Add new notification entity to queue
                            await queueOutput.AddAsync(new Notification(post.Id, post.Title));

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
