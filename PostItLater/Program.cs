﻿using System;
using System.Collections.Generic;
using RestSharp;
using RestSharp.Authenticators;
using Newtonsoft.Json;
using System.Net;
using System.Threading;
using System.IO;

namespace PostItLater
{
    class Program
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

            List<Task> pendingTasks = new List<Task>();
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
                if (listener.HasWork()) { pendingTasks.AddRange(listener.GetWork()); }

                for (int i = pendingTasks.Count - 1; i >= 0; i--)
                {
                    var delta = DateTimeOffset.FromUnixTimeSeconds(pendingTasks[i].epoch) - now;
                    if (delta.TotalSeconds > 0) { continue; }

                    reddit.ProcessTask(pendingTasks[i]);
                    pendingTasks.RemoveAt(i);
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
    }
}