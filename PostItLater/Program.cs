using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RestSharp;
using RestSharp.Authenticators;
using Newtonsoft.Json;
using System.Net;
using System.Threading; 

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

            while(true)
            {
                Thread.Sleep(5 * 1000);
                var now = DateTimeOffset.Now;
                if (listener.HasWork()) { pendingTasks.AddRange(listener.GetWork()); }

                for(int i = pendingTasks.Count - 1; i >= 0; i--)
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

    class PostItLater {
        Token token;
        RestClient oauth;

        public PostItLater()
        {
            oauth = new RestClient("https://oauth.reddit.com");
            oauth.Authenticator = new HttpBasicAuthenticator("iBD7pShu5iNzrg", "TNjpWNy29AvKUw-8i5xyI8jzYs4");
            AcquireToken();
        }
        public void ProcessTask(Task task)
        {
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
            request.AddHeader("Authorization", "bearer " + token.access_token);
            return request;
        }
        void AcquireToken()
        {
            var client = new RestClient("https://www.reddit.com");
            client.Authenticator = new HttpBasicAuthenticator("appid", "secret");
            var request = new RestRequest("api/v1/access_token", Method.POST);
            request.AddParameter("grant_type", "password");
            request.AddParameter("username", "user");
            request.AddParameter("password", "pw");
            var result = client.Execute(request);
            if (result.StatusCode != System.Net.HttpStatusCode.OK)
            {
                Console.Error.WriteLine("Error! Acquring token failed");
                return;
            }
            token = JsonConvert.DeserializeObject<Token>(result.Content);
        }

        void Link(Task task)
        {
            var request = PrepareRequest("api/submit", Method.POST);
            request.AddParameter("title", task.title);
            request.AddParameter("kind", task.type);
            request.AddParameter("sr", task.thing);
            request.AddParameter(task.type == "self"? "text" : "url", task.content);

            var result = oauth.Execute(request);
            dynamic parsedJson = JsonConvert.DeserializeObject(result.Content);
            var json= JsonConvert.SerializeObject(parsedJson, Formatting.Indented);
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
            var json= JsonConvert.SerializeObject(parsedJson, Formatting.Indented);
            Console.WriteLine(json);
            
        }
    }
}
