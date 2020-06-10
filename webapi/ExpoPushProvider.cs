using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using RestSharp;
using RestSharp.Authenticators;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using webapi.Models.Db;

namespace webapi
{
    public class ExpoPushAdapter
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="users">Each user record should contain the deviceToken field filled.</param>
        /// <param name="title"></param>
        /// <param name="message"></param>
        public static IList<PushNotification> GetNotifications(IEnumerable<User> users, string title, string message)
        {
            var notifications = new List<PushNotification>();

            foreach (var u in users)
            {
                if (!string.IsNullOrWhiteSpace(u.DeviceToken))
                {
                    notifications.Add(new PushNotification { To = u.DeviceToken, Title = title, Body = message });
                }
            }

            return notifications;
        }
    }

    public class ExpoPushProvider
    {
        public static void EnqueueNotifications(IEnumerable<PushNotification> notifications)
        {
            Initialize();

            if (mFlushAndExit || mExitWithoutFinishing) throw new Exception("Error.NoMoreAccepted");

            lock (mLock)
            {
                foreach (var n in notifications)
                {
                    if (n.To == null || n.Title == null || n.Body == null) continue;

                    mQueue.Enqueue(n);
                }
            }

            // Log that nn notifications were sent (to a text file or some logging facility).
        }

        public static int QueueLength
        {
            get { return mQueue.Count; }
        }

        public static void Stop()
        {
            mExitWithoutFinishing = true;
            if (mWorkerThread != null) mWorkerThread.Join();

            Log.Information("ExpoPushProvider: Immediate shutdown with {0} notifications in the queue.", mQueue.Count);
        }

        public static void FlushAndStop()
        {
            mFlushAndExit = true;
            if (mWorkerThread != null) mWorkerThread.Join();

            Log.Information("ExpoPushProvider: Flush shutdown with {0} notifications in the queue.", mQueue.Count);
        }

        private static void Initialize()
        {
            if (mWorkerThread != null) return;

            mWorkerThread = new Thread(Worker)
            {
                Name = "PushNotificationWorker",
                IsBackground = true
            };

            mIsRunning = true;
            mWorkerThread.Start();
        }

        private static void Worker()
        {
            while (mIsRunning)
            {
                try
                {
                    if (mQueue.Count > 0)
                    {
                        PushNotification[] packet = null;

                        lock (mLock)
                        {
                            var numItems = Math.Min(mQueue.Count, MaxExpoNotificationsInPacket);
                            packet = new PushNotification[numItems];

                            for (int i = 0; i < numItems; ++i) packet[i] = mQueue.Dequeue();
                        }

                        if (packet == null) continue;

                        var result = SendPushNotifications(packet);

                        ProcessResult(result);
                    }
                    else
                    {
                        if (mFlushAndExit) break;
                    }

                    if (mExitWithoutFinishing) break;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "ExpoPushProvider");  // Log exception, move on
                }

                Thread.Sleep(SpinWaitTime);
            }
        }

        internal static void ProcessResult(PushResponse result)
        {
            if (result.Errors != null)
            {
                // Request errors, should not happen. 
            }

            foreach (var data in result.Data)
            {
                if (data.Status == "error" && data.Details != null)
                {
                    if (data.Details.Error == "DeviceNotRegistered")
                    {
                        // Delete device from the DB.
                    }
                }
            }
            // Handle results: if any device has an error, delete it from the DB. But we should do this in a separate thread, return here as soon as possible. 
            // Maybe the sending should also go to producer/consumer with a queue, then processing the result can be done there with the required timing

            // Do we also create notifications in the DB?
            // Or just a notification to the sender to leave a log of the things sent? 
        }


        internal static PushResponse SendPushNotifications(IEnumerable<PushNotification> notification)
        {
            // SIMULATE SENDING LOAD
            //Thread.Sleep(5000);
            //return null;

            var requestJson = GetJsonFromNotification(notification);

            string responseJson = null;

            using (WebClient client = new WebClient())
            {
                client.Headers.Add("accept", "application/json");
                client.Headers.Add("accept-encoding", "gzip, deflate");
                client.Headers.Add("Content-Type", "application/json");
                responseJson = client.UploadString("https://exp.host/--/api/v2/push/send", requestJson);
            }

            var response = GetReponseFromJson(responseJson);
            return response;
        }

        private static string GetJsonFromNotification(IEnumerable<PushNotification> n)
        {
            return JsonConvert.SerializeObject(n, 
                            Formatting.Indented,
                            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        }

        private static PushResponse GetReponseFromJson(string json)
        {
            return JsonConvert.DeserializeObject<PushResponse>(json);
        }


        private const int SpinWaitTime = 1000;  // ms
        private const int MaxExpoNotificationsInPacket = 100;

        private static Thread mWorkerThread;
        private static Queue<PushNotification> mQueue = new Queue<PushNotification>();
        private static bool mIsRunning;
        private static bool mExitWithoutFinishing;
        private static bool mFlushAndExit;
        private static readonly object mLock = new object();
    }







    public class ExpoPushReceiptHandler
    {
        public static void EnqueueReceipt(string receiptId)
        {
            Initialize();

            if (mFlushAndExit || mExitWithoutFinishing) throw new Exception("Error.NoMoreAccepted");

            lock (mLock)
            {
                mReceipts.Add(receiptId, null);
            }

            // Log that nn notifications were sent (to a text file or some logging facility).
        }

        public static void Stop()
        {
            mExitWithoutFinishing = true;
            if (mWorkerThread != null) mWorkerThread.Join();

            Log.Information("ExpoPushReceiptHandler: Immediate shutdown with {0} receipts in the queue.", mReceipts.Count);
        }

        public static void FlushAndStop()
        {
            mFlushAndExit = true;
            if (mWorkerThread != null) mWorkerThread.Join();

            Log.Information("ExpoPushReceiptHandler: Flush shutdown with {0} receipts in the queue.", mReceipts.Count);
        }

        private static void Initialize()
        {
            if (mWorkerThread != null) return;

            mWorkerThread = new Thread(Worker)
            {
                Name = "PushReceiptsWorker",
                IsBackground = true
            };

            mIsRunning = true;
            mWorkerThread.Start();
        }

        private static void Worker()
        {
            while (mIsRunning)
            {
                try
                {
                    if (mReceipts.Count > 0)
                    {
                        var response = QueryReceipts(mReceipts.Keys);

                        HandleReceiptsResponse(response);
                    }
                    else
                    {
                        if (mFlushAndExit) break;
                    }

                    if (mExitWithoutFinishing) break;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "ExpoPushProvider");  // Log exception, move on
                }

                Thread.Sleep(SpinWaitTime);
            }
        }


        internal static ReceiptResponse QueryReceipts(IEnumerable<string> receiptIds)
        {
            var requestJson = GetJsonFromCollection(receiptIds);

            string responseJson = null;

            using (WebClient client = new WebClient())
            {
                client.Headers.Add("accept", "application/json");
                client.Headers.Add("accept-encoding", "gzip, deflate");
                client.Headers.Add("Content-Type", "application/json");
                responseJson = client.UploadString("https://exp.host/--/api/v2/push/getReceipts", requestJson);
            }

            var response = GetReponseFromJson(responseJson);
            return response;
        }

        private static void HandleReceiptsResponse(ReceiptResponse response)
        {
            if (response == null || response.Data == null) return;


        }



        private static string GetJsonFromCollection(IEnumerable<string> n)
        {
            var target = new ReceiptsRequest { Ids = n };

            return JsonConvert.SerializeObject(target,
                            Formatting.Indented,
                            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        }

        private static ReceiptResponse GetReponseFromJson(string json)
        {
            return JsonConvert.DeserializeObject<ReceiptResponse>(json);
        }


        private const int SpinWaitTime = 10000;  // ms
        private const int MaxExpoNotificationsInPacket = 100;

        private static Thread mWorkerThread;
        private static Dictionary<string, object> mReceipts = new Dictionary<string, object>();
        private static bool mIsRunning;
        private static bool mExitWithoutFinishing;
        private static bool mFlushAndExit;
        private static readonly object mLock = new object();
    }


    public class ReceiptsRequest
    {
        [JsonProperty("ids")] public IEnumerable<string> Ids { get; set; }
    }

    public class ReceiptResponse
    {
        [JsonProperty("data")] public Dictionary<string, string> Data { get; set; }
    }






    public class PushNotification
    {
        [JsonProperty("to")] public string To { get; set; }
        [JsonProperty("title")] public string Title { get; set; }
        [JsonProperty("body")] public string Body { get; set; }

        [JsonProperty("data")] public string Data { get; set; }        // Max 4KB

        [JsonProperty("ttl")] public long? Ttl { get; set; }          // Time to Live: the number of seconds for which the message may be kept around for redelivery if it hasn't been delivered yet. Defaults to 0.
        [JsonProperty("expiration")] public long? Expiration { get; set; }   // Timestamp since epoch for the notification to expire
        [JsonProperty("sound")] public string Sound { get; set; }

        [JsonProperty("badge")] public int? Badge { get; set; }         // Number to display in the app badge. 0 to clear. 

        [JsonProperty("channelId")] public string ChannelId { get; set; }
    }


    internal class PushResponse
    {
        public PushResponseError[] Errors { get; set; }
        public PushResponseData[] Data { get; set; }
    }

    internal class PushResponseError
    {
        public string Code { get; set; }
        public string Message { get; set; }
    }

    internal class PushResponseData
    {
        public string Id { get; set; }
        public string Status { get; set; }      // "error" | "ok"
        public string Message { get; set; }
        public PushResponseDataDetails Details { get; set; }
    }

    internal class PushResponseDataDetails
    {
        public string Error { get; set; }       // "DeviceNotRegistered"
    }
}
