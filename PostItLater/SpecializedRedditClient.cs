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

        public ResponseCode ProcessTask(Task task)
        {
            switch (task.type)
            {
                case "comment":
                    return this.Comment(task);
                case "self":
                case "link":
                    return this.Link(task);
                default:
                    throw new Exception("Unknown task type");
            }
        }

        private ResponseCode Link(Task task)
        {
            return this.SendRequest("api/submit", Method.POST, new Dictionary<string, string>()
            {
                { "title", task.title },
                { "kind", task.type },
                { "sr", task.thing },
                { task.type == "self" ? "text" : "url", task.content },
            });
        }

        private ResponseCode Comment(Task task)
        {
            return this.SendRequest("api/comment", Method.POST, new Dictionary<string, string>()
            {
                { "text", task.content },
                { "thing_id", task.thing },
            });
        }
    }
}
