using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.IO.Compression;
using RedditBotNew;
using System.Text.RegularExpressions;
using System.Linq;
using IniParser.Model;
using IniParser;
using System.Text;
using System.Web;
using GoFileSharp.Model.GoFileData;
using System.Diagnostics;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium.Interactions;
using System.Xml.Linq;
using NAudio.Wave;
using System.Drawing;
using OpenAI_API;
using OpenAI_API.Models;
using OpenAI_API.Chat;

namespace RedditBotNew.ProfileToVideo
{
    internal class ProfileToVideoGen
    {
        /// Final Infos
        // Public
        public string SelectedPicture = "";
        public string SelectedUser = "";
        public FinalVideo VideoToUpload = new();
        // Private
        string SongPrompt = "";

        // Normale
        static string Path = Program.ptvPath;
        IniData IniFile = (new FileIniDataParser()).ReadFile(Path + "/config.ini");
        string SongsFolderPath = "";
        string WaveFolderPath = "";
        double SongLength = 0;

        // Logger
        public LogUpdates Logger;

        // Cross Refrence
        public bool IsSuccessful = false;
        public string Error;
        public string CookieFile = Path + "/AccountCookies/SongFrames.txt";

        // --- Start Creation --- \\
        public void StartCreation()
        {
            // Set Account Login
            VideoToUpload.InstaEmail = "upvotevoices@gmail.com";
            VideoToUpload.InstaPassword = "69251Janko!!";

            // Link Error Handeling
            Console.ForegroundColor = ConsoleColor.Gray;
            IsSuccessful = false;

            // Start Script
            try
            {
                GetRandomProfile().Wait();
                GetSongPromptAndCaption().Wait();
                GenorateSong().Wait();
                SplitStems();
                CreateWaveforms();
                CreateFinalVideo();
                CheckFFmpegSuccess();
                Cleanup();
            }
            catch (Exception ex)
            {
                UnhandledExceptionTrapper(ex);
                return;
            }

            IsSuccessful = true;
            Logger.Log("*** --- Done --- ***");
        }

        void UnhandledExceptionTrapper(Exception ex)
        {
            // Display Message
            IsSuccessful = false;
            Error = $"An (Unexpected) Error Occurred:\nMessage {ex.Message}\n\nSource: {ex.Source}\n\nStackTrace: {ex.StackTrace}\n\nCreation has Been Terminated";
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(Error);
            // Delete Folders
            //Cleanup();
            return;
        }

        // --- Get Followers --- \\
        async Task GetRandomProfile()
        {
            // Logger.Logger.Log
            Logger.Log("--- Getting Random Follower ---");
            // Get JSON Reponse
            string jsonResponse = "";
            try
            {
                string msToken = await GetmsToken();
                jsonResponse = await GetUserList(msToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                return;
            }

            // Convert To Json
            var jsonObject = JsonConvert.DeserializeObject<dynamic>(jsonResponse);

            // Get Random Followers
            Logger.Log("-- Selecting Random Follower");
            var RandomFollower = jsonObject.userList[new Random().Next(0, jsonObject.userList.Count - 1)];
            SelectedUser = (string)RandomFollower["user"]["uniqueId"];
            SelectedPicture = (string)RandomFollower["user"]["avatarLarger"];
            Console.WriteLine($"Selected: {SelectedUser}\nPfp: {SelectedPicture}");
            Logger.Log($"Selected: {SelectedUser}\nPfp: {SelectedPicture}");
        }
        async Task<string> GetmsToken()
        {
            Logger.Log("-- Getting msToken");
            // Setup
            var cookieContainer = new CookieContainer();
            var handler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = cookieContainer
            };

            // Start Client
            using (var httpClient = new HttpClient(handler))
            {
                // URI
                Uri baseUri = new Uri("https://www.tiktok.com");
                httpClient.BaseAddress = baseUri;

                // Load Cookies
                var AllCookies = File.ReadAllLines(Path + "\\Resources\\msTokenAccount.txt");
                foreach (string Cookie in AllCookies)
                {
                    if (Cookie.StartsWith("#") || string.IsNullOrWhiteSpace(Cookie))
                    {
                        continue;
                    }

                    var parts = Cookie.Split('\t');

                    if (parts.Length < 7)
                    {
                        continue; // Malformed Cookie
                    }

                    var domain = parts[0];
                    var flag = parts[1].Equals("TRUE", StringComparison.InvariantCultureIgnoreCase);
                    var path = parts[2];
                    var secure = parts[3].Equals("TRUE", StringComparison.InvariantCultureIgnoreCase);
                    var expiration = parts[4];
                    var name = parts[5];
                    var value = parts[6];

                    // Parse the expiration time
                    var expires = DateTime.UnixEpoch.AddSeconds(Convert.ToDouble(expiration));

                    var cookie = new System.Net.Cookie(name, value, path, domain)
                    {
                        Secure = secure,
                        Expires = expires,
                        HttpOnly = flag // Usually used to indicate HttpOnly cookies
                    };

                    cookieContainer.Add(cookie);
                }

                // Get Response
                var response = await httpClient.GetAsync("/");

                // Ensure the response is successful
                response.EnsureSuccessStatusCode();

                // Get the cookies from the cookie container
                var cookies = cookieContainer.GetCookies(baseUri);
                System.Net.Cookie msTokenCookie = cookies.FirstOrDefault((c) => c.Name == "msToken", null);

                // Check
                if (msTokenCookie == null) throw new Exception("Could Not Find msToken Cookie");

                // Return
                return msTokenCookie.Value;
            }
        }
        async Task<string> GetUserList(string msToken)
        {
            Logger.Log("-- Getting Follower List");
            // Set the base address for the HTTP client
            HttpClient httpClient = new HttpClient() 
            {
                DefaultRequestVersion = HttpVersion.Version20
            };
            httpClient.BaseAddress = new Uri("https://www.tiktok.com");
            string secUid = "MS4wLjABAAAAnhenNVif3REQOadqIq1CjluFiSb8RnhwkZuvKs4airDqEH-yKiuDAQS1cFNA60BQ"; // Account WHere Getting Followers

            // Build query parameters based on the second request
            var queryParameters = new Dictionary<string, string>
            {
                { "WebIdLastTime", "1714812810" },
                { "aid", "1988" },
                { "app_language", "en" },
                { "app_name", "tiktok_web" },
                { "browser_language", "en-US" },
                { "browser_name", "Mozilla" },
                { "browser_online", "true" },
                { "browser_platform", "Win32" },
                { "browser_version", "5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.6367.118 Safari/537.36" },
                { "channel", "tiktok_web" },
                { "cookie_enabled", "true" },
                { "count", "30" },
                { "device_id", "7365064819043583494" },
                { "device_platform", "web_pc" },
                { "focus_state", "true" },
                { "from_page", "user" },
                { "history_len", "4" },
                { "is_fullscreen", "false" },
                { "is_page_visible", "true" },
                { "maxCursor", "0" },
                { "minCursor", "0" },
                { "os", "windows" },
                { "priority_region", "ZA" },
                { "region", "ZA" },
                { "scene", "67" },
                { "screen_height", "864" },
                { "screen_width", "1536" },
                { "secUid", secUid },
                { "tz_name", "Africa/Johannesburg" },
                { "webcast_language", "en" },
                { "msToken", msToken },
                { "X-Bogus", "DFSzswVL7iJANCsVtljTqGjErrPG" },
                { "_signature", "_02B4Z6wo00001y3A04wAAIDCKzBNliqmL7stwNcAAK1D44" },
                { "referer", "https://www.tiktok.com/@upvotevoices_?lang=en" },
                { "root_referer", "https://www.tiktok.com/@upvotevoices_?lang=en" }
            };

            // Build the query string
            var queryString = System.Web.HttpUtility.ParseQueryString(string.Empty);
            foreach (var param in queryParameters)
            {
                queryString.Add(param.Key, param.Value);
            }

            // Build the request URI
            var requestUri = "/api/user/list/?" + queryString.ToString();

            // Set headers
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/117.0.5938.132 Safari/537.36");
            httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua", "\"Chromium\";v=\"117\", \"Not;A=Brand\";v=\"8\"");
            httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Mobile", "?0");
            httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
            httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");

            // Encoding for Br
            httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));

            // Make the GET request
            var response = await httpClient.GetAsync(requestUri);

            if (response.IsSuccessStatusCode)
            {
                // Check if the response is Brotli-compressed
                if (response.Content.Headers.ContentEncoding.Contains("br"))
                {
                    // Decompress using BrotliStream
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var brotliStream = new BrotliStream(stream, CompressionMode.Decompress))
                    using (var reader = new StreamReader(brotliStream))
                    {
                        return await reader.ReadToEndAsync(); // Decompress and return as string
                    }
                }
                else
                {
                    // If not compressed, read as plain text
                    return await response.Content.ReadAsStringAsync();
                }
            }
            else
            {
                throw new HttpRequestException($"Request failed with status code: {response.StatusCode}");
            }
        }
        
        // --- Get Song Prompt --- \\
        async Task GetSongPromptAndCaption()
        {
            Logger.Log("--- Loading Pfp ---");
            using (var handler = new HttpClientHandler())
            using (var client = new HttpClient(handler))
            {
                // Save Pfp
                Logger.Log("-- Saving Pfp");
                // Create Folder
                var BaseFolder = new DirectoryInfo(Path);
                var FolderPath = "TempWavefroms" + BaseFolder.GetDirectories().Count().ToString();
                WaveFolderPath = BaseFolder.CreateSubdirectory(FolderPath).FullName;
                // Download Pfp
                using (var response = await client.GetAsync(SelectedPicture))
                {
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    {
                        using (var filestream = new FileStream(WaveFolderPath + "\\Pfp.png", FileMode.Create))
                        {
                            await stream.CopyToAsync(filestream);
                        }
                    }
                }

                // Get Song Prompt
                Logger.Log("-- Getting Song Prompt");
                var bytes = await client.GetByteArrayAsync(SelectedPicture);
                string Reponse = await AskAI(IniFile["Prompts"]["AskImage_prompt"], Convert.ToBase64String(bytes));
                SongPrompt = Reponse;
                Logger.Log($"Created Song Prompt For {SelectedUser}:\n{Reponse}");
                Logger.Log("[TITLE] " + Reponse);

                // Get Caption
                string ReponseCaption = await AskAI(IniFile["Prompts"]["Caption_prompt"] + Reponse);
                ReponseCaption = ReponseCaption + $@" @{SelectedUser}";
                // Remove ""
                if (ReponseCaption[0] == '\"')
                    ReponseCaption = ReponseCaption[1..];
                if (ReponseCaption[ReponseCaption.Length - 1] == '\"')
                    ReponseCaption = ReponseCaption[..^1];
                // Add ReponseCaption
                Logger.Log("ReponseCaption: " + ReponseCaption);
                VideoToUpload.VideoCaption = ReponseCaption;
            }
        }
        async Task<string> AskAI(string Prompt, string Image64 = "")
        {
            /// CLAUDE AI
            /*// Setup
            var client = new AnthropicClient(IniFile["Settings"]["ClaudeAPI"]);
            // Create Message
            var Message = new Message(
                    RoleType.User,
                    new TextContent()
                    {
                        Text = Prompt
                    });
            if (Image64 != "")
            {
                Message.Content.Add(new ImageContent()
                {
                    Source = new ImageSource()
                    {
                        MediaType = "image/jpeg",
                        Data = Image64
                    }
                });
            }
            // Create Params
            var messages = new List<Message>() { Message };
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
            catch (Exception ex)
            {
                throw new Exception("Error While Getting AI response:\n" + ex.Message);
            }
            // Return
            return response.Message.ToString();*/

            /// Chatgpt
            // Setup
            OpenAIAPI api = new OpenAIAPI(IniFile["Settings"]["OpenAIAPI"]);
            var chat = api.Chat.CreateConversation();
            chat.Model = Model.GPT4_Omni;
            chat.RequestParameters.Temperature = 0.35;
            // Image And Prompt
            if (!string.IsNullOrEmpty(Image64))
                chat.AppendUserInput(Prompt, new ChatMessage.ImageInput(Image64));
            else
                chat.AppendUserInput(Prompt);
            // Get Response
            try
            {
                string response = await chat.GetResponseFromChatbotAsync();
                return response;
            }
            catch (Exception Ex)
            {
                throw new Exception("Error While Getting Ai Reponse:\n" + Ex.Message);
            }
        }
    
        // --- Genorate Song --- \\
        async Task GenorateSong()
        {
            // Create Folder
            var BaseFolder = new DirectoryInfo(Path);
            var FolderPath = "TempSongs" + BaseFolder.GetDirectories().Count().ToString();
            SongsFolderPath = BaseFolder.CreateSubdirectory(FolderPath).FullName;
            // Path to save the file
            var DownloadPath = $"{SongsFolderPath}\\FinalSong.mp3";
            // Get Song
            Logger.Log($"--- 1/3 Creating Song For {SelectedUser} ---");
            string AudioUrl = await PromptToSong(SongPrompt);
            using (HttpClient client = new HttpClient(new HttpClientHandler()))
            {
                try
                {
                    Logger.Log($"-- 3/3 Downloading Song");
                    // Send a GET request to the specified Uri
                    using (var response = await client.GetAsync(AudioUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode(); // Throw if not a success code.

                        // Read the content into a MemoryStream and then write to file
                        using (var ms = await response.Content.ReadAsStreamAsync())
                        using (var fs = File.Create(DownloadPath))
                        {
                            await ms.CopyToAsync(fs);
                            fs.Flush();
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("Error downloading file: " + ex.Message);
                }
            }
            // Get Song Length
            var naudioSong = new Mp3FileReader(DownloadPath);
            SongLength = naudioSong.TotalTime.TotalSeconds;
            naudioSong.Close();
        }
        async Task<string> PromptToSong(string Prompt)
        {
            var client = new HttpClient(new HttpClientHandler());
            client.BaseAddress = new Uri("https://suno-api2-git-main-jamadoos-projects.vercel.app");

            var requestBody = new
            {
                prompt = Prompt,
                make_instrumental = true,
                wait_audio = false
            };

            // Serialize the object to a JSON string
            string jsonContent = JsonConvert.SerializeObject(requestBody);

            // Create StringContent with the JSON data
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var requestUri = "/api/generate";
            var response = await client.PostAsync(requestUri, content);

            if (response.IsSuccessStatusCode)
            {
                // Desentrolize
                Logger.Log($"-- 2/3 Waiting For SongURL");
                string StrinContent = await response.Content.ReadAsStringAsync();
                var ReponseJSON = JsonConvert.DeserializeObject<dynamic>(StrinContent);
                string ID = ReponseJSON[0]["id"];
                string AudioUrl = "";

                var Paramters = HttpUtility.ParseQueryString(string.Empty);
                Paramters.Add("ids", ID);
                string GetRequest = "/api/get?" + Paramters.ToString();
                while (AudioUrl == "")
                {
                    Console.WriteLine("Calling GET");
                    var GetReponse = await client.GetAsync(GetRequest);

                    if (GetReponse.IsSuccessStatusCode)
                    {
                        string GetContent = await GetReponse.Content.ReadAsStringAsync();
                        var GetJSON = JsonConvert.DeserializeObject<dynamic>(GetContent);
                        AudioUrl = (string)GetJSON[0]["audio_url"];
                    }
                    await Task.Delay(10000);
                }
                Console.WriteLine(AudioUrl);
                return AudioUrl;
            }
            else
            {
                throw new Exception($"Request failed with status code: {response.StatusCode}");
            }
        }
        
        // --- Split Stems --- \\
        void SplitStems()
        {
            Logger.Log("--- Creating Stems ---");
            // Call Spleeter
            // Your FFmpeg command
            string Command = $"-m 5stems -o \"{SongsFolderPath}\\$(TrackName).$(Ext)\" --overwrite \"{SongsFolderPath}\\FinalSong.mp3\"";

            // Start FFmpeg process
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = Path + "\\Resources\\Spleeter\\Spleeter.exe",
                Arguments = Command,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = false
            };

            Process process = new Process
            {
                StartInfo = psi,
                EnableRaisingEvents = true
            };

            var lastProgressUpdate = DateTime.Now;
            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data) && DateTime.Now.Subtract(lastProgressUpdate).TotalSeconds >= 2)
                {
                    string Line = e.Data;
                    var Progress = Regex.Match(Line, "\\[(.*?)\\]");
                    if (Progress.Success)
                    {
                        lastProgressUpdate = DateTime.Now;
                        Logger.Log("--- Creating Stems ---|||Progress: " + Progress.Value);
                    }
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.WaitForExit();
        }

        // --- Create Waveforms --- \\
        void CreateWaveforms()
        {
            Logger.Log("--- Creating Wavefrom Images ---");
            // Loop through folder
            foreach (var file in new DirectoryInfo(SongsFolderPath).GetFiles())
            {
                if (file.Name != "FinalSong.mp3")
                {
                    CallWaveformCommand(file);
                }
            }
        }
        void CallWaveformCommand(FileInfo AudioPath)
        {
            Logger.Log("-- Waveform: " + AudioPath.Name);
            // Call Spleeter
            // Your FFmpeg command
            double PixelsPerSec = 93;
            string Command = 
                $"-i \"{AudioPath.FullName}\" -o \"{WaveFolderPath}\\{AudioPath.Name[..AudioPath.Name.LastIndexOf('.')]}.png\"" +
                $" -w {MathF.Round((float)(PixelsPerSec * SongLength))} -h 52 --pixels-per-second {PixelsPerSec} -b 8" +
                $" --no-axis-labels --waveform-color ffffff --background-color 00000000";

            // Start FFmpeg process
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = Path + "\\Resources\\audiowaveform.exe",
                Arguments = Command,
                UseShellExecute = false,
                CreateNoWindow = false
            };

            Process process = new Process
            {
                StartInfo = psi
            };

            process.Start();
            process.WaitForExit();
        }
        
        // --- Create Video --- \\
        void CreateFinalVideo()
        {
            Logger.Log("--- Creating Final Video ---");
            string Command = GetFFmpegCommand();

            // Start FFmpeg process
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = Program.FFmpegPath,
                Arguments = Command,
                UseShellExecute = false,
                CreateNoWindow = false
            };

            Process process = new Process
            {
                StartInfo = psi
            };

            process.Start();
            process.WaitForExit();
        }
        string GetFFmpegCommand()
        {
            // Get Random ShitPost
            var AllShit = new DirectoryInfo(Path + "\\VideoTemplate\\Shitposts").GetFiles();
            string SelectedShit = AllShit.ElementAt(new Random().Next(0, AllShit.Length - 1)).FullName;

            // Get Inputs
            string Inputs = 
                $"-i \"{Path}\\VideoTemplate\\FlFootage.mp4\" " +
                $"-i  \"{Path}\\VideoTemplate\\Transparent.mov\" " +
                $"-i \"{SongsFolderPath}\\FinalSong.mp3\" " +
                $"-loop 1 -i \"{WaveFolderPath}\\Pfp.png\" " +
                $"-i \"{WaveFolderPath}\\drums.png\" " +
                $"-i \"{WaveFolderPath}\\bass.png\" " +
                $"-i \"{WaveFolderPath}\\other.png\" " +
                $"-i \"{WaveFolderPath}\\piano.png\" " +
                $"-i \"{SelectedShit}\"";

            // Create Filter
            string PathToFont = ($"{Path}\\Resources\\TiktokFont.ttf").Replace("\\", "/").Replace(":", "\\:");

            string FilterComplex = " -filter_complex " +
                // Actual Video
                "\"[4]pad=w=iw:h=ih+22[p1],[5]pad=w=iw:h=ih+22[p2],[6]pad=w=iw:h=ih+22[p3],[7]pad=w=iw:h=ih+22[p4]," +
                "[p1][p2][p3][p4]vstack=inputs=4[stack]," +
                "[1:v][stack]overlay=x=-(t*93)+306:y=157[overlay]," +
                "[0:v][overlay]blend=c0_mode='screen'[FlVideo]," +
                "[2:a]avectorscope=s=333x282:zoom=2,format=yuv420p[Avector]," +
                "[FlVideo][Avector]overlay=x=141:y=490[OverlayAve]," +
                "[2:a]showfreqs=s=489x300:mode=line:fscale=log,format=yuv420p[ShowWaves]," +
                "[OverlayAve][ShowWaves]overlay=x=63:y=756[FinalFl]," +
                "[3:v]scale=w=240:h=240[ScalePfp]," +
                $"[ScalePfp]drawtext=fontfile='{PathToFont}':text='Chosen Follower':fontcolor=white:fontsize=28:box=1:boxcolor=black@0.5:x=(w/2)-(tw/2):y=5:boxborderw=5|240[PfpText]," +
                "[PfpText]format=rgba,fade=out:d=5:alpha=1[Pfp]," +
                "[FinalFl][Pfp]overlay=x=(W/2)-(w/2):y=633[FinalBeatVideo]," +
                // Shitpost
                $"[8:v]scale={VideoToUpload.VideoWidth}:{VideoToUpload.VideoHeight}:force_original_aspect_ratio=decrease,pad={VideoToUpload.VideoWidth}:{VideoToUpload.VideoHeight}:(ow-iw)/2:(oh-ih)/2,setsar=1[ShitPost]," +
                "[ShitPost][8:a][FinalBeatVideo][2:a]concat=n=2:v=1:a=1[FinalVideo][FinalAudio]\"";

            // Create Output
            var DicCount = new DirectoryInfo($"{Path}\\FinalVideos").GetFiles().Length;
            VideoToUpload.VideoPath = $"{Path}\\FinalVideos\\{DicCount + 1}.mp4";
            string OutputString = $" -map [FinalVideo] -map [FinalAudio] -t {Math.Floor(SongLength)} \"{VideoToUpload.VideoPath}\"";

            // Final Command
            return Inputs + FilterComplex + OutputString;
        }
    
        // --- Check FFmpeg Success --- \\
        void CheckFFmpegSuccess()
        {
            if (string.IsNullOrEmpty(VideoToUpload.VideoPath) || !File.Exists(VideoToUpload.VideoPath))
            {
                throw new Exception("FFmpeg Failed To Create Video. Check Console For More Info");
            }
        }

        // --- Cleanup --- \\
        void Cleanup()
        {
            if (Directory.Exists(SongsFolderPath))
            {
                try
                {
                    Directory.Delete(SongsFolderPath, true);
                }
                catch
                {
                    Console.WriteLine("Failed Deleting SongFolder");
                }
            }
            if (Directory.Exists(WaveFolderPath))
            {
                try
                {
                    Directory.Delete(WaveFolderPath, true);
                }
                catch
                {
                    Console.WriteLine("Failed Deleting WaveFolder");
                }
            }
        }
    }
}

