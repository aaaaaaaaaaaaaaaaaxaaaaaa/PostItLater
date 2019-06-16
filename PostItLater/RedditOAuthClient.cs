namespace PostItLater
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using RestSharp;
    using RestSharp.Authenticators;

    /// <summary>
    /// This class manages interactions between the program and the reddit API.
    /// </summary>
    public class RedditOAuthClient
    {
        private static readonly string TokenUrl = "https://www.reddit.com";
        private static readonly string OauthUrl = "https://oauth.reddit.com";
        private static readonly string Useragent = string.Format(".net:PostItLater:v{0} (by /u/Lyxica)", Program.Version);
        private readonly string clientId;
        private readonly RestClient client;
        private APIKey apikey;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedditOAuthClient"/> class.
        /// </summary>
        /// <param name="clientId">App id of the reddit app.</param>
        /// <param name="apikey">API key data.</param>
        public RedditOAuthClient(string clientId, APIKey apikey)
        {
            this.apikey = apikey;
            this.client = CreateClient(OauthUrl, clientId);
            this.clientId = clientId;
        }

        /// <summary>
        /// Delegate to notify event subscribers of APIKey update.
        /// </summary>
        /// <param name="apikey">New APIKey data.</param>
        public delegate void APIUpdate(APIKey apikey);

        /// <summary>
        /// This event is raised whenever the APIKey is refreshed.
        /// </summary>
        public event APIUpdate APIKeyUpdated;

        private double RemainingTokenTime
        {
            get
            {
                return (DateTimeOffset.FromUnixTimeSeconds(this.apikey.tokenExpirationEpoch) - DateTimeOffset.Now).TotalSeconds;
            }
        }

        /// <summary>
        /// Send an API request to the reddit server.
        /// </summary>
        /// <param name="endpoint">Endpoint of the REST API.</param>
        /// <param name="method">HTTP Method type.</param>
        /// <param name="parameters">Arguments for the API call.</param>
        /// <returns>If request was successful.</returns>
        protected bool SendRequest(string endpoint, Method method, Dictionary<string, string> parameters = null)
        {
            Console.WriteLine(JsonConvert.SerializeObject(parameters));
            if (this.RemainingTokenTime < 5 * 60) { this.Refresh(); }

            var request = new RestRequest(endpoint, method);
            request.AddHeader("Authorization", "bearer " + this.apikey.token);
            if (parameters != null)
            {
                foreach (var kv in parameters)
                {
                    request.AddParameter(kv.Key, kv.Value);
                }
            }

            var response = this.client.Execute(request);
            Console.WriteLine(response.Content);
            Console.WriteLine(JsonConvert.SerializeObject(this.apikey));
            Console.WriteLine("bearer " + this.apikey.token);
            Console.WriteLine(this.clientId);
            Console.WriteLine(response.ResponseUri);

            return true;
        }

        private static RestClient CreateClient(string baseUrl, string clientId)
        {
            return new RestClient(baseUrl)
            {
                UserAgent = Useragent,
            };
        }

        private void Refresh()
        {
            var client = CreateClient(TokenUrl, this.clientId);
            var request = new RestRequest("api/v1/access_token", Method.POST);
            request.AddParameter("grant_type", "refresh_token");
            request.AddParameter("refresh_token", this.apikey.refresh);
            var result = client.Execute(request);
            if (result.StatusCode != System.Net.HttpStatusCode.OK)
            {
                Console.Error.WriteLine("Error! Acquring token failed");
                Console.WriteLine(result.Content);
                return;
            }

            var parsed_result = JsonConvert.DeserializeObject<Token>(result.Content);
            this.apikey = new APIKey(parsed_result.access_token, this.apikey.refresh, parsed_result.expires_in);
            this.APIKeyUpdated.Invoke(this.apikey);
        }
    }
}
