using Reddit;
using Reddit.Controllers;
using IniParser;
using IniParser.Model;
using OpenAI_API.Models;
using RedditBotNew;
using Reddit.Inputs.Search;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Newtonsoft.Json;
using System.Text;
using System.Diagnostics;
using NAudio.Wave;
using OpenQA.Selenium.Chrome;
using static System.Net.Mime.MediaTypeNames;
using System.Text.RegularExpressions;
using OpenQA.Selenium.Interactions;
using GoFileSharp.Model.GoFileData;
using System.IO;
using System.Collections.Generic;
using NAudio.Wave.SampleProviders;
using System.Runtime.InteropServices;
using NAudio.Mixer;
using OpenAI_API;
using static OpenAI_API.Audio.TextToSpeechRequest;
using OpenAI_API.Audio;

namespace RedditBot
{
    internal class rc_VideoGen
    {
        // Private Single Use
        static string Path = Program.VFCPath;
        string VoiceoverFolder;
        string ScreenshotFolder;
        int SelectedTitleIndex;
        RedditClient Reddit;
        List<Post> TopPosts = new List<Post>();
        OpenAIAPI api;
        object logLock = new object();
        static int VideoHeight = 1080;
        static int VideoWidth = 570;
        int RunningCommentJobs = 0;

        // Final Lists
        List<ScriptComment> SelectedComments = new List<ScriptComment>();
        Dictionary<string, List<double>> VoiceAndTimings = new Dictionary<string, List<double>>();
        List<List<string>> ScreenshotPaths = new List<List<string>>();

        // Public (Final) Infos
        public List<Comment> FilteredComments = new List<Comment>();
        public string FinalVideoPath;
        public rc_SelectedTitle SelectedTitle;
        public string VideoCaption;
        public string CookieFile = Path + "/Resources/AccountCookies/UpvoteVoices.txt";
        
        // Cross Refrence
        public IniData IniFile = (new FileIniDataParser()).ReadFile(Path + "\\config.ini");
        public string LoggerPath;
        public bool IsSuccessful = false;
        public string Error;
        public LogUpdates rcLogUpdates = null;
        public string FinalVideosFolder = $"{Path}/FinalVideos";

        // Logger
        private string _logAsString;
        public string LogAsString { 
            get { return _logAsString; } 
            set { 
                if (_logAsString != value)
                {
                    _logAsString = value; 
                    rcLogUpdates.HandleLogUpdate(_logAsString); 
                }
            } 
        }

        // --- Startup Functions --- \\
        public void StartCreation()
        {
            // Link Error Handeling
            Console.ForegroundColor = ConsoleColor.Gray;
            IsSuccessful = false;

            try
            {
                /// Start Script
                GetScript();
                GetScreenshots();
                GetVoiceAndTiming().Wait();
                CreateVideoCaption().Wait();
                SaveVaraibles(); // debugging
                CreateVideo().Wait();
                Cleanup();
            }
            catch(Exception ex)
            {
                UnhandledExceptionTrapper(ex);
                return;
            }

            IsSuccessful = true;
            Log("*** --- Done --- ***");
        }

        void UnhandledExceptionTrapper(Exception ex)
        {
            // Display Message
            IsSuccessful = false;
            Error = $"An (Unexpected) Error Occurred:\nMessage {ex.Message}\n\nSource: {ex.Source}\n\nStackTrace: {ex.StackTrace}\n\nCreation has Been Terminated";
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(Error);
            // Delete Folders
            if (Directory.Exists(ScreenshotFolder))
            {
                Directory.Delete(ScreenshotFolder, true);
            }
            if (Directory.Exists(VoiceoverFolder))
            {
                Directory.Delete(VoiceoverFolder, true);
            }
            // Remove Title
            if (SelectedTitle != null && SelectedTitle.Title != "")
            {
                string FilePath = $"{Path}/Resources/UsedTitles.txt";
                var TextFile = File.ReadAllLines(FilePath);
                var NewTextFile = "";
                foreach (string Line in TextFile)
                {
                    if (!Regex.IsMatch(Line, SelectedTitle.Title))
                    {
                        var NewLine = Regex.Replace(Line, "/r|/n", "");
                        NewTextFile += NewLine + "\n";
                    }
                }
                File.WriteAllText(FilePath, NewTextFile);
            }
        }

        // Public For Usage In Other Classes
        public void Log(string message)
        {
            lock (logLock)
            {
                try
                {
                    LogAsString += "\n"+ message;
                    using (FileStream LogFileStream = new FileStream(LoggerPath, FileMode.Append, FileAccess.Write, FileShare.Read))
                    using (StreamWriter LogFileWriter = new StreamWriter(LogFileStream))
                    {
                        LogFileWriter.WriteLine(message);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error logging message, will retry: {ex.Message}");
                    Thread.Sleep(500);
                    Log(message);
                }
            }
        }

        // --- DEBUG FUNCTIONS --- \\
        void LoadVaraibles()
        {
            string jsonData = File.ReadAllText(Path + "/DebugVars/SelectedComments.json");
            SelectedComments = JsonConvert.DeserializeObject<List<ScriptComment>>(jsonData);

            jsonData = File.ReadAllText(Path + "/DebugVars/SelectedTitle.json");
            SelectedTitle = JsonConvert.DeserializeObject<rc_SelectedTitle>(jsonData);

            jsonData = File.ReadAllText(Path + "/DebugVars/VoiceAndTimings.json");
            VoiceAndTimings = JsonConvert.DeserializeObject<Dictionary<string, List<double>>>(jsonData);

            jsonData = File.ReadAllText(Path + "/DebugVars/ScreenshotPaths.json");
            ScreenshotPaths = JsonConvert.DeserializeObject<List<List<string>>>(jsonData);
        }
        void SaveVaraibles()
        {
            string jsonData = JsonConvert.SerializeObject(SelectedComments);
            File.WriteAllText(Path + "/DebugVars/SelectedComments.json", jsonData);

            jsonData = JsonConvert.SerializeObject(SelectedTitle);
            File.WriteAllText(Path + "/DebugVars/SelectedTitle.json", jsonData);

            jsonData = JsonConvert.SerializeObject(VoiceAndTimings);
            File.WriteAllText(Path + "/DebugVars/VoiceAndTimings.json", jsonData);

            jsonData = JsonConvert.SerializeObject(ScreenshotPaths);
            File.WriteAllText(Path + "/DebugVars/ScreenshotPaths.json", jsonData);
        }

        // --- Get Script --- \\
        void GetScript()
        {
            // Display
            Log("--- Scrapping Top 10 Posts ---");
            // Get Top 10 Posts
            Reddit = new RedditClient("HJFoj9C97TXJQDnLIANyhw", "2379623998316-NfFLVzJ6Hjy8I68RD-cWMpiXcrQxWA");
            var AskReddit = Reddit.Subreddit("AskReddit");
            TopPosts = AskReddit.Posts.Hot.Take(10).ToList();
            // Filter Used Posts
            CheckDuplicates(AskReddit.Posts.Hot);
            // Convert To String
            string SelectedPosts = "";
            for (int i = 0; i < TopPosts.Count(); i++)
            {
                string Tag = TopPosts[i].NSFW ? "[NSFW] " : " ";
                SelectedPosts = SelectedPosts + i.ToString() + ". " + Tag + TopPosts[i].Title + "\n";
            }
            Log("Selected Posts:\n"+SelectedPosts);
            /// Get Best Posts (AI)
            Log("-- Asking AI To Select Title");
            SelectedTitleIndex = -1;
            var FoundTitle = false;
            Post SelectedPost = null;
            while (!FoundTitle)
            {
                GetSelectTitle_AI(SelectedPosts).Wait();
                SelectedPost = TopPosts[SelectedTitleIndex];
                if (SelectedPost.Comments.ITop.Count >= 14)
                {
                    FoundTitle = true;
                    break;
                }
                else
                {
                    // Log And Mark Title
                    Log("-- Not Enough Comments On Post, Retrying. Title Will Be Valid in 24h");
                    File.AppendAllLines($"{Path}/Resources/UsedTitles.txt", new List<string> { $"{SelectedPost.Title}||{DateTime.Today.Subtract(TimeSpan.FromDays(3))}" });

                    // Remove Title From List
                    // Filter Used Posts
                    CheckDuplicates(AskReddit.Posts.Hot);
                    // Convert To String
                    SelectedPosts = "";
                    for (int i = 0; i < TopPosts.Count(); i++)
                    {
                        string Tag = TopPosts[i].NSFW ? "[NSFW] " : " ";
                        SelectedPosts = SelectedPosts + i.ToString() + ". " + Tag + TopPosts[i].Title + "\n";
                    }
                }
            }
            SelectedTitle = new rc_SelectedTitle(SelectedPost.Title, SelectedPost.Author, SelectedPost.NSFW);
            // Filter Title
            SelectedTitle.Title = FilterString(SelectedTitle.Title);
            // Display Title
            Log("[TITLE]SelectedTitle: " + SelectedTitle.Title);
            Thread.Sleep(1000); // wait for telegram message to edit
            // Add To Used Titles
            File.AppendAllLines($"{Path}/Resources/UsedTitles.txt", new List<string> { $"{SelectedPost.Title}||{DateTime.Today}" });
            // Get Comments
            Log("--- Scrapping Comments ---");
            FilteredComments.Clear();
            Post ActivePost = TopPosts[SelectedTitleIndex];
            var TopComments = ActivePost.Comments.ITop;
            RunningCommentJobs = 0;
            for (int PostComment = 0; PostComment < Math.Min(18, TopComments.Count-1); PostComment++)
            {
                Log("Starting: " + PostComment.ToString());
                var Comment = TopComments[PostComment];
                var DepthCount = 0;
                Task.Run(() => { AddCommentThread(Comment, DepthCount); });
            }
            Thread.Sleep(1000);
            while (RunningCommentJobs > 0) { Thread.Sleep(1000); }
            // Get Best Comments (AI)
            Log("-- Using AI For Selection");
            // Convert Comments To String
            string CommentBodies = "";
            for (int i = 0; i < FilteredComments.Count; i++)
            {
                CommentBodies = CommentBodies + i.ToString() + ". " + FilteredComments[i].Body + "\n";
            }
            // Ask AI
            SelectedComments.Clear();
            GetSelectComments_AI(CommentBodies, FilteredComments).Wait();
        }
        public static string FilterString(string ToFilter)
        {
            string filePath = Path + "/Resources/SwearWords.txt";

            // Read the list of swear words from the text file
            List<string> swearWords = new List<string>(File.ReadAllLines(filePath));

            // Combine swear words into a single regex pattern
            string Words = string.Join("|", swearWords);
            string regexPattern = $@"\b({Words})\b[\W]?";

            // Use Regex.Replace to replace swear words with asterisks
            string filteredString = Regex.Replace(ToFilter, regexPattern, match =>
            {
                // Inline function to replace vowels in a match with asterisks
                string word = match.Value;
                return Regex.Replace(word, "[aeiouAEIOU]", "*");
            }, RegexOptions.IgnoreCase);

            // Return New String
            return filteredString;
        }
        void CheckDuplicates(List<Post> RedditPosts)
        {
            // Display
            Log("-- Removing Used Titles");
            // Get Used Titles
            var TextFilePath = Path + "/Resources/UsedTitles.txt";
            if (!File.Exists(TextFilePath))
            {
                File.WriteAllText(TextFilePath, "");
            }
            var InfoTitles = File.ReadAllText(TextFilePath);
            List<string> UsedTitles = new List<string>();
            string NewTextFile = "";
            foreach (var InfoTitle in InfoTitles.Split('\n'))
            {
                if (InfoTitle != "")
                {
                    var InfoSplit = InfoTitle.Split("||");
                    if (InfoSplit.Length == 2)
                    {
                        var AddDate = DateTime.Parse(InfoSplit[1]);
                        var f = DateTime.Today.Subtract(AddDate).Days;
                        if (DateTime.Today.Subtract(AddDate).Days < 4)
                        {
                            NewTextFile = NewTextFile + InfoTitle + "\n";
                            UsedTitles.Add(InfoSplit[0]);
                        }
                    }
                }
            }
            // Save Filtered Titles
            File.WriteAllText(TextFilePath, NewTextFile);
            // Check Current Titles
            int i = 0;
            int AddIndex = 11;
            while (i < TopPosts.Count)
            {
                // Check Used
                var CurrentTitle = TopPosts[i].Title;
                var Used = CheckTitle(CurrentTitle, UsedTitles);
                if (Used)
                {
                    // Add New Until Special
                    var RemoveIndex = i;
                    i--;
                    while (Used)
                    {
                        // Display
                        Log("Removing Title " + TopPosts[RemoveIndex].Title);
                        // Remove Title
                        TopPosts.RemoveAt(RemoveIndex);
                        // Add Next
                        TopPosts.Add(RedditPosts[AddIndex]);
                        // Check New
                        Used = CheckTitle(RedditPosts[AddIndex].Title, UsedTitles);
                        RemoveIndex = TopPosts.Count-1;
                        AddIndex++;
                    }
                }
                // Next Element
                i++;
            }
        }
        bool CheckTitle(string CurTitle, List<string> UsedTitles)
        {
            foreach (string Title in UsedTitles)
            {
                if (Title == CurTitle)
                {
                    return true;
                }
            }
            return false;
        }
        bool CheckUsername(string chUsername)
        {
            foreach (var cComment in SelectedComments)
                if (cComment.Username == chUsername) return true;
            return false;
        }
        void AddCommentThread(Comment SearchComment, int DepthCount)
        {
            var ReplyTag = DepthCount >= 1 ? "[REPLY] " : "";
            RunningCommentJobs++;
            try
            {
                SearchComment.Body = ReplyTag + SearchComment.Body;
                FilteredComments.Add(SearchComment);

                if (DepthCount >= 2) { return; }

                var ComReplies = SearchComment.Comments.ITop;
                for (var i = 0; i < Math.Clamp(ComReplies.Count - 1, 0, 3); i++)
                {
                    var iComment = ComReplies[i];
                    var iDepthCount = DepthCount + 1;
                    Task.Run(() => { AddCommentThread(iComment, iDepthCount); });
                }
            }
            catch(Exception ex)
            {
                Log("-- Reddit Error:\n"+ ex.Message + "\n\nRetrying in 30 secs");
                Thread.Sleep(30000);
                AddCommentThread(SearchComment, DepthCount);
            }
            RunningCommentJobs--;
            Task.Run(() => Log("--- Scrapping Comments ---|||Scrapped Comments: " + FilteredComments.Count.ToString()));
        }
        async Task<string> AskAI(string Prompt)
        {
            /// CLAUDE AI
            /*// Setup
            var client = new AnthropicClient(IniFile["Settings"]["ClaudeAPI"]);
            var messages = new List<Message>() { new Message(RoleType.User, Prompt) };
            var parameters = new MessageParameters()
            {
                Messages = messages,
                MaxTokens = 2000,
                Stream = false,
                Temperature = 0.3m,
            };
            // Select Model
            switch (IniFile["Settings"]["ClaudeModle"])
            {
                case "1":
                    parameters.Model = AnthropicModels.Claude3Haiku;
                    break;
                case "2":
                    parameters.Model = AnthropicModels.Claude3Sonnet;
                    break;
                case "3":
                    parameters.Model = AnthropicModels.Claude3Opus;
                    break;
                default:
                    parameters.Model = AnthropicModels.Claude3Sonnet;
                    break;
            }
            // Get Reponse
            MessageResponse response = null;
            try
            {
                response = await client.Messages.GetClaudeMessageAsync(parameters);
            }
            catch(Exception ex)
            {
                throw new Exception("Error While Getting AI response:\n" + ex.Message);
            }
            // Return
            return response.Message.ToString();*/

            /// Chatgpt
            // Setup
            OpenAIAPI api = new OpenAI_API.OpenAIAPI(IniFile["Settings"]["OpenAIAPI"]);
            var chat = api.Chat.CreateConversation();
            chat.Model = Model.GPT4_Omni;
            chat.RequestParameters.Temperature = 0.35;
            // Give Prompt
            chat.AppendUserInput(Prompt);
            // Get Response
            try
            {
                string response = await chat.GetResponseFromChatbotAsync();
                return response;
            }
            catch(Exception Ex)
            {
                throw new Exception("Error While Getting Ai Reponse:\n" + Ex.Message);
            }
        }

        async Task GetSelectTitle_AI(string Titles)
        {
            // Get Prompt
            var Prompt = IniFile["Prompts"]["title_prompt"] + "\n" + Titles;
            // Retry Until You Get a Valid Prompt
            while (SelectedTitleIndex == -1)
            {
                // Create Request
                Log("Genorating Response");
                var Result = await AskAI(Prompt);
                Log("Gen Response: " + Result);
                // Get Title
                try
                { 
                    SelectedTitleIndex = int.Parse(Result.Substring(0, 1));
                    break;
                }
                catch(Exception exp)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Log("-- AI provided wrong format. Retrying!:" + Result + "\n Error:" + exp.Message);
                    Console.ForegroundColor = ConsoleColor.Gray;
                    SelectedTitleIndex = -1;
                }
            }
            
        }
    
        async Task GetSelectComments_AI(string CommentBodies, List<Comment> arr_Comments)
        {
            // Clear Array
            SelectedComments.Clear();
            // Get Prompt
            var Prompt = IniFile["Prompts"]["comment_prompt"] + SelectedTitle.Title + "\n\nHere are the comments for your consideration: \n" + CommentBodies;
            // Get Reponse
            Log("Genorating Response");
            string Result = "";
            try
            {
                Result = await AskAI(Prompt);
            }
            catch(Exception Ex)
            {
                Log("-- An Error Occurred While Asking For Reply. Retrying In 5 secs: " + Ex.Message);
                Thread.Sleep(5000);
                GetSelectComments_AI(CommentBodies, arr_Comments).Wait();
                return;
            }
            Log("Selected Comments: " + Result);
            // Get Comments In Array
            var arr_StringComments = Result.Split("|||").ToList();
            // Check Format
            if (arr_StringComments.Count <= 1)
            {
                Log("-- Incorrect Splitting Format. Retrying");
                GetSelectComments_AI(CommentBodies, arr_Comments).Wait();
                return;
            }
            // Add To Selected Comments
            Log("-- Attempting Read Ai Comments");
            foreach (string cAiComment in arr_StringComments)
            {
                string AiComment = cAiComment.Trim();
                if (!string.IsNullOrEmpty(AiComment))
                {
                    // Set Vars
                    int SelectIndex;

                    // Use regex to extract all sequences of digits
                    var matches = Regex.Matches(cAiComment, @"\d+");
                    if (matches.Count > 0)
                    {
                        // Get the first match, which is the first sequence of digits
                        SelectIndex = int.Parse(matches[0].Value);
                        Console.WriteLine("Extracted First Number: " + matches[0].Value);
                    }
                    else
                    {
                        Log("Incorrect Format By AI, retrying");
                        GetSelectComments_AI(CommentBodies, arr_Comments).Wait();
                        return;
                    }
                    if (AiComment.Length <= 2)
                    {
                        Log("AI Did Not Provide Full Comment. Full Returned Reponse: " + AiComment);
                        GetSelectComments_AI(CommentBodies, arr_Comments).Wait();
                        return;
                    }
                    Log("Converted Index: " + SelectIndex.ToString());
                    // Get Unique User
                    bool bFoundUser = false;
                    Comment CurrentComment = null;
                    SelectIndex--;
                    int tries = 0;
                    while (!bFoundUser)
                    {
                        if (tries >= 10) break;
                        SelectIndex++;
                        tries++;

                        if (SelectIndex >= arr_Comments.Count()) SelectIndex = 0;
                        bFoundUser = !CheckUsername(arr_Comments[SelectIndex].Author);
                        if (!bFoundUser) Log("Duplicate User, Getting Next\nCurSelectIndex: " + (SelectIndex));
                    }
                    CurrentComment = arr_Comments[SelectIndex];
                    // Get User Data
                    string IconImg;
                    try
                    {
                        List<User> User = Reddit.SearchUsers(new SearchGetSearchInput(CurrentComment.Author));
                        if (User != null && User.Count() >= 1)
                        {
                            IconImg = User[0].IconImg;
                        }
                        else
                        {
                            IconImg = "https://www.redditstatic.com/avatars/defaults/v2/avatar_default_" + (new Random()).Next(0, 8) + ".png";
                        }
                    }
                    catch
                    {
                        IconImg = "https://www.redditstatic.com/avatars/defaults/v2/avatar_default_" + (new Random()).Next(0, 8) + ".png";
                    }
                    // Add SelectedComment
                    ScriptComment sc_NewComment = new ScriptComment(AiComment[(Regex.Match(AiComment, @"[a-zA-Z]").Index)..], CurrentComment.Author, IconImg);
                    SelectedComments.Add(sc_NewComment);
                }
            }

            Log("-- Total Comments: " + SelectedComments.Count);
        }

        // --- Get Audio And Timings --- \\
        async Task TextToSpeech(string text, string SavePath)
        {
            try
            {
                api = new OpenAIAPI(IniFile["Settings"]["OpenAIAPI"]);
                var request = new TextToSpeechRequest()
                {
                    Input = text,
                    ResponseFormat = ResponseFormats.MP3,
                    Model = Model.TTS_Speed,
                    Voice = Voices.Alloy,
                    Speed = 1
                };
                await api.TextToSpeech.SaveSpeechToFileAsync(request, SavePath);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Log("--- Error Occorred. Waiting 20 secs");
                Console.WriteLine("Error Happened While Calling Retrying In 20 Secs: " + ex.Message);
                Thread.Sleep(21000);
                await TextToSpeech(text, SavePath);
            }
        }
        async Task GetVoiceAndTiming()
        {
            Log("--- Creating Voice And Timings ---");
            // Create TempVoice Folder
            var BaseFolder = new DirectoryInfo(Path);
            var FolderPath = "VoiceoverTemp" + BaseFolder.GetDirectories().Count().ToString();
            VoiceoverFolder = BaseFolder.CreateSubdirectory(FolderPath).FullName;
            // Say Title
            VoiceAndTimings.Clear();
            // OpenAI
            var TitleVoicePath = VoiceoverFolder + "\\Title.mp3";
            await TextToSpeech(SelectedTitle.Title, TitleVoicePath);
            // Get length and Add To List
            var TitleTimings = new List<double>();
            var NAudioTitle = new Mp3FileReader(TitleVoicePath);
            TitleTimings.Add(NAudioTitle.TotalTime.TotalSeconds);
            VoiceAndTimings.Add(TitleVoicePath, TitleTimings);
            NAudioTitle.Close();

            // Say Comments
            var i = 1;
            foreach (ScriptComment CurrentComment in SelectedComments)
            {
                Log("-- Saying Comment: " + i.ToString());
                var ListSentences = CurrentComment.ReturnSentences();
                List<double> Timings = new List<double>();
                List<byte> CommentClip = new List<byte>();
                string CommentVoicePath = VoiceoverFolder + "\\" + i.ToString() + ".mp3";
                foreach (string Sentence in ListSentences)
                {                    
                    var ModifiedSentence = Sentence;
                    if (Sentence == ListSentences[Math.Clamp(ListSentences.Count - 1, 0, ListSentences.Count)])
                    {
                        ModifiedSentence = Sentence + "...";
                    }
                    ModifiedSentence = ModifiedSentence.Replace("*", "");
                    // Get Audio
                    Log("Sentence: " + ModifiedSentence);
                    var AudioPath = VoiceoverFolder + "\\SentenceAudio.mp3";
                    await TextToSpeech(ModifiedSentence, AudioPath);
                    // Get Length
                    var NAudioComment = new Mp3FileReader(AudioPath);
                    Timings.Add(NAudioComment.TotalTime.TotalSeconds);
                    NAudioComment.Close();

                    // If File Not Exsist (first Sentence)
                    if (!File.Exists(CommentVoicePath))
                    {
                        // Create File On First Sentence
                        File.Copy(AudioPath, CommentVoicePath);
                    }
                    else
                    {
                        // Merge
                        using (var memoryStream = new MemoryStream())
                        {
                            // Combine the audio clips into the memory stream
                            CombineAudioClips(new string[] { CommentVoicePath, AudioPath }, memoryStream);
                            memoryStream.Seek(0, SeekOrigin.Begin);

                            using (var fileStream = new FileStream(
                                       CommentVoicePath,
                                       FileMode.Create, // This will overwrite if file exists
                                       FileAccess.Write))
                            {
                                memoryStream.CopyTo(fileStream);
                                fileStream.Flush();
                                fileStream.Close();
                            }
                            memoryStream.Close();
                        }
                    }
                }

                VoiceAndTimings.Add(CommentVoicePath, Timings);
                i++;
            }
            
        }
        public static void CombineAudioClips(string[] inputFiles, Stream output)
        {
            foreach (string file in inputFiles)
            {
                Mp3FileReader reader = new Mp3FileReader(file);
                if ((output.Position == 0) && (reader.Id3v2Tag != null))
                {
                    output.Write(reader.Id3v2Tag.RawData, 0, reader.Id3v2Tag.RawData.Length);
                }
                Mp3Frame frame;
                while ((frame = reader.ReadNextFrame()) != null)
                {
                    output.Write(frame.RawData, 0, frame.RawData.Length);
                }
                reader.Close();
            }
        }

        // --- Get Screenshots --- \\
        Boolean WaitUntilElementVisible(IWebElement Element, int timeout, ChromeDriver Driver)
        {
            // Get the value of the 'complete' property of the image element
            var script = "return arguments[0].complete";
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(timeout));
            try
            {
                return wait.Until(driver =>
                {
                    try
                    {
                        // Execute JavaScript to check if the image is fully loaded
                        return (bool)((IJavaScriptExecutor)driver).ExecuteScript(script, Element);
                    }
                    catch (NoSuchElementException)
                    {
                        // Return false if the element is not found (considered not loaded)
                        return false;
                    }
                    catch (StaleElementReferenceException)
                    {
                        // Return false if the element reference is stale (considered not loaded)
                        return false;
                    }
                });
            }
            catch (WebDriverTimeoutException)
            {
                // Return false if the timeout occurs (considered not loaded)
                return false;
            }
        }
        void GetScreenshots()
        {
            Log("---  Creating HTML Docs ---");
            // Create Screenshot Folder
            var BaseFolder = new DirectoryInfo(Path);
            var FolderPath = "ScreenshotTemp" + BaseFolder.GetDirectories().Count().ToString();
            ScreenshotFolder = BaseFolder.CreateSubdirectory(FolderPath).FullName;
            // Open Chrome
            var Options = new ChromeOptions();
            Options.BinaryLocation = Program.ChromePath;
            Options.AddArguments(new string[] { $"-window-size={VideoWidth},2000", "-headless" });
            ChromeDriver Driver = null;
            int Attempts = 0;
            while (Driver == null)
            {
                try
                {
                    Driver = new ChromeDriver(Program.ChromeDriverPath, Options);
                    break;
                }
                catch(Exception error)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Log("Error Happened While Starting Driver, retrying.\nError: " + error.Message);
                    Console.ForegroundColor = ConsoleColor.Gray;

                    if (Attempts > 5)
                    {
                        throw new Exception("Error While Creating Chrome Driver, after 5 attempts:\n" + error.Message);
                    }

                    Attempts++;
                }
            }
            Driver.Navigate().GoToUrl("https://wills-dynamite-site-3e18f2.webflow.io");
            /// Get Elements
            var jsExecutor = (IJavaScriptExecutor)Driver;
            //Title
            var TitleElement = Driver.FindElement(By.XPath("//*[@typ='PostTitle']"));
            var AutherElement = Driver.FindElement(By.XPath("//*[@typ='PostAuther']"));
            var TitleBox = Driver.FindElement(By.XPath("//*[@typ='TitleBox']"));
            // Title Extra
            var ExtraPostTime = Driver.FindElement(By.XPath("//*[@typ='PostTimestamp']"));
            var ExtraUpVotes = Driver.FindElement(By.XPath("//*[@typ='PostUp']"));
            var ExtraComments = Driver.FindElement(By.XPath("//*[@typ='CommentCount']"));
            var ExtraPostIcon = Driver.FindElement(By.XPath("//*[@typ='PostIcon']"));
            var ExtraNSFWTag = Driver.FindElement(By.XPath("//*[@typ='NSFWTag']"));
            // Comments
            var CommentBox = Driver.FindElement(By.XPath("//*[@typ='CommentBox']"));
            var UsernameElement = Driver.FindElement(By.XPath("//*[@typ='CommentAuther']"));
            var PfpElement = Driver.FindElement(By.XPath("//*[@typ='CommentPfp']"));
            var ParagrthElement = Driver.FindElement(By.XPath("//*[@typ='CommentBody']"));
            var UsernameDiv = Driver.FindElement(By.XPath("//*[@typ='UsernameDiv']"));
            var ExtraCommentTime = Driver.FindElement(By.XPath("//*[@typ='CommentTimestamp']"));
            // Set Text
            var R = new Random();
            ChangeInnerText(jsExecutor, SelectedTitle.Title, TitleElement);
            ChangeInnerText(jsExecutor, SelectedTitle.Username, AutherElement);
            ChangeInnerText(jsExecutor, "• " + R.Next(2,10).ToString() + " hr. ago", ExtraPostTime);
            ChangeInnerText(jsExecutor, (R.Next(10, 90) / 10.0).ToString() + "K", ExtraUpVotes);
            ChangeInnerText(jsExecutor, (R.Next(10, 30) / 10.0).ToString() + "K", ExtraComments);
            // Set NSFWTag
            if (!SelectedTitle.NSFW)
            {
                Driver.ExecuteScript("arguments[0].remove();", ExtraNSFWTag);
            }
            // Wait Site To Load
            Log("Waiting For Page To Load");
            WaitUntilElementVisible(ExtraPostIcon, 5, Driver);
            // Take Title Screenshot
            Log("-- Saving Title Screenshot");
            string TitlePath = ScreenshotFolder + "\\Title.png";
            Screenshot sc = ((ITakesScreenshot)TitleBox).GetScreenshot();
            sc.SaveAsFile(TitlePath);
            ScreenshotPaths.Clear();
            ScreenshotPaths.Add(new List<string> { TitlePath });
            // Loop through comments
            int CommentIndex = 1;
            foreach (ScriptComment CurrentCommnet in SelectedComments)
            {
                Log("-- Saving Comment " + CommentIndex.ToString());
                // Set Info
                ChangeInnerText(jsExecutor, CurrentCommnet.Username, UsernameElement);
                jsExecutor.ExecuteScript("arguments[0].innerText = '';", ParagrthElement);
                ChangeInnerText(jsExecutor, "• " + R.Next(1, 5).ToString() + " hr. ago", ExtraCommentTime);
                // Set Pfp
                jsExecutor.ExecuteScript("arguments[0].setAttribute('src', '"+ CurrentCommnet.PfpAddress + "');", PfpElement);
                WaitUntilElementVisible(PfpElement, 5, Driver);
                // Setup Loop
                ITakesScreenshot CommentSc = (ITakesScreenshot)CommentBox;
                string CurrentMessage = "";
                string CommentPath;
                int SentenceIndex = 0;
                // Loop throught Sentences
                List<string> CommentScreenshots = new List<string>();
                bool RefreshNeeded = false;
                var CommentSentences = CurrentCommnet.ReturnSentences();
                foreach (string Sentence in CommentSentences)
                {
                    // Set Message
                    CurrentMessage = CurrentMessage + Sentence;
                    ChangeInnerText(jsExecutor, CurrentMessage, ParagrthElement);
                    // Determine If Para Reset
                    if (CommentBox.Size.Height >= 200)
                    {
                        // Check Remaining Chars
                        var RemainingChars = 0;
                        for (int i = SentenceIndex+1; i < CommentSentences.Count; i++)
                        {
                            RemainingChars = RemainingChars + CommentSentences[i].Length;
                        }
                        // Continue If Good
                        if (RemainingChars >= 70)
                        {
                            // Set Vars
                            RefreshNeeded = true;
                            CurrentMessage = Sentence;
                            // Hide Elements
                            Driver.ExecuteScript("arguments[0].style.marginTop = \"--58px\";", UsernameDiv);
                            Driver.ExecuteScript("arguments[0].style.display = \"none\";", PfpElement);
                            // Remove Top Line Breaks
                            while (CurrentMessage.Substring(0,1) == "\n")
                            {
                                CurrentMessage = CurrentMessage.Substring(1);
                            }
                        }
                    }
                    ChangeInnerText(jsExecutor, CurrentMessage, ParagrthElement);
                    /// Save Screenshot
                    CommentPath = $"{ScreenshotFolder}\\{CommentIndex}_{SentenceIndex}.png";
                    bool Succsess = false;
                    while (!Succsess)
                    {
                        try
                        {
                            CommentSc.GetScreenshot().SaveAsFile(CommentPath);
                            Succsess = true;
                        }
                        catch(Exception error)
                        {
                            Succsess = false;
                            Console.ForegroundColor = ConsoleColor.Red;
                            Log(Error);
                            throw new Exception("Error While Saving Screenshot: \n" + error.Message + "\n\nClick The Enter Key To Retry");
                        }

                    }

                    // Add To List
                    CommentScreenshots.Add(CommentPath);
                    // Increase Number
                    SentenceIndex++;
                }
                ScreenshotPaths.Add(CommentScreenshots);
                // Add Index
                CommentIndex++;
                // Refresh
                // Show Hidden Elements
                if (RefreshNeeded)
                {
                    Driver.ExecuteScript("arguments[0].style.marginTop = \"-30px\";", UsernameDiv);
                    Driver.ExecuteScript("arguments[0].style.display = \"Block\";", PfpElement);
                }
            }
            // Close Driver
            Driver.Close();
        }
        void ChangeInnerText(IJavaScriptExecutor jsExecutor, string Text, IWebElement Element)
        {
            string encodedText = (string)jsExecutor.ExecuteScript("return encodeURIComponent(arguments[0]);", Text);
            string script = "arguments[0].innerText = decodeURIComponent('" + encodedText.Replace("'", "\\'") + "');";
            jsExecutor.ExecuteScript(script, Element);
        }

        // --- Create Video Caption--- \\
        async Task CreateVideoCaption()
        {
            // Get Script
            string Script = "";
            Log("-- Creating Caption");
            foreach (ScriptComment Comment in SelectedComments)
            {
                Script += Comment.Message;
            }
            // Create Prompt
            string Prompt = $"{IniFile["Prompts"]["vfCaption_prompt"]}\n\nHere Is The Title And Script You Will Be Using\nTItle: {SelectedTitle.Title}\nScript:\n{Script}";
            // Get Caption
            VideoCaption = await AskAI(Prompt);
            Log("Video Caption:\n" + VideoCaption);
        }

        // --- Create Video --- \\
        async Task CreateVideo()
        {
            Log("--- Creating Video ---");
            // Get Background Video
            Log("-- Geting bgVideo");
            string bgVideoPath = await ChooseGenre("BackgroundVideos", "bgVideo_prompt");
            // Get Background Song(s)
            Log("-- Geting bgSong");
            string bgAudioPath = await ChooseGenre("BackgroundMusic", "bgAudio_prompt");
            // Genorate ffmeg command
            Log("-- Getting Command");
            var Command = GenerateFFmpegCommand(bgVideoPath, bgAudioPath);
            Log("FFmpeg Command:\n" + Command);
            // Run Command
            Log("-- Running Command");
            RunFFmpegCommand(Command);
            // Check If Succses
            if (string.IsNullOrEmpty(FinalVideoPath) || !File.Exists(FinalVideoPath))
            {
                IsSuccessful = false;
                throw new Exception("Error Occored While Saving Video. Please Check Logs For More Details");
            }
        }

        async Task<string> ChooseGenre(string FolderName, string PromptName)
        {
            string Genres = "";
            var bgElements = new DirectoryInfo($"{Path}/VideoResources/{FolderName}");
            int GenreIndex = 1;
            var AllGenres = bgElements.GetDirectories();
            foreach (var Genre in AllGenres)
            {
                Genres += $"{GenreIndex}. {Genre.Name}\n";
                GenreIndex++;
            }
            // Get Script
            string Script = "";
            foreach (ScriptComment Comment in SelectedComments)
            {
                Script += Comment.Message;
            }
            // Create Prompt
            var Prompt = $"{IniFile["Prompts"][PromptName]}\nGenres To Choose From:\n{Genres}\n\nTitle: {SelectedTitle.Title}\n\nScript: {Script}";
            // Get Response
            Log("Selecting Genre");
            var ElementIndex = -1;
            while (ElementIndex == -1)
            {
                // Create Request
                var Result = await AskAI(Prompt);
                // Convert To Int
                try
                {
                    ElementIndex = int.Parse(Result.Substring(0, 1));
                    break;
                }
                catch (Exception exp)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Log("AI provided wrong format. Retrying!:" + Result + "\n Error:" + exp.Message);
                    Console.ForegroundColor = ConsoleColor.Gray;
                    ElementIndex = -1;
                }
            }
            // Get Path
            var bgElementFolder = new DirectoryInfo(AllGenres[ElementIndex-1].FullName);
            // Get Random Element
            var AllElements = bgElementFolder.GetFiles();
            var bgChosenElement = AllElements[(new Random()).Next(0, AllElements.Length-1)];
            Log($"Chosen Element: {bgChosenElement.Name}");
            // Return 
            return bgChosenElement.FullName;
        }

        void RunFFmpegCommand(string Command)
        {
            // Call FFmeg
            // Your FFmpeg command
            string ffmpegCommand = Command;

            // Start FFmpeg process
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = Program.FFmpegPath,
                Arguments = ffmpegCommand,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = false
            };

            Process process = new Process
            {
                StartInfo = psi
            };

            process.Start();
            process.WaitForExit();
        }

        string GenerateFFmpegCommand(string backgroundVideo, string BackgroundSong)
        {
            // Create Parts
            StringBuilder Inputs = new StringBuilder();
            StringBuilder OverlayFilter = new StringBuilder();
            string DelayFilter = "";
            string BaseFilters = "";
            string OutputString = "";
            string FinalCommand = "";

            // Base command with background video and music
            Inputs.Append($"-stream_loop -1 -i \"{backgroundVideo}\" -stream_loop -1 -i \"{BackgroundSong}\" ");

            // Add Screenshots
            var VoiceStartIndex = 2;
            int TotalScreenshots = 0;
            foreach (var Paths in ScreenshotPaths)
            {
                foreach (string Path in Paths)
                {
                    Inputs.Append($"-i \"{Path}\" ");
                    VoiceStartIndex++;
                    TotalScreenshots++;
                }
            }
            // Add Voiceovers
            foreach (var Path in VoiceAndTimings.Keys)
            {
                Inputs.Append($"-i \"{Path}\" ");
            }

            /// Create Filter
            // Overlay Filter
            int CShotIndex = 2;
            string CurrentMainIn = "bgVideo";
            double CurrentTime = 0;
            int i = 1;
            List<double> VoiceoverLengths = new List<double>();
            foreach (List<double> CommentTimings in VoiceAndTimings.Values)
            {
                // Add Sentence Screenshots
                var CommentStartTime = CurrentTime;
                //var SentenceIndex = 1;
                foreach (double SentenceTime in CommentTimings)
                {
                    // Format Timings
                    string StartTime = CurrentTime.ToString().Replace(',', '.');
                    string EndTime = (CurrentTime + SentenceTime).ToString().Replace(',', '.');
                    // Determene StreamName (if we are at the last comment's last screenshot)
                    string StreamName = i == TotalScreenshots ? "OverlayStream" : $"overlay{CShotIndex}";
                    // Add Filters
                    OverlayFilter.Append($"[{CShotIndex}]scale=w={VideoWidth}:h={VideoWidth}/a[Scale{CShotIndex}],[Scale{CShotIndex}]format=argb,colorchannelmixer=aa=0.8[TransOverlay{CShotIndex}],[{CurrentMainIn}][TransOverlay{CShotIndex}]overlay=(W/2)-(w/2):(H/2)-(h/2)-200:enable='between(t,{StartTime},{EndTime})'[{StreamName}],");
                    // Updates Vars
                    CurrentMainIn = $"overlay{CShotIndex}";
                    CurrentTime = CurrentTime + SentenceTime;
                    CShotIndex++;
                    i++;
                }

                // Add To VoiceoverLengths (with 1 Second Gap)
                VoiceoverLengths.Add((CurrentTime - CommentStartTime));
                // Second Delay Between each comment
                //CurrentTime++;
            }
            double TotalVideoLength = CurrentTime;
            // Voiceover Filter
            StringBuilder DelayStreams = new StringBuilder();
            StringBuilder AudioInputStreams = new StringBuilder();
            double NextDelayTime = 0;
            int CurrStreamIndex = VoiceStartIndex;
            string OutputAudio = $"Voiceover{CurrStreamIndex}";
            for (int CurrElementIndex = 0; CurrElementIndex <= VoiceAndTimings.Keys.Count - 1; CurrElementIndex++)
            {
                // Create StreamList
                AudioInputStreams.Append($"[{OutputAudio}]");
                // Create Delay Filter
                DelayStreams.Append($"[{CurrStreamIndex}:a]adelay={(NextDelayTime*1000).ToString().Replace(',', '.')}[{OutputAudio}],");
                // Add Delay
                NextDelayTime = NextDelayTime + VoiceoverLengths[CurrElementIndex];
                // Update Vars
                CurrStreamIndex++;
                OutputAudio = $"Voiceover{CurrStreamIndex}";
            }
            // Combind StringBuilders
            DelayFilter = $"{DelayStreams}{AudioInputStreams}amix=inputs={VoiceAndTimings.Keys.Count}[VoiceOverDelay],[VoiceOverDelay]speechnorm=p=1:e=12.5:r=0.0001[VoiceoverStream],";
            // First Filters
            var bgVideoReader = new MediaFoundationReader(backgroundVideo);
            double bgVideoDur = bgVideoReader.TotalTime.TotalSeconds;
            var bgStartTime = (new Random()).Next(0, (int)Math.Clamp(Math.Floor(bgVideoDur - TotalVideoLength), 0, bgVideoDur));
            // Base Video Filters (volume, mapping)
            BaseFilters = $"[1:a]loudnorm=i=-40[MusicStream],[VoiceoverStream][MusicStream]amix=inputs=2[FinalAudio],";
            // Create Output String (the last bit which is outisde the filter)
            FinalVideoPath = $"{FinalVideosFolder}/{ReplaceInvalidChars(SelectedTitle.Title)}.mp4";
            OutputString = $"-map [OverlayStream] -map [FinalAudio] -t {TotalVideoLength.ToString().Replace(",",".")} -y";
            // Get GPU Command
            bool GPUAccEnabled = IniFile["Settings"]["NividiaGPUAcc"] == "0" ? false : true;
            string GPUStartCom = GPUAccEnabled ? "-hwaccel nvdec " : "";
            string GPUEndCom = GPUAccEnabled ? " -c:v h264_nvenc" : "";

            // Pieve Everything Togetherc
            FinalCommand = $"{GPUStartCom}-ss {bgStartTime} {Inputs}-filter_complex \"[0]crop={VideoWidth}:{VideoHeight}[bgVideo],{OverlayFilter.ToString()}{DelayFilter}{BaseFilters}\" {OutputString}{GPUEndCom} \"{FinalVideoPath}\"";
            return FinalCommand;
        }

        string ReplaceInvalidChars(string filename)
        {
            return string.Join("_", filename.Split(System.IO.Path.GetInvalidFileNameChars()));
        }
        // --- Cleanup --- \\
        void Cleanup()
        {
            Log("--- Cleaning up ---");
            // Delete Folders
            if (Directory.Exists(ScreenshotFolder))
            {
                Directory.Delete(ScreenshotFolder, true);
            }
            if (Directory.Exists(VoiceoverFolder))
            {
                Directory.Delete(VoiceoverFolder, true);
            }
        }
    }
}
