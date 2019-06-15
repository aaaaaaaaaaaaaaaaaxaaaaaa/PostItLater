using System;

namespace PostItLater
{
    public struct Token
    {
        public string access_token;
        public string token_type;
        public int expires_in;
        public string refresh_token;
        public string scope;
    }

    public struct Task
    {
        public string type;
        public uint epoch;
        public string content;
        public string thing;
        public string title;
    }

    public struct APIKey
    {
        public APIKey(string token, string refresh, int expiration)
        {
            this.token = token;
            this.refresh = refresh;
            this.tokenExpirationEpoch = DateTimeOffset.Now.ToUnixTimeSeconds() + expiration;
        }

        public string token;
        public string refresh;
        public long tokenExpirationEpoch;
    }
}
