using System;
using System.Net.Http;
using System.Threading.Tasks;
using CotB.WatchExchange.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CotB.WatchExchange
{
    public static class NewPostParser
    {
        // Create a single, static HttpClient
        // https://docs.microsoft.com/en-us/azure/azure-functions/manage-connections
        private static HttpClient httpClient = new HttpClient();

        [FunctionName("NewPostParser")]
        public static async Task Run([TimerTrigger("0 */5 * * * *")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            // Pull json data back from the /r/WatchExchange feed sorted by newest posts
            HttpResponseMessage response = await httpClient.GetAsync("https://www.reddit.com/r/watchexchange/new.json");

            // Check if the response includes a success status code
            if(response.IsSuccessStatusCode)
            {
                // Read response content into string
                string result = await response.Content.ReadAsStringAsync();
                // Take the string content and deserialize
                Listing listing = JsonConvert.DeserializeObject<Listing>(result);

                log.LogInformation($"Found new post with title: {listing?.Data?.Posts[0]?.Data.Title}");
            }
            else
            {
                log.LogError($"Error fetching posts: {response.StatusCode} - {response.ReasonPhrase}.");
            }
        }
    }
}
