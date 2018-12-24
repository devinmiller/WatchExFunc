using System;
using System.Collections.Generic;
using CotB.WatchExchange.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace CotB.WatchExchange
{
    public static class NewPostNotifier
    {
        [FunctionName("NewPostNotifier")]
        public static void Run(
            [QueueTrigger("notifications", Connection = "WexConn")]string queueItem, 
            [TwilioSms(AccountSidSetting = "TwilioAccountSidSetting", AuthTokenSetting = "TwilioAuthTokenSetting")] IAsyncCollector<CreateMessageOptions> messages, 
            ILogger log,
            ExecutionContext context)
        {
            log.LogInformation($"C# Queue trigger function processed: {queueItem}");

            var config = new ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            Notification notification = JsonConvert.DeserializeObject<Notification>(queueItem);

            string to = config["TwilioMessageTo"];
            string from = config["TwilioMessageFrom"];

            CreateMessageOptions message = new CreateMessageOptions(new PhoneNumber(to))
            {
                Body = $"{notification.Title} - https://redd.it/{notification.Id.Replace("t3_", string.Empty)}",
                From = from
            };

            if(!string.IsNullOrWhiteSpace(notification.ImageUrl))
            {
                message.MediaUrl = new List<Uri>() { new Uri(notification.ImageUrl) };
            }

            messages.AddAsync(message);
        }
    }
}
