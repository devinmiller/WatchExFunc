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

namespace CotB.WatchExchange
{
    public static class NewPostDownloader
    {
        // Create a single, static HttpClient
        // https://docs.microsoft.com/en-us/azure/azure-functions/manage-connections
        private static HttpClient httpClient = new HttpClient();

        [Disable]
        [FunctionName("NewPostDownloader")]
        public static async Task Run(
            [QueueTrigger("downloads", Connection = "WexConn")]string queueItem, 
            [Table("Posts", Connection = "WexConn")]CloudTable input,
            [Queue("notifications", Connection = "WexConn")]IAsyncCollector<PostNotification> queueOutput,
            [Blob("images", FileAccess.ReadWrite, Connection = "WexConn")]CloudBlobContainer blobOutput,
            ILogger log)
        {
            log.LogInformation($"C# Queue trigger function processed.");

            PostData post = JsonConvert.DeserializeObject<PostData>(queueItem);

            //Check if the retrieve operation returned a result
            if(post == null)
            {
                log.LogWarning($"Unable to find post with id {post.Id}");
            }
            else
            {
                log.LogInformation($"Attempting to download image for post id {post.Id}");

                //Regex for determining if link is an image
                Regex imageRegex = new Regex(@"\.(jpg|jpeg|gif|png)$");
                
                // Attempt to get a link to the post image
                Uri postLink = GetImageUri(post);

                //Check if link is an image based on the regex
                bool isImageFile = postLink != null && imageRegex.IsMatch(postLink.Segments.Last());

                //Create notification entity
                PostNotification notification = new PostNotification(post.Id, post.Title);

                if(isImageFile)
                {
                    //Stream image from link
                    using(Stream stream = await httpClient.GetStreamAsync(postLink.ToString()))
                    {
                        //Create block blob reference
                        CloudBlockBlob blob = blobOutput.GetBlockBlobReference($"{post.Id}_{postLink.Segments.Last()}");
                        
                        //Write image stream to blob block
                        await blob.UploadFromStreamAsync(stream);

                        notification.ImageUrl = blob.Uri.ToString();
                    }
                }
                                          

                log.LogInformation($"Adding new post {post.Id} to notification queue");

                //Add new notification entity to queue
                await queueOutput.AddAsync(notification);
            }
        }

        private static Uri GetImageUri(PostData post)
        {
            // if(post.PreviewEnabled)
            // {
            //     var url = post.PreviewUrl;

            //     return new Uri(url.Remove(url.IndexOf('?')));
            // }

            if(post.SecureMedia != null)
            {
                var url = post?.SecureMedia?.Data.ThumbnailUrl;

                return new Uri(url.Remove(url.IndexOf('?')));
            }
            
            if(!post.IsSelf)
            {
                return new Uri(post.Url);
            }

            return null;
        }
    }
}
