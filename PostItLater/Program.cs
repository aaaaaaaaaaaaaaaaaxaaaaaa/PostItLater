namespace PostItLater
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Threading;
    using Newtonsoft.Json;

    public class Program
    {
        /// <summary>
        /// Build version.
        /// </summary>
        public static readonly string Version = "0.1";

        /// <summary>
        /// Location of config file.
        /// </summary>
        public static readonly string CfgPath = AppDomain.CurrentDomain.BaseDirectory + "cfg.txt";

        /// <summary>
        /// Enables verbose console logs.
        /// </summary>
        public static bool Verbose = false;

        private static readonly string ClientId = "FPA7sj2DFPNWpQ";

        // Conditions to consider:
        //  Needing to refresh token
        //  Bad response
        private static void Main(string[] args)
        {
            var largs = new List<string>(args);
            if (largs.Contains("-v")) { Verbose = true; }

            var pendingTasks = new Stack<Task>();
            APIKey apikey;
            if (LoadCfg().HasValue)
            {
                Log.Info("APIKey loaded from config file.");
                apikey = LoadCfg().Value;
            }
            else
            {
                Log.Info("Config file not found, APIKey authorization starting.");
                apikey = new Setup(ClientId).Run();
                Reddit_APIKeyUpdated(apikey);
            }

            var reddit = new SpecializedRedditClient(apikey, ClientId);
            reddit.APIKeyUpdated += Reddit_APIKeyUpdated;
            RequestListener listener = new RequestListener();
            listener.Start();

            while (true)
            {
                Thread.Sleep(5 * 1000);
                var now = DateTimeOffset.Now;
                if (listener.HasWork())
                {
                    var work = listener.GetWork();
                    work.ForEach(e => pendingTasks.Push(e));
                    Log.Info(string.Format("Scheduling {0} task{1}.", work.Count, work.Count > 1 ? "s" : string.Empty));
                }

                for (int i = pendingTasks.Count - 1; i >= 0; i--)
                {
                    var task = pendingTasks.Pop();
                    if (now < DateTimeOffset.FromUnixTimeSeconds(task.epoch)) { continue; }

                    RedditOAuthClient.ResponseCode result;
                    try
                    {
                        result = reddit.ProcessTask(task);
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine(string.Format("Error encounted when trying to perform task: \n {0}", e.Message));
                        continue;
                    }

                    if (result == RedditOAuthClient.ResponseCode.OKAY)
                    {
                        continue;
                    }
                    else if (result == RedditOAuthClient.ResponseCode.RATE_LIMITED)
                    {
                        var minutesToWait = GetRateLimitPeriod(reddit.GetErrorInfo());
                        task.epoch = now.AddMinutes(minutesToWait).ToUnixTimeSeconds();
                        pendingTasks.Push(task);
                        Log.Warn(string.Format("Task due was delayed due to RATE_LIMITED, will try again in {0} minutes", minutesToWait));
                    }
                    else
                    {
                        // UNKNOWN ERROR
                    }
                }
            }
        }

        private static void Reddit_APIKeyUpdated(APIKey apikey)
        {
            Log.Info("APIKey updated, saving to file.");
            File.WriteAllText(CfgPath, JsonConvert.SerializeObject(apikey));
        }

        private static APIKey? LoadCfg()
        {
            if (!File.Exists(CfgPath)) { return null; }

            return JsonConvert.DeserializeObject<APIKey>(File.ReadAllText(CfgPath));
        }

        private static uint GetRateLimitPeriod(string msg)
        {
            var re = new Regex(@"(\d+)");
            var matches = re.Matches(msg);
            return uint.Parse(matches[0].Value);
        }
    }
}