using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CotB.WatchExchange.Models.Queue;
using CotB.WatchExchange.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System.Web;

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
            [Table("Posts", Connection = "WexConn")]CloudTable input,
            [Queue("notifications", Connection = "WexConn")]IAsyncCollector<Notification> queueOutput,
            [Blob("images", FileAccess.ReadWrite, Connection = "WexConn")]CloudBlobContainer blobOutput,
            ILogger log)
        {
            log.LogInformation($"C# Queue trigger function processed.");

            Download download = JsonConvert.DeserializeObject<Download>(queueItem);

            //Create the retrieve operation that checks if entity exists
            TableOperation fetchOp = TableOperation.Retrieve<PostData>(download.Author, download.Id);

            //Execute the retrieve operation
            TableResult fetchResult = await input.ExecuteAsync(fetchOp);

            PostData post = fetchResult.Result as PostData;

            //Check if the retrieve operation returned a result
            if(post == null)
            {
                log.LogError($"Unable to retrieve post with Id {download.Id}");
            }
            else
            {
                if(download.ThumbnailUrl != "self")
                {
                    log.LogInformation($"Downloading image from {download.ThumbnailUrl}");
                    
                    //Stream image from link
                    using(Stream stream = await httpClient.GetStreamAsync(download.ThumbnailUrl))
                    {
                        //Create block blob reference
                        CloudBlockBlob blob = blobOutput.GetBlockBlobReference($"{download.Id}_thumbnail.jpg");
                        
                        //Write image stream to blob block
                        await blob.UploadFromStreamAsync(stream);
                    }

                    post.HasThumbnail = true;
                }

                Preview preview = download.Preview;

                if(preview != null)
                {
                    Image image = preview.Images.FirstOrDefault();
                    
                    if(image.Source != null)
                    {
                        log.LogInformation($"Downloading source image image from {HttpUtility.HtmlDecode(image.Source.Url)}");

                        //Stream source image from link
                        using(Stream stream = await httpClient.GetStreamAsync(HttpUtility.HtmlDecode(image.Source.Url)))
                        {
                            //Create block blob reference
                            CloudBlockBlob blob = blobOutput.GetBlockBlobReference($"{download.Id}_source.jpg");
                            
                            //Write image stream to blob block
                            await blob.UploadFromStreamAsync(stream);

                            post.HasImage = true;
                            post.ImageWidth = image.Source.Width;
                            post.ImageHeight = image.Source.Height;
                        }
                    }

                    ImageData previewResolution = image.Resolutions.SingleOrDefault(x => x.Width == 640);

                    if(previewResolution != null)
                    {
                        log.LogInformation($"Downloading preview image image from {HttpUtility.HtmlDecode(previewResolution.Url)}");

                        //Stream source image from link
                        using(Stream stream = await httpClient.GetStreamAsync(HttpUtility.HtmlDecode(previewResolution.Url)))
                        {
                            //Create block blob reference
                            CloudBlockBlob blob = blobOutput.GetBlockBlobReference($"{download.Id}_preview.jpg");
                            
                            //Write image stream to blob block
                            await blob.UploadFromStreamAsync(stream);

                            post.HasPreview = true;
                            post.PreviewWidth = previewResolution.Width;
                            post.PreviewHeight = previewResolution.Height;
                        }
                    }
                }

                // Create the Replace TableOperation.
                TableOperation updateOperation = TableOperation.Replace(post);

                // Execute the operation.
                await input.ExecuteAsync(updateOperation);
            }
        }
    }
}
