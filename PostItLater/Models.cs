using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PostItLater
{
    public struct Token
    {
        public string access_token;
        public int expires_in;
        public string scope;
        public string token_type;
    }

    public struct Task
    {
        public string type;
        public uint epoch;
        public string content;
        public string thing;
        public string title;
    }
}
