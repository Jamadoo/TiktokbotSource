using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedditBotNew
{
    internal class rc_SelectedTitle
    {
        public string Title { get; set; }
        public string Username { get; set; }
        public bool NSFW { get; set; }

        public rc_SelectedTitle(string title, string username, bool nSFW = false)
        {
            Title = title;
            Username = username;
            NSFW = nSFW;
        }

    }
}
