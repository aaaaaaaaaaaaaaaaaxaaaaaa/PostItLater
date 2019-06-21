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
        /// Location of saved tasks.
        /// </summary>
        public static readonly string TasksPath = AppDomain.CurrentDomain.BaseDirectory + "tasks.txt";

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

            var pendingTasks = new WrappedList<Task>(LoadSavedTasks());
            if (pendingTasks.Count > 0)
            {
                Log.Info(string.Format("Loaded {0} tasks from {1}", pendingTasks.Count, TasksPath), ConsoleColor.DarkGreen);
            }

            pendingTasks.ListChanged += (e, _) => SaveTasks((List<Task>)e);
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
                    pendingTasks.AddRange(work);
                    Log.Info(string.Format("Scheduling {0} task{1}.", work.Count, work.Count > 1 ? "s" : string.Empty));
                }

                for (int i = pendingTasks.Count - 1; i >= 0; i--)
                {
                    if (now < DateTimeOffset.FromUnixTimeSeconds(pendingTasks[i].epoch)) { continue; }
                    var task = pendingTasks.RemoveAt(i);

                    RedditOAuthClient.ResponseCode result;
                    try
                    {
                        result = reddit.ProcessTask(task);
                    }
                    catch (Exception e)
                    {
                        Log.Error(string.Format("Error encounted when trying to perform task: \n {0}", e.Message), true);
                        continue;
                    }

                    if (result == RedditOAuthClient.ResponseCode.OKAY)
                    {
                        Log.Info("Task succesfully posted.", ConsoleColor.DarkGreen);
                    }
                    else if (result == RedditOAuthClient.ResponseCode.RATE_LIMITED)
                    {
                        var minutesToWait = GetRateLimitPeriod(reddit.GetErrorInfo());
                        task.epoch = now.AddMinutes(minutesToWait).ToUnixTimeSeconds();
                        pendingTasks.Add(task);
                        Log.Warn(string.Format("Task due was delayed due to RATE_LIMITED, will try again in {0} minutes", minutesToWait));
                    }
                    else
                    {
                        Log.Error(string.Format("Unknown reddit API error -- ErrorInfo: \n {0}", reddit.GetErrorInfo()), true);
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
            var re = new Regex(@"you are doing that too much\. try again in (\d+) (minutes|seconds)\.");
            var match = re.Match(msg);
            var isMinutes = match.Groups[2].Value == "minutes";
            return isMinutes ? uint.Parse(match.Groups[1].Value) : 1; // Return 1 minute period if RATE_LIMITED timer is below 60 seconds.
        }

        private static List<Task> LoadSavedTasks()
        {
            if (!File.Exists(TasksPath)) { return new List<Task>(); }

            var raw_data = File.ReadAllText(TasksPath);
            return JsonConvert.DeserializeObject<List<Task>>(raw_data) ?? new List<Task>();
        }

        private static void SaveTasks(List<Task> tasks)
        {
            File.WriteAllText(TasksPath, JsonConvert.SerializeObject(tasks));
        }
    }
}