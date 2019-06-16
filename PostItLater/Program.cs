using System;
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

        private static readonly string ClientId = "FPA7sj2DFPNWpQ";

        // Conditions to consider:
        //  Needing to refresh token
        //  Bad response
        private static void Main(string[] args)
        {
            List<Task> pendingTasks = new List<Task>();
            APIKey apikey;
            if (LoadCfg().HasValue)
            {
                apikey = LoadCfg().Value;
            }
            else
            {
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
            File.WriteAllText(CfgPath, JsonConvert.SerializeObject(apikey));
        }

        private static APIKey? LoadCfg()
        {
            if (!File.Exists(CfgPath)) { return null; }

            return JsonConvert.DeserializeObject<APIKey>(File.ReadAllText(CfgPath));
        }
    }

    class Setup
    {
        private readonly string landingUrl = "http://localhost:7926/";
        private readonly string clientId;
        private readonly Guid guid = Guid.NewGuid();

        public Setup(string clientId)
        {
            this.clientId = clientId;
        }

        public APIKey Run()
        {
            System.Diagnostics.Process.Start(String.Format("https://www.reddit.com/api/v1/authorize?client_id={0}&" +
                                                    "response_type=code&" +
                                                    "state={2}&" +
                                                    "redirect_uri={1}&" +
                                                    "duration=permanent&" +
                                                    "scope=submit", clientId, landingUrl, guid.ToString()));
            var code = GetCode();
            var token = GetToken(code);
            return new APIKey(token.access_token, token.refresh_token, token.expires_in);
        }
        private string GetCode()
        {
            var httpclient = new HttpListener();
            httpclient.Prefixes.Add(landingUrl);
            httpclient.Start();
            var context = httpclient.GetContext();
            var query = context.Request.QueryString;
            if (query["error"] != null) { Console.Error.WriteLine("Error"); }
            if (query["state"] != guid.ToString()) { Console.Error.WriteLine("Error"); }
            context.Response.Redirect("https://www.reddit.com/prefs/apps/");
            context.Response.Close();
            httpclient.Stop();
            return query["code"];
        }
        private Token GetToken(string code)
        {
            var client = new RestClient("https://www.reddit.com/");
            client.Authenticator = new HttpBasicAuthenticator(clientId, "");
            var request = new RestRequest("api/v1/access_token", Method.POST);
            request.AddParameter("grant_type", "authorization_code");
            request.AddParameter("code", code);
            request.AddParameter("redirect_uri", landingUrl);
            var result = client.Execute(request);
            Console.WriteLine(result.Content);

            var token = JsonConvert.DeserializeObject<Token>(result.Content);
            return token;
        }
    }
}