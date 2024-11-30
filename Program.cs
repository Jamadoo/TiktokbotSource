using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using FluentScheduler;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Exceptions;
using GoFileSharp;
using GoFileSharp.Model;
using GoFileSharp.Model.GoFileData;
using GoFileSharp.Model.HTTP;
using GoFileSharp.Controllers;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using IniParser.Model;
using IniParser;
using System.Net;
using System.IO.Compression;
using System.ComponentModel;
using InstagramApiSharp.API.Builder;
using InstagramApiSharp.API;
using InstagramApiSharp.Classes;
using InstagramApiSharp.Logger;
using InstagramApiSharp.Classes.Models;
using InstagramApiSharp.Classes.SessionHandlers;

using RedditBotNew.RedditToVideo;
using RedditBotNew.ProfileToVideo;
using RedditBotNew.TextMessageToVideo;

namespace RedditBotNew
{
    public class RedditBotScheduler : Registry
    {
        public RedditBotScheduler(List<DateTime> postTimes)
        {
            // RedditCommentBot
            foreach (var Time in postTimes)
            {
                Schedule(() => Program.StartRedditToVideo().Wait()).ToRunEvery(0).Weeks().On(Time.DayOfWeek).At(Time.Hour, Time.Minute);
            }
            // Delete Old Videos
            Schedule(() => 
            {
                // Final Videos
                var FolderPath = new DirectoryInfo(new RedditToVideoGen().FinalVideosFolder);
                foreach (var File in FolderPath.GetFiles())
                {
                    if (DateTime.Now.Subtract(File.CreationTime).TotalHours >= 12)
                    {
                        File.Delete();
                    }
                }
            }).ToRunEvery(12).Hours();
            // Delete Old Logs
            Schedule(() =>
            {
                // Final Videos
                var FolderPath = new DirectoryInfo(Program.rtvPath + "/Logs");
                foreach (var File in FolderPath.GetFiles())
                {
                    if (DateTime.Now.Subtract(File.CreationTime).TotalHours >= 25)
                    {
                        File.Delete();
                    }
                }
            }).ToRunEvery(1).Days();
        }
    }

    internal class Program
    {
        // Folders
        public static string RepoPath = (Debugger.IsAttached ? Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.Parent.FullName + "\\Build" : AppDomain.CurrentDomain.BaseDirectory) + "\\PersistentStorage";
        public static string grPath = RepoPath + "\\GlobalResources";
        public static string rtvPath = RepoPath + "\\RedditToVideo";
        public static string ptvPath = RepoPath + "\\ProfileToVideo";
        public static string tsvPath = RepoPath + "\\TextMessageToVideo";
        // Files
        static string TiktokUploaderPath = $"{Program.grPath}/uploader.exe";
        // Get Paths
        public static string FFmpegPath =  grPath + "/ffmpeg.exe";
        public static string ChromePath = grPath + "/Chrome/chrome.exe";
        public static string ChromeDriverPath = grPath + "/chromedriver.exe";

        // Telegram bot
        public static TelegramBotClient botClient = new("7012710556:AAGwv_8jywFmSQFfR4WCegft63JCgEhobiY");
        public static string GroupChatId = "-1002028670804";
        // Base Text
        static string BaseText = "Creating Video\nType:";
        public static string rtvBaseText = BaseText + "RedditCommentVideo\nStatus:";
        public static string ptvBaseText = BaseText + "ProfileSongVideo\nStatus:";

        // Instagram Uploader
        static IInstaApi instaApi;
        static string SeasionPath = rtvPath + "/Resources/InstaSeasions/InstaSeasion.bin";

        // Commands
        static string CreateVideoCommand = "/create";
        static string SettingsCommand = "/settings";
        static string NextPostCommand = "/nextpost";
        static string UpdateCommand = "/update";
        static string ExitProgramCommand = "/exit";
        // Other
        static List<DateTime> PostTimes = null;
        static string SelectedConfig = rtvPath;
        static string EscapePattern = @"(?:\\)?[_\*\[\]\(\)~>#\+\-=|{}.!]";

        // General
        static int VideoGenInProgress = 0;
        static DateTime lastDownloadUpdate = DateTime.Now;

        //  Disable Close Button
        private const int MF_BYCOMMAND = 0x00000000;
        public const int SC_CLOSE = 0xF060;
        [DllImport("user32.dll")]
        public static extern int DeleteMenu(IntPtr hMenu, int nPosition, int wFlags);
        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetConsoleWindow();

        static void Main()
        {
            if (!System.IO.File.Exists(grPath + "\\ffmpeg.exe"))
            {
                Process.GetCurrentProcess().WaitForExit();
            }

            // Reset Colors
            Console.ForegroundColor = ConsoleColor.Gray;

            // Disable Close Button
            DeleteMenu(GetSystemMenu(GetConsoleWindow(), false), SC_CLOSE, MF_BYCOMMAND);

            // Schedule Tasks
            string jsonData = System.IO.File.ReadAllText(rtvPath + "\\Resources\\PostTimes.JSON");
            PostTimes = JsonConvert.DeserializeObject<List<DateTime>>(jsonData);
            JobManager.Initialize(new RedditBotScheduler(PostTimes));

            // Add Bot Listener
            botClient.OnUpdate += OnUserMessage;

            // Check if Update
            if (System.IO.File.Exists("Updated.txt"))
            {
                botClient.SendMessage(GroupChatId, "Bot Has Been Updated!\nEverything Is Okay");
                System.IO.File.Delete("Updated.txt");
            }

            // Wait
            Console.WriteLine("Video Schedules And Bot Listeners Are Setup...\nEverything Is OK");
            Process.GetCurrentProcess().WaitForExit();
        }

        // --- Telegram Bot Receive --- \\
        static async Task OnUserMessage(Update update)
        {
            // Safty Check
            if (update.Message is not { } message || update.Message.Text is not { } messageText || DateTime.UtcNow.Subtract(update.Message.Date).TotalSeconds >= 5)
                return;

            // Create Command
            if (update.Message.Text.Substring(0, Math.Min(update.Message.Text.Length, CreateVideoCommand.Length)) == CreateVideoCommand)
            {
                Console.WriteLine("-- Recieved Command");
                // Get Args
                var Args = update.Message.Text.Split(" ");
                if (Args.Length <= 1)
                {
                    Message StatusMessage = await botClient.SendMessage(chatId: GroupChatId, text: "Please Spesify Which Video Type To Create\n--- Args ---\n1 - RedditToVideo\n2 - ProfileToVideo (broken)\n3 - TextMessageToVideo", cancellationToken: new CancellationToken());
                    return;
                }
                // Get Video
                switch (Args[1])
                {
                    case "1":
                        StartRedditToVideo();
                        break;
                    case "2":
                        StartProfileToVideo();
                        break;
                    case "3":
                        StartTextMessageToVideo();
                        break;
                    default:
                        Message StatusMessage = await botClient.SendMessage(chatId: GroupChatId, text: "Invalid Video Type", cancellationToken: new CancellationToken());
                        break;
                }
            }
            // Config Commands
            if (update.Message.Text.Substring(0, Math.Min(update.Message.Text.Length, SettingsCommand.Length)) == SettingsCommand)
            {
                // Check Args
                var Args = update.Message.Text.Split(" ");
                // Get Ini File
                var parser = new FileIniDataParser();
                IniData IniFile = parser.ReadFile(SelectedConfig + "/config.ini");
                // Select Config
                if (Args.Length == 3 && Args[1].ToLower() == "select")
                {
                    int SelectedIndex = 0;
                    int.TryParse(Args[2], out SelectedIndex);
                    if (SelectedIndex > 0)
                    {
                        switch (SelectedIndex)
                        {
                            case 1:
                                SelectedConfig = rtvPath;
                                break;
                            case 2:
                                SelectedConfig = ptvPath;
                                break;
                            default:
                                SelectedConfig = rtvPath;
                                break;
                        }
                        try
                        {
                            await botClient.SendMessage(GroupChatId, "Selected Config In Folder: " + SelectedConfig[(SelectedConfig.LastIndexOf("\\") + 1)..]);
                        }
                        catch(Exception ex)
                        {
                            Console.WriteLine("Failed To Send Select Config Notice:\n" + ex.Message);
                        }
                    }
                    else
                    {
                        try
                        {
                            await botClient.SendMessage(GroupChatId, "Please Provide a Real Index Number");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Failed To Send Config Notice:\n" + ex.Message);
                        }
                    }
                    return;
                }
                // Show Help
                if (Args.Length == 2 && Args[1].ToLower() == "help")
                {
                    string HelpMessage = 
                        "/settings select [index] - Select VideoType Config" +
                        "/settings help - Show Available Commands\n" +
                        "/settings - Display Config File\n" +
                        "/settings comments - Display Config File With Comments\n" +
                        "/settings remove [KeyName] - Remove Key From Config\n" +
                        "/settings [KeyName] [KeyValue] - Add/Modify Key";
                    await botClient.SendMessage(chatId: GroupChatId, text: HelpMessage, cancellationToken: new CancellationToken());
                    return;
                }
                // List Settings
                if (Args.Length <= 1 || Args[1].ToLower() == "comments")
                {
                    string Properties = "";
                    foreach (var Prop in IniFile["Settings"])
                    {
                        if (!(Prop.KeyName[0] == '#') || (Args.Count() >= 2 && Args[1].ToLower() == "comments"))
                            Properties = $"{Properties}\n{Prop.KeyName} = {Prop.Value}";
                    }
                    await botClient.SendMessage(chatId: GroupChatId, text: $"Config File:\n{Properties}", cancellationToken: new CancellationToken());
                    return;
                }
                // Check Remove Key
                if (Args[1].ToLower() == "remove")
                {
                    string Key = Args[2];
                    IniFile["Settings"].RemoveKey(Key);
                    parser.WriteFile(rtvPath + "/config.ini", IniFile);
                    await botClient.SendMessage(chatId: GroupChatId, text: $"Removed Key {Key}");
                    return;
                }
                // Set key
                string Setting = Args[1];
                string Value = Args[2];
                IniFile["Settings"][Setting] = Value;
                parser.WriteFile(rtvPath + "/config.ini", IniFile);
                // Send Message
                await botClient.SendMessage(chatId: GroupChatId, text: $"Set {Setting} To {Value}", cancellationToken: new CancellationToken());
            }
            // Next Post Command
            if (update.Message.Text.Substring(0, Math.Min(update.Message.Text.Length, NextPostCommand.Length)) == NextPostCommand)
            {
                // Check
                if (PostTimes == null) return;
                // Get Current Time
                double SmallestTime = -1;
                DateTime CurrentTime = DateTime.Now;
                foreach (var Time in PostTimes)
                {
                    if (Time.DayOfWeek == DateTime.Now.DayOfWeek)
                    {
                        double TimeDif = Time.TimeOfDay.Subtract(DateTime.Now.TimeOfDay).TotalSeconds;
                        if (TimeDif <= 0 && (SmallestTime == -1 || TimeDif < Math.Abs(SmallestTime)))
                        {
                            SmallestTime = TimeDif;
                            CurrentTime = Time;
                        }
                    }
                }
                // Get Next Post
                SmallestTime = -1;
                TimeSpan NextPost = TimeSpan.Zero;
                foreach (var Time in PostTimes)
                {
                    double TimeDif = Time.Subtract(CurrentTime).TotalSeconds;
                    if (TimeDif > 0 && (SmallestTime == -1 || TimeDif < SmallestTime))
                    {
                        SmallestTime = TimeDif;
                        NextPost = Time.TimeOfDay;
                    }
                }
                // Give NextPost
                try
                {
                    await botClient.SendMessage(chatId: GroupChatId, text: "Next Post Time: " + NextPost, cancellationToken: new CancellationToken());
                }
                catch(Exception ex)
                {
                    Console.WriteLine("Failed To Send Next Post Message:\n" + ex.Message);
                }
            }
            //  Update
            if (update.Message.Text.Substring(0, Math.Min(update.Message.Text.Length, UpdateCommand.Length)) == UpdateCommand)
            {
                // Check If VideoGenInProgress
                if (VideoGenInProgress > 0 && !Regex.IsMatch(update.Message.Text, " -f"))
                {
                    await botClient.SendMessage(GroupChatId, "Cant Update Bot While Video Gen Is In Progress");
                    return;
                }
                // Check For Update Link
                var Args = update.Message.Text.Split(" ");
                if (Args.Length == 2)
                {
                    // Do File Download Update
                    ZipUpdate(Args[1]).Wait();
                    return;
                }

                /// Or Else Do Git Update
                // Send Telegram Message
                await botClient.SendMessage(GroupChatId, "Bot Is Being Updated");
                // Update From Git
                GitUpdate();
            }
            // Exit Program
            if (update.Message.Text.Substring(0, Math.Min(update.Message.Text.Length, ExitProgramCommand.Length)) == ExitProgramCommand)
            {
                await botClient.SendMessage(GroupChatId, "Program Has Been Closed");
                Environment.Exit(0);
            }
        }

        // --- Video Focused Functions --- \\
        // -- Common Functions
        static async Task<Message> StartNewCreation(string Type)
        {
            VideoGenInProgress++;
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"--- Starting Creation Of {Type} ---");
            // Send Message
            Message StatusMessage = await botClient.SendMessage(chatId: GroupChatId, text: ptvBaseText + " Initializing Creation", cancellationToken: new CancellationToken());
            return StatusMessage;
        }
        static void CreationCleanup(bool IsSuccessfull)
        {
            VideoGenInProgress--;
            Console.WriteLine("--- Done Creating Video ---\nSuccsess: " + IsSuccessfull);
        }
        static void CreateFolders(string Path)
        {
            if (!Directory.Exists(Path + "\\DebugVars"))
            {
                Directory.CreateDirectory(Path + "\\DebugVars");
            }
            if (!Directory.Exists(Path + "\\FinalVideos"))
            {
                Directory.CreateDirectory(Path + "\\FinalVideos");
            }
            if (!Directory.Exists(Path + "\\Logs"))
            {
                Directory.CreateDirectory(Path + "\\Logs");
            }
        }
        static async Task DisplayError(Message StatusMessage, string ErrorText)
        {
            try
            {
                await botClient.EditMessageText(chatId: StatusMessage.Chat.Id, messageId: StatusMessage.MessageId, ErrorText, parseMode: ParseMode.Html);
            }
            catch
            {
                Console.WriteLine("Error Editing Message For Unsuccessfull Video");
            }
        }
        static LogUpdates CreateLogger(Message StatusMessage, string Path, string BaseText)
        {
            string LogFilePath = Path + "/Logs/Log" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".txt";
            System.IO.File.WriteAllText(LogFilePath, "");
            Console.WriteLine("Log File: " + LogFilePath);
            // Manage Log Updates
            var LogUpdateManager = new LogUpdates(LogFilePath, StatusMessage, BaseText);
            return LogUpdateManager;
        }
        static string FixCaption(string Caption)
        {
            if (Caption[0] == '\"')
                Caption = Caption[1..];
            if (Caption[Caption.Length - 1] == '\"')
                Caption = Caption[..^1];
            return Caption;
        }
        // -- Functions
        static public async Task StartRedditToVideo()
        {
            Message StatusMessage = await StartNewCreation("RedditToVideo");

            // Create Folders
            CreateFolders(rtvPath);

            // Get Script
            var VideoGen = new RedditToVideoGen();

            // Create LogFile
            VideoGen.Logger = CreateLogger(StatusMessage, rtvPath, rtvBaseText);

            // Start Script
            var Gen = Task.Run(VideoGen.StartCreation);
            Gen.Wait();

            // Remove Event
            Thread.Sleep(1000);
            // Get Result
            if (VideoGen.IsSuccessful)
            {
                // Modify Caption
                string VideoCaption = FixCaption(VideoGen.VideoToUpload.VideoCaption);
                // Create Succsess Text
                string successText = $"--- Video Created! ---\n\nTitle: {VideoGen.SelectedTitle.Title}\nCaption: `{VideoCaption}`\n\nFinal Video Is Being Uploaded";
                successText = Regex.Replace(successText, EscapePattern, @"\$0");

                // Upload File And Send Telegram Message
                StartUploader(VideoGen, StatusMessage, successText);
            }
            else
            {
                // Display Error
                var DisplayTitle = VideoGen.SelectedTitle == null ? "Unselected" : VideoGen.SelectedTitle.Title;
                var ErrorText = "--- An Error Occured While Creating Video ---\nTitle: " + DisplayTitle + "\nError: " + VideoGen.Error;
                await DisplayError(StatusMessage, ErrorText);
            }

            // Cleanup
            CreationCleanup(VideoGen.IsSuccessful);
        }
        static public async Task StartProfileToVideo()
        {
            Message StatusMessage = await StartNewCreation("ProfileToVideo");

            // Create Folders
            CreateFolders(ptvPath);

            // Get Script
            var VideoGen = new ProfileToVideoGen();

            // Create LogFile
            VideoGen.Logger = CreateLogger(StatusMessage, ptvPath, ptvBaseText);

            // Start Script
            var Gen = Task.Run(VideoGen.StartCreation);
            Gen.Wait();

            // Remove Event
            Thread.Sleep(1000);
            // Get Result
            if (VideoGen.IsSuccessful)
            {
                // Get Caption
                string VideoCaption = FixCaption(VideoGen.VideoToUpload.VideoCaption);
                // Create Succsess Text
                string successText = "--- Video Created! ---\n\nCaption: `" + VideoCaption + "`\n\nFinal Video Is Being Uploaded";
                successText = Regex.Replace(successText, EscapePattern, @"\$0");

                // Upload File And Send Telegram Message
                StartUploader(VideoGen, StatusMessage, successText);
            }
            else
            {
                var ErrorText = "--- An Error Occured While Creating Video ---\nError: " + VideoGen.Error;
                await DisplayError(StatusMessage, ErrorText);
            }

            // Cleanup
            CreationCleanup(VideoGen.IsSuccessful);
        }   
        static public async Task StartTextMessageToVideo()
        {
            // Initinalize Creatoin
            Message StatusMessage = await StartNewCreation("TextMessageToVideo");
            // Create Folders
            CreateFolders(ptvPath);

            // Get Script
            var VideoGen = new TextMessageToVideoGen
            {
                // Create LogFile
                Logger = CreateLogger(StatusMessage, ptvPath, ptvBaseText)
            };

            // Start Script
            var Gen = Task.Run(VideoGen.StartCreation);
            Gen.Wait();

            // Remove Event
            Thread.Sleep(1000);
            // Get Result
            if (VideoGen.IsSuccessful)
            {
                // Get Caption
                string VideoCaption = FixCaption(VideoGen.VideoToUpload.VideoCaption);
                // Create Succsess Text
                string successText = "--- Video Created! ---\n\nCaption: `" + VideoCaption + "`\n\nFinal Video Is Being Uploaded";
                successText = Regex.Replace(successText, EscapePattern, @"\$0");

                // Upload File And Send Telegram Message
                StartUploader(VideoGen, StatusMessage, successText);
            }
            else
            {
                var ErrorText = "--- An Error Occured While Creating Video ---\nError: " + VideoGen.Error;
                await DisplayError(StatusMessage, ErrorText);
            }

            // Cleanup
            CreationCleanup(VideoGen.IsSuccessful);
        }

        // --- Cloud Funcitons --- \\
        // -- Start Uploader
        static async void StartUploader(dynamic VideoGen, Message StatusMessage, string successText)
        {
            // Update Telegram
            await botClient.DeleteMessageAsync(StatusMessage.Chat.Id, StatusMessage.MessageId);
            StatusMessage = await botClient.SendMessage(chatId: GroupChatId, successText, parseMode: ParseMode.MarkdownV2);

            // Wait For Video To Appear
            if (!System.IO.File.Exists(VideoGen.VideoToUpload.VideoPath))
            {
                long StartTime = DateTime.Now.Ticks;
                do { await Task.Delay(1000); } 
                while (StartTime - DateTime.Now.Ticks < 5 
                    && !System.IO.File.Exists(VideoGen.VideoToUpload.VideoPath));

                // Display Error
                if (!System.IO.File.Exists(VideoGen.VideoToUpload.VideoPath))
                {
                    await botClient.EditMessageText(chatId: StatusMessage.Chat.Id, messageId: StatusMessage.MessageId, "--- An Error Occured While Creating Video ---\nCould Not Find Final Video, after 5 seconds of sleeping", parseMode: ParseMode.Html);
                    return;
                }
            }

            if (VideoGen.IniFile["Settings"]["ForceGoFile"] == "1")
            {
                UploadToGoFile(VideoGen.VideoToUpload.VideoPath, StatusMessage, successText).Wait();
                return;
            }
            UploadToInstagram(VideoGen.VideoToUpload, StatusMessage, successText).Wait();
        }

        // -- Upload To Instagram
        static async Task UploadToInstagram(FinalVideo VideoToUpload, Message StatusMessage, string successText)
        {
            Console.WriteLine("--- Uploading To Instagram");
            // Login
            var userSession = new UserSessionData
            {
                UserName = VideoToUpload.InstaEmail,
                Password = VideoToUpload.InstaPassword
            };
            instaApi = InstaApiBuilder.CreateBuilder()
                                       .SetUser(userSession)
                                       .UseLogger(new DebugLogger(InstagramApiSharp.Logger.LogLevel.Exceptions)) // optional logging
                                       .SetSessionHandler(new FileSessionHandler() { FilePath = SeasionPath })
                                       .Build();
            // Load Season
            try
            {
                try
                {
                    if (System.IO.File.Exists(SeasionPath))
                    {
                        Console.WriteLine("Loading seasion from file");
                        using (var fs = System.IO.File.OpenRead(SeasionPath))
                        {
                            instaApi.LoadStateDataFromStream(fs);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }
            catch { }
            // Test Challange
            Console.WriteLine("Checking Auth");
            if (!instaApi.IsUserAuthenticated)
            {
                Console.WriteLine("Not Authed");
                // Send Login
                await instaApi.SendRequestsBeforeLoginAsync();
                await Task.Delay(5000);
                var loginResult = await instaApi.LoginAsync();
                // Test Login
                if (loginResult.Succeeded)
                {
                    // Save Seasion
                    await instaApi.SendRequestsAfterLoginAsync();
                    SaveSession();
                }
                else
                {
                    if (loginResult.Value == InstaLoginResult.ChallengeRequired)
                    {
                        // Try "It's Me" Click
                        try
                        {
                            await instaApi.AcceptChallengeAsync();
                        }
                        catch { Console.WriteLine("Not 'Its Me' Button"); }
                        // Get Challange
                        var challenge = await instaApi.GetChallengeRequireVerifyMethodAsync();
                        if (challenge.Succeeded)
                        {
                            if (challenge.Value.SubmitPhoneRequired)
                            {
                                Console.WriteLine("Phone Needed");
                                await botClient.EditMessageText(chatId: StatusMessage.Chat.Id, messageId: StatusMessage.MessageId, $"{successText}\n\nChallange Required, Phone Number Is Required\\. Uploading To Gofile", parseMode: ParseMode.MarkdownV2);
                                UploadToGoFile(VideoToUpload.VideoPath, StatusMessage, successText).Wait();
                                return;
                            }
                            else
                            {
                                if (!string.IsNullOrEmpty(challenge.Value.StepData.PhoneNumber))
                                {
                                    // Update Bot Message
                                    await botClient.EditMessageText(chatId: StatusMessage.Chat.Id, messageId: StatusMessage.MessageId, $"{successText}\n\nChallange Required, Sms Required", parseMode: ParseMode.MarkdownV2);
                                    // Send Code
                                    var phoneNumber = await instaApi.RequestVerifyCodeToSMSForChallengeRequireAsync();
                                    if (phoneNumber.Succeeded)
                                    {
                                        // Get sms
                                        Console.WriteLine("Sms Code Needed");
                                        Console.WriteLine("Please Input The Sms Code");
                                        string Sms = Console.ReadLine();
                                        await SendVerifcationCode(VideoToUpload.VideoPath, StatusMessage, successText, Sms);
                                    }
                                    else
                                    {
                                        await botClient.EditMessageText(chatId: StatusMessage.Chat.Id, messageId: StatusMessage.MessageId, $"{successText}\n\nLogin Failed\\. Uploading To GoFile", parseMode: ParseMode.MarkdownV2);
                                        UploadToGoFile(VideoToUpload.VideoPath, StatusMessage, successText).Wait();
                                        return;
                                    }
                                }
                                else if (!string.IsNullOrEmpty(challenge.Value.StepData.Email))
                                {
                                    // Update Bot Message
                                    await botClient.EditMessageText(chatId: StatusMessage.Chat.Id, messageId: StatusMessage.MessageId, $"{successText}\n\nChallange Required, Email Is Required", parseMode: ParseMode.MarkdownV2);
                                    // Send Code
                                    var SendEmail = await instaApi.RequestVerifyCodeToEmailForChallengeRequireAsync();
                                    if (SendEmail.Succeeded)
                                    {
                                        // Get email
                                        Console.WriteLine("Email Code Needed");
                                        Console.WriteLine("Please Input The Email Code");
                                        string Code = Console.ReadLine();
                                        await SendVerifcationCode(VideoToUpload.VideoPath, StatusMessage, successText, Code);
                                    }
                                    else
                                    {
                                        await botClient.EditMessageText(chatId: StatusMessage.Chat.Id, messageId: StatusMessage.MessageId, $"{successText}\n\nLogin Failed. Uploading To GoFile", parseMode: ParseMode.MarkdownV2);
                                        UploadToGoFile(VideoToUpload.VideoPath, StatusMessage, successText).Wait();
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            Console.WriteLine("Authed");
            // Create Video
            var Video = new InstaVideoUpload()
            {
                Video = new InstaVideo()
                {
                    Uri = VideoToUpload.VideoPath,
                    Height = VideoToUpload.VideoHeight,
                    Width = VideoToUpload.VideoWidth
                }
            };
            // Post Video
            Console.WriteLine("Uplaoding Video");
            var result = await instaApi.MediaProcessor.UploadVideoAsync(Video, VideoToUpload.VideoCaption);
            Console.WriteLine("Upload Result: " + result.Info);
            // Update Bot
            if (result.Succeeded)
            {
                Console.WriteLine("Uploaded");
                await botClient.EditMessageText(chatId: StatusMessage.Chat.Id, messageId: StatusMessage.MessageId, $"{successText}\n\n\\!\\!\\!Video Uploaded To Instragram\\!\\!\\!", parseMode: ParseMode.MarkdownV2);
            }
            else
            {
                Console.WriteLine("Failed, uploading to gofile");
                string NewsuccessText = $"{successText}\n\nFailed To Upload. Error: {result.Info}\n\nUploading To GoFile";
                NewsuccessText = Regex.Replace(NewsuccessText, EscapePattern, @"\$0");

                await botClient.EditMessageText(chatId: StatusMessage.Chat.Id, messageId: StatusMessage.MessageId, NewsuccessText, parseMode: ParseMode.MarkdownV2);
                UploadToGoFile(VideoToUpload.VideoPath, StatusMessage, NewsuccessText).Wait();
            }
        }

        static async Task SendVerifcationCode(string FinalVideoPath, Message StatusMessage, string successText, string Sms)
        {
            await botClient.EditMessageText(chatId: StatusMessage.Chat.Id, messageId: StatusMessage.MessageId, $"{successText}\n\nSending Entered Code", parseMode: ParseMode.MarkdownV2);

            var verifyLogin = await instaApi.VerifyCodeForChallengeRequireAsync(Sms);
            if (verifyLogin.Succeeded)
            {
                Console.WriteLine("Log In Successfull");
                await botClient.EditMessageText(chatId: StatusMessage.Chat.Id, messageId: StatusMessage.MessageId, $"{successText}\n\nLogged In\\. Resuming Upload\\.", parseMode: ParseMode.MarkdownV2);
                SaveSession();
            }
            else
            {
                await botClient.EditMessageText(chatId: StatusMessage.Chat.Id, messageId: StatusMessage.MessageId, $"{successText}\n\nFailed To Login\\. Uploading To GoFile", parseMode: ParseMode.MarkdownV2);
                UploadToGoFile(FinalVideoPath, StatusMessage, successText).Wait();
                return;
            }
        }

        static void SaveSession()
        {
            var state = instaApi.GetStateDataAsStream();
            using (var fileStream = System.IO.File.Create(SeasionPath))
            {
                state.Seek(0, SeekOrigin.Begin);
                state.CopyTo(fileStream);
            }
        }
        
        // -- Upload To Other Platforms
        static void UploadToTiktok(FinalVideo VideoToUpload, string CookiesPath, string UploaderPath, Message StatusMessage, string SuccsessText)
        {
            List<string> StatusUpdates = new List<string>
            {
                "Create a chrome browser instance",
                "Authenticating browser with cookies",
                "Navigating to upload page",
                "Uploading Video File",
                "Removing split window",
                "Split window not found or operation timed out",
                "Setting interactivity settings",
                "Setting description",
                "Clicking the post button",
                "Video posted successfully"
            };

            // Call Uploader.exe
            // Start Uploader process
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = UploaderPath,
                Arguments = $"\"{VideoToUpload.VideoPath}\" \"{VideoToUpload.VideoCaption}\" \"{CookiesPath}\" --headless",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = false
            };

            Process process = new Process
            {
                StartInfo = psi,
                EnableRaisingEvents = true
            };

            // Event handler for when the process outputs a line to the console
            string PreviousText = "";
            int PreviousStatus = 0;
            TimeSpan LastUpdate = DateTime.Now.TimeOfDay;
            bool StopUpdates = false;
            DateTime LastStatusUpdate = DateTime.Now;
            string PreviousProgressText = "";
            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data) && !StopUpdates)
                {
                    // Check Error Or TimeoutTime
                    if (e.Data.ToLower().Contains("failed to upload") || DateTime.Now.Subtract(LastStatusUpdate).TotalMinutes >= 3.5)
                    {
                        SuccsessText = $"{SuccsessText}\nERROR: An Error Happend File Uploading Video\\. Check Console For More Info\\.\nOn: {PreviousText}\n\nUploading To GoFile\\.";
                        try
                        {
                            botClient.EditMessageText(chatId: StatusMessage.Chat.Id, messageId: StatusMessage.MessageId, SuccsessText, parseMode: ParseMode.MarkdownV2);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Failed To Set Failed Upload Status:\n" + ex.Message);
                        }
                        StopUpdates = true;
                        if (!process.HasExited) process.Kill();
                        UploadToGoFile(VideoToUpload.VideoPath, StatusMessage, SuccsessText).Wait();
                        return;
                    }
                    LastStatusUpdate = DateTime.Now;
                    PreviousText = e.Data;

                    // Get Status Step
                    int CurStatus = 0;
                    for (int i = 0; i <= StatusUpdates.Count - 1; i++)
                    {
                        if (e.Data.ToLower().Contains(StatusUpdates[i].ToLower()))
                        {
                            CurStatus = i;
                            PreviousText = StatusUpdates[i];
                            break;
                        }
                    }
                    // Get New Message
                    string ProgressText = "";
                    bool MainUpdate = false;
                    if (CurStatus >= PreviousStatus)
                    {
                        PreviousStatus = CurStatus;
                        MainUpdate = true;
                        if (CurStatus != StatusUpdates.Count - 1)
                        {
                            ProgressText = $"Progress: {CurStatus + 1}/{StatusUpdates.Count}";
                        }
                        else
                        {
                            ProgressText = $"!!! Video Uploaded !!!";
                        }
                    }
                    if (ProgressText == "") ProgressText = PreviousProgressText;
                    PreviousProgressText = ProgressText;
                    // Update Message
                    try
                    {
                        if (MainUpdate || DateTime.Now.TimeOfDay.Subtract(LastUpdate).TotalSeconds >= 2)
                        {
                            string EscapedText = SuccsessText + Regex.Replace($"\n{ProgressText}\nText: {PreviousText}", EscapePattern, @"\$0");
                            if (EscapedText != StatusMessage.Text)
                            {
                                Console.WriteLine("Update: " + PreviousText);
                                try
                                {
                                    botClient.EditMessageText(chatId: StatusMessage.Chat.Id, messageId: StatusMessage.MessageId,  EscapedText, parseMode: ParseMode.MarkdownV2);
                                }
                                catch(Exception ex)
                                {
                                    Console.WriteLine("Failed To Set Upload Status: " + ex.Message);
                                }
                                LastUpdate = DateTime.Now.TimeOfDay;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error While Updating Upload Progress:\n" + ex.Message);
                    }
                }
            };

            process.Start();
            process.BeginOutputReadLine(); // Start reading lines from output
            process.WaitForExit();
        }
        static async Task UploadToGoFile(string VideoPath, Message StatusMessage, string successText)
        {
            /// Upload File
            // Setup
            var Options = new GoFileOptions
            {
                ApiToken = "DFJGeXxxjZyx9OsauFJsMfpUA0H7xx6a",
                PreferredZone = ServerZone.Europe
            };
            var goFile = new GoFile(Options);
            var fileInfo = new FileInfo(VideoPath);
            // Track Progress
            TimeSpan LastUpdate = DateTime.Now.TimeOfDay;
            TimeSpan TimeoutTime = DateTime.Now.TimeOfDay;
            double Lastprogress = 0;
            var uploadProgress = new Progress<double>((percent) =>
            {
                if (DateTime.Now.TimeOfDay.Subtract(TimeoutTime).TotalSeconds >= 2)
                {
                    try
                    {
                        botClient.EditMessageText(chatId: StatusMessage.Chat.Id, messageId: StatusMessage.MessageId, $"{successText}\nProgress: {percent}%", parseMode: ParseMode.MarkdownV2);
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine("Error Happened While Editing Progress:\n"+ex.Message);
                    }
                    TimeoutTime = DateTime.Now.TimeOfDay;
                }
                LastUpdate = DateTime.Now.TimeOfDay;
                Lastprogress = percent;
            });
            // Start Upload
            var _api = new GoFileController();
            GoFileResponse<UploadInfo> goFileResponse = await _api.UploadFileAsync(fileInfo, Options.PreferredZone, Options.ApiToken, uploadProgress);
            // Check Return
            if (goFileResponse.IsOK == false)
            {
                string FailText = Regex.Replace($"An Error Occored While Uploading: {goFileResponse.Status}", EscapePattern, @"\$0");
                try
                {
                    await botClient.EditMessageText(chatId: StatusMessage.Chat.Id, messageId: StatusMessage.MessageId, $"{successText}\n\n{FailText}", parseMode: ParseMode.MarkdownV2);
                }
                catch
                {
                    Console.WriteLine("Error While Edtiting Mesage For GoFile Error. GoFile Error:\n" + FailText);
                }
                return;
            }
            // Wait For Completion
            while (DateTime.Now.TimeOfDay.Subtract(LastUpdate).TotalSeconds <= 7 && Lastprogress <= 95)
            {
                await Task.Delay(1000);
            }
            /// End

            // Give File
            string LinkText = $"Video Link: {goFileResponse.Data.DownloadPage} (if the video is not shown, please wait a couple seconds)";
            var FinalText = Regex.Replace(LinkText, EscapePattern, @"\$0");
            try
            {
                await botClient.EditMessageText(chatId: StatusMessage.Chat.Id, messageId: StatusMessage.MessageId, $"{successText}\n\n{FinalText}", parseMode: ParseMode.MarkdownV2);
            }
            catch
            {
                await Task.Delay(31000);
                await botClient.EditMessageText(chatId: StatusMessage.Chat.Id, messageId: StatusMessage.MessageId, $"{successText}\n\n{FinalText}", parseMode: ParseMode.MarkdownV2);
            }
        }
        
        // --- Update Project --- \\
        static void GitUpdate()
        {
            // Create the batch script in the user temp folder
            string tempFolder = Path.GetTempPath();
            string batchScriptPath = Path.Combine(tempFolder, "update_and_restart.bat");

            // Write the batch script
            string[] batchScriptContent =
            {
                    "@echo off",
                    "timeout /t 1 > nul", // Add a short delay to give time for the C# process to close
                    $"cd \"{RepoPath}\"", // Change directory to the repository path
                    "git fetch --all",
                    "git reset --hard origin/main", // Execute git pull
                    "timeout /t 3 /nobreak",
                    "echo Bot Updated. > Updated.txt",
                    "start RedditBotNew.exe", // Start the new program
                    "del \"%~f0\"" // Delete itself (the batch file)
                };

            System.IO.File.WriteAllLines(batchScriptPath, batchScriptContent);

            // Execute the batch script in a new Command Prompt
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batchScriptPath}\"", // Run and exit the new Command Prompt
                CreateNoWindow = true, // Don't show a new console window
                UseShellExecute = false
            });

            // Exit program
            Environment.Exit(0);
        }
        static async Task ZipUpdate(string DownloadLink)
        {
            // Download File
            string UpdateText = "-- Updating --\n";
            var StatusMessage = await botClient.SendMessage(GroupChatId, $"{UpdateText}File Is Being Downlaoded");
            var filePath = $"{RepoPath}\\UpdateFolder.zip";
            bool DownloadDone = false;
            using (WebClient wc = new WebClient())
            {
                wc.DownloadProgressChanged += (object sender, DownloadProgressChangedEventArgs e) => UpdateDownloadProgress(StatusMessage, e.ProgressPercentage, $"{UpdateText}File Is Being Downlaoded");
                wc.DownloadFileCompleted += (object sender, AsyncCompletedEventArgs e) => DownloadDone = true;
                try
                {
                    wc.DownloadFileAsync(
                        new Uri(DownloadLink),
                        filePath
                    );
                }
                catch
                {
                    await botClient.EditMessageText(GroupChatId, StatusMessage.MessageId, $"{UpdateText}ERROR: Error Occored While Downloading File");
                    return;
                }
                while (!DownloadDone) await Task.Delay(1000);
            }
            // Extract Files
            await botClient.EditMessageText(GroupChatId, StatusMessage.MessageId, $"{UpdateText}Extracting Files");
            try
            {
                ZipFile.ExtractToDirectory(filePath, RepoPath, true);
            }
            catch(Exception ex)
            {
                await botClient.EditMessageText(GroupChatId, StatusMessage.MessageId, $"{UpdateText}ERROR: Could Not Extract Files:\n" + ex.Message);
                return;
            }
            // Delete Zip
            System.IO.File.Delete(filePath);
            // Done
            await botClient.EditMessageText(GroupChatId, StatusMessage.MessageId, "-- Updated --\nBot Files Updated!");
        }
        static void UpdateDownloadProgress(Message StatusMessage, int Progress, string BaseText)
        {
            if (DateTime.Now.Subtract(lastDownloadUpdate).TotalSeconds >= 2)
            {
                lastDownloadUpdate = DateTime.Now;
                botClient.EditMessageText(GroupChatId, StatusMessage.MessageId, BaseText + "\nProgress: " + Progress);
            }  
        }
    }
}
