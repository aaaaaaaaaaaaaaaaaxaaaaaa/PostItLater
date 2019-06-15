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
        // Conditions to consider: 
        //  Needing to refresh token
        //  Bad response
        static void Main(string[] args)
        {
            List<Task> pendingTasks = new List<Task>();
            PostItLater pil = new PostItLater();
            Listener listener = new Listener();
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

                    pil.ProcessTask(pendingTasks[i]);
                    pendingTasks.RemoveAt(i);
                }
            }
        }

    }
    class Listener
    {
        List<Task> queue = new List<Task>();
        Thread thread;

        public void Start()
        {
            thread = new Thread(Run);
            thread.Start();
        }
        public bool HasWork()
        {
            lock (queue)
            {
                return queue.Count > 0;
            }
        }
        public Task[] GetWork()
        {
            Task[] tasks;
            lock (queue)
            {
                tasks = queue.ToArray();
                queue.Clear();
            }
            return tasks;
        }
        void Run()
        {
            var httpListener = new HttpListener();
            httpListener.Prefixes.Add("http://localhost:4958/");
            httpListener.Start();
            while (true)
            {
                var context = httpListener.GetContext();
                var request = context.Request;
                var raw_data = new System.IO.StreamReader(request.InputStream, request.ContentEncoding).ReadToEnd();
                var data = JsonConvert.DeserializeObject<Task>(raw_data);
                Console.WriteLine(String.Format("{0} task added", data.type));
                lock (queue)
                {
                    queue.Add(data);
                }
            }
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
    class PostItLater
    {
        readonly static string cfgPath = AppDomain.CurrentDomain.BaseDirectory + "cfg.txt";
        readonly static string clientId = "FPA7sj2DFPNWpQ";
        APIKey apikey;
        RestClient oauth;
        double remainingTokenTime 
        {
            get {
                return (DateTimeOffset.FromUnixTimeSeconds(apikey.tokenExpirationEpoch) - DateTimeOffset.Now).TotalSeconds;
            }
        }
        public PostItLater()
        {
            if (File.Exists(cfgPath))
            {
                var raw_data = File.ReadAllText(cfgPath);
                apikey = JsonConvert.DeserializeObject<APIKey>(raw_data);
            } 
            else
            {
                apikey = new Setup(clientId).Run();
                File.WriteAllText(cfgPath, JsonConvert.SerializeObject(apikey));
            }

            oauth = new RestClient("https://oauth.reddit.com");
            oauth.Authenticator = new HttpBasicAuthenticator(clientId, "");
        }
        public void ProcessTask(Task task)
        {
            if (remainingTokenTime < 60*5)
            {
                RefreshToken();
            }
            switch (task.type)
            {
                case "comment":
                    Comment(task);
                    return;
                case "self":
                case "link":
                    Link(task);
                    return;
                default:
                    Console.Error.WriteLine(String.Format("ERROR"));
                    return;
            }
        }
        RestRequest PrepareRequest(string api, Method method)
        {
            var request = new RestRequest(api, method);
            request.AddHeader("Authorization", "bearer " + apikey.token);
            return request;
        }
        void RefreshToken()
        {
            var client = new RestClient("https://www.reddit.com");
            client.Authenticator = new HttpBasicAuthenticator(clientId, "");
            var request = new RestRequest("api/v1/access_token", Method.POST);
            request.AddParameter("grant_type", "refresh_token");
            request.AddParameter("refresh_token", apikey.refresh);
            var result = client.Execute(request);
            if (result.StatusCode != System.Net.HttpStatusCode.OK)
            {
                Console.Error.WriteLine("Error! Acquring token failed");
                Console.WriteLine(result.Content);

                return;
            }
            var parsed_result = JsonConvert.DeserializeObject<Token>(result.Content);
            apikey = new APIKey(parsed_result.access_token, apikey.refresh, parsed_result.expires_in);
            File.WriteAllText(cfgPath, JsonConvert.SerializeObject(apikey));

            Console.WriteLine(result.Content);
        }

        void Link(Task task)
        {
            var request = PrepareRequest("api/submit", Method.POST);
            request.AddParameter("title", task.title);
            request.AddParameter("kind", task.type);
            request.AddParameter("sr", task.thing);
            request.AddParameter(task.type == "self" ? "text" : "url", task.content);

            var result = oauth.Execute(request);
            dynamic parsedJson = JsonConvert.DeserializeObject(result.Content);
            var json = JsonConvert.SerializeObject(parsedJson, Formatting.Indented);
            Console.WriteLine(json);

            Console.WriteLine(JsonConvert.SerializeObject(task, Formatting.Indented));

        }

        void Comment(Task task)
        {
            var request = PrepareRequest("api/comment", Method.POST);
            request.AddParameter("text", task.content);
            request.AddParameter("thing_id", task.thing);
            var result = oauth.Execute(request);
            dynamic parsedJson = JsonConvert.DeserializeObject(result.Content);
            var json = JsonConvert.SerializeObject(parsedJson, Formatting.Indented);
            Console.WriteLine(json);
        }
    }
}
