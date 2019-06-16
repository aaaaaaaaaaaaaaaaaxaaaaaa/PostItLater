namespace PostItLater
{
    using System;
    using System.Collections.Generic;
    using RestSharp;

    public class SpecializedRedditClient : RedditOAuthClient
    {
        public SpecializedRedditClient(APIKey apikey, string clientId)
            : base(clientId, apikey)
        {
        }

        public void ProcessTask(Task task)
        {
            switch (task.type)
            {
                case "comment":
                    this.Comment(task);
                    return;
                case "self":
                case "link":
                    this.Link(task);
                    return;
                default:
                    Console.Error.WriteLine(string.Format("ERROR"));
                    return;
            }
        }

        private void Link(Task task)
        {
            this.SendRequest("api/submit", Method.POST, new Dictionary<string, string>()
            {
                { "title", task.title },
                { "kind", task.type },
                { "sr", task.thing },
                { task.type == "self" ? "text" : "url", task.content },
            });
        }

        private void Comment(Task task)
        {
            this.SendRequest("api/comment", Method.POST, new Dictionary<string, string>()
            {
                { "text", task.content },
                { "thing_id", task.thing },
            });
        }
    }
}
