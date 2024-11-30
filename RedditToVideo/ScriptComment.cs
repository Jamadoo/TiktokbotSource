using Reddit.Controllers;
using Reddit.Things;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RedditBotNew.RedditToVideo
{
    internal class ScriptComment
    {
        public string Message { get; set; }
        public string Username { get; set; }
        public string PfpAddress { get; set; }

        public ScriptComment(string message, string username, string pfpAddress)
        {
            Message = message;
            Username = username;
            PfpAddress = pfpAddress;
        }

        private string GetDomainFromUrl(string url)
        {
            Uri uri;
            if (Uri.TryCreate(url, UriKind.Absolute, out uri))
            {
                string host = uri.Host;

                // Remove the subdomain (e.g., "www") if present
                string[] parts = host.Split('.');
                if (parts.Length > 2) // Check if there are more than two parts (subdomain + domain)
                {
                    host = parts[parts.Length - 2] + "." + parts[parts.Length - 1];
                }

                return host;
            }
            return url; // Return the original URL if parsing fails
        }

        public List<string> ReturnSentences()
        {
            List<string> sentences = new List<string>();
            int startIdx = 0;
            var SentenceBreak = "!.?\n";
            string websitePattern = @"((http[s]?|ftp):\/\/)?([w]{3}\.)?([a-zA-Z0-9]+\.[a-zA-Z]{2,})";
            string NumberPattern = @"[0-9]";

            // Remove extra parts from URLs in the Message
            string modifiedComment = Message;
            MatchCollection matches = Regex.Matches(Message, websitePattern);
            foreach (Match match in matches)
            {
                if (Regex.IsMatch(match.Value, @"\w"))
                {
                    string originalUrl = match.Value;
                    originalUrl = Regex.Replace(originalUrl, @"[(\)\[\]{\}]", "");
                    string domain = GetDomainFromUrl(originalUrl);
                    modifiedComment = modifiedComment.Replace(originalUrl, domain);
                }
            }
            modifiedComment = modifiedComment.Replace("\r", "");

            for (int i = 0; i < modifiedComment.Length - 1; i++)
            {
                if (SentenceBreak.Contains(modifiedComment[i]) &&
                    !SentenceBreak.Replace("\n", "").Contains(modifiedComment[i + 1]) &&
                    i - startIdx > 1 &&
                    !(Regex.IsMatch(modifiedComment[i + 1].ToString(), NumberPattern) && Regex.IsMatch(modifiedComment[i - 1].ToString(), NumberPattern)) &&
                    modifiedComment[i + 1] != '\"')
                {
                    var Sentence = modifiedComment.Substring(startIdx, (i - startIdx) + 1);

                    // Do Checks
                    if (Regex.IsMatch(Sentence, @"[a-zA-Z0-9]") && !string.IsNullOrWhiteSpace(Sentence))
                    {
                        // Add To Valids Sentences
                        string FilteredString = RedditToVideoGen.FilterString(Sentence);
                        sentences.Add(FilteredString);
                    }
                    startIdx = i + 1;
                }
            }

            // Add the last sentence if the modifiedComment doesn't end with punctuation
            if (startIdx < modifiedComment.Length)
            {
                int DivirderIndex = modifiedComment.Substring(startIdx).IndexOf("|||");
                int EndIndex = DivirderIndex != -1 ? DivirderIndex : modifiedComment.Length - startIdx;
                string lastSentence = modifiedComment.Substring(startIdx, EndIndex);

                if (!string.IsNullOrEmpty(lastSentence))
                {
                    // Add To Valids Sentences
                    string FilteredString = RedditToVideoGen.FilterString(lastSentence);
                    sentences.Add(FilteredString);
                }
            }

            return sentences;
        }
    }
}
