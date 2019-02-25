using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CotB.WatchExchange.Models.Queue;
using Reddit = CotB.WatchExchange.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System.Web;
using Wex.Context;
using Wex.Context.Models;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;

namespace CotB.WatchExchange
{
    public static class NewPostDownloader
    {
        // Create a single, static HttpClient
        // https://docs.microsoft.com/en-us/azure/azure-functions/manage-connections
        private static HttpClient httpClient = new HttpClient();

        [FunctionName("NewPostDownloader")]
        public static async Task Run(
            [QueueTrigger("downloads", Connection = "WexConn")]string queueItem, 
            [Blob("images", FileAccess.ReadWrite, Connection = "WexConn")]CloudBlobContainer blobOutput,
            ILogger log)
        {
            log.LogInformation($"C# Queue trigger function processed.");

            Download download = JsonConvert.DeserializeObject<Download>(queueItem);

            using(WexContext context = new WexContext())
            {
                Post post = await context.Posts.Include(p => p.Images).SingleOrDefaultAsync(p => p.Id == download.Id);

                //Check if the retrieve operation returned a result
                if(post == null)
                {
                    log.LogError($"Unable to retrieve post with Id {download.Id}");
                }
                else
                {
                    foreach (Image image in post.Images)
                    {
                        int attempts = 0;

                        do
                        {
                            try
                            {
                                log.LogInformation($"Downloading image from {image.Url}");

                                attempts++;

                                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/webp"));

                                //Stream image from link
                                using (Stream stream = await httpClient.GetStreamAsync(HttpUtility.HtmlDecode(image.Url)))
                                {
                                    //Create block blob reference
                                    CloudBlockBlob blob = blobOutput.GetBlockBlobReference(
                                        $"{post.Id}_{post.RedditId}_{image.ImageType.ToString("g")}_{image.Width}_X_{image.Height}.jpg");

                                    //Write image stream to blob block
                                    await blob.UploadFromStreamAsync(stream);
                                }

                                break;

                            }
                            catch (Exception ex)
                            {
                                if (attempts == 3)
                                {
                                    log.LogError(ex, $"Error downloading image from {HttpUtility.HtmlDecode(image.Url)}");
                                    break;
                                }

                                Task.Delay(500).Wait();
                            }
                        }
                        while (true);
                    }
                }
            }
        }
    }
}
