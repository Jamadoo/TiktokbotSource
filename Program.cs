using OpenQA.Selenium.DevTools.V121.Page;
using RedditBot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Telegram.Bot;
using Telegram.Bot.Types;
using FluentScheduler;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Exceptions;
using GoFileSharp;
using GoFileSharp.Model;
using OpenQA.Selenium.DevTools.V121.DOMSnapshot;
using static System.Net.Mime.MediaTypeNames;
using GoFileSharp.Model.GoFileData;
using GoFileSharp.Model.HTTP;
using GoFileSharp.Controllers;
using GoFileSharp.Extensions;
using GoFileSharp.Interfaces;
using GoFileSharp.Model.GoFileData.Wrappers;
using System.Collections.Concurrent;
using NAudio.Wave;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using OpenQA.Selenium;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Newtonsoft.Json;
using IniParser.Model;
using IniParser;
using IniWrapper.ParserWrapper;
using System.Net;
using System.IO.Compression;
using System.ComponentModel;

namespace RedditBotNew
{
    public class RedditBotScheduler : Registry
    {
        public RedditBotScheduler(List<DateTime> postTimes)
        {
            // RedditCommentBot
            foreach (var Time in postTimes)
            {
                Schedule(() => Program.StartRedditCommentVideo().Wait()).ToRunEvery(0).Weeks().On(Time.DayOfWeek).At(Time.Hour, Time.Minute);
            }
            // Delete Old Videos
            Schedule(() => 
            {
                // Final Videos
                var FolderPath = new DirectoryInfo(new rc_VideoGen().FinalVideosFolder);
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
                var FolderPath = new DirectoryInfo(Program.VFCPath + "/Logs");
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
        public static string RepoPath = (Debugger.IsAttached ? Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.Parent.FullName + "\\Build" : AppDomain.CurrentDomain.BaseDirectory);
        public static string GRPath = RepoPath + "\\GlobalResources";
        public static string VFCPath = RepoPath + "\\VideoFromComment";
        public static string PSPath = RepoPath + "\\SongFromProfile";
        // Files
        static string TiktokUploaderPath = $"{Program.GRPath}/uploader.exe";
        // Get Paths
        public static string FFmpegPath =  GRPath + "/ffmpeg.exe";
        public static string ChromePath = GRPath + "/Chrome/chrome.exe";
        public static string ChromeDriverPath = GRPath + "/chromedriver.exe";

        // Telegram bot
        public static TelegramBotClient botClient = new("7012710556:AAGwv_8jywFmSQFfR4WCegft63JCgEhobiY");
        public static string GroupChatId = "-1002028670804";
        // Base Text
        static string BaseText = "Creating Video\nType:";
        public static string rcBaseText = BaseText + "RedditCommentVideo\nStatus:";
        public static string psBaseText = BaseText + "ProfileSongVideo\nStatus:";

        // Commands
        static string CreateVideoCommand = "/create";
        static string SettingsCommand = "/settings";
        static string NextPostCommand = "/nextpost";
        static string UpdateCommand = "/update";
        static string ExitProgramCommand = "/exit";
        // Other
        static List<DateTime> PostTimes = null;
        static string SelectedConfig = VFCPath;

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
            // Reset Colors
            Console.ForegroundColor = ConsoleColor.Gray;

            // Disable Close Button
            DeleteMenu(GetSystemMenu(GetConsoleWindow(), false), SC_CLOSE, MF_BYCOMMAND);

            // Schedule Tasks
            string jsonData = System.IO.File.ReadAllText(VFCPath + "\\Resources\\PostTimes.JSON");
            PostTimes = JsonConvert.DeserializeObject<List<DateTime>>(jsonData);
            JobManager.Initialize(new RedditBotScheduler(PostTimes));

            // Add Bot Listener
            ReceiverOptions receiverOptions = new()
            {
                AllowedUpdates = Array.Empty<UpdateType>() // receive all update types except ChatMember related updates
            };
            botClient.StartReceiving(
                updateHandler: OnUserMessage,
                pollingErrorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: new CancellationToken()
            );

            // Check if Update
            if (System.IO.File.Exists("Updated.txt"))
            {
                botClient.SendTextMessageAsync(GroupChatId, "Bot Has Been Updated!\nEverything Is Okay");
                System.IO.File.Delete("Updated.txt");
            }

            // Wait
            Console.WriteLine("Video Schedules And Bot Listeners Are Setup...\nEverything Is OK");
            Process.GetCurrentProcess().WaitForExit();
        }

        // --- Telegram Bot Receive --- \\
        static async Task OnUserMessage(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
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
                    Message StatusMessage = await botClient.SendTextMessageAsync(chatId: GroupChatId, text: "Please Spesify Which Video Type To Create\n--- Args ---\n1 - RedditCommentVideo", cancellationToken: new CancellationToken());
                    return;
                }
                // Get Video
                switch (Args[1])
                {
                    case "1":
                        StartRedditCommentVideo();
                        break;
                    case "2":
                        StartProfileToSongVideo();
                        break;
                    default:
                        Message StatusMessage = await botClient.SendTextMessageAsync(chatId: GroupChatId, text: "Invalid Video Type", cancellationToken: new CancellationToken());
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
                                SelectedConfig = VFCPath;
                                break;
                            case 2:
                                SelectedConfig = PSPath;
                                break;
                            default:
                                SelectedConfig = VFCPath;
                                break;
                        }
                        try
                        {
                            await botClient.SendTextMessageAsync(GroupChatId, "Selected Config In Folder: " + SelectedConfig[(SelectedConfig.LastIndexOf("\\") + 1)..]);
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
                            await botClient.SendTextMessageAsync(GroupChatId, "Please Provide a Real Index Number");
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
                    await botClient.SendTextMessageAsync(chatId: GroupChatId, text: HelpMessage, cancellationToken: new CancellationToken());
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
                    await botClient.SendTextMessageAsync(chatId: GroupChatId, text: $"Config File:\n{Properties}", cancellationToken: new CancellationToken());
                    return;
                }
                // Check Remove Key
                if (Args[1].ToLower() == "remove")
                {
                    string Key = Args[2];
                    IniFile["Settings"].RemoveKey(Key);
                    parser.WriteFile(VFCPath + "/config.ini", IniFile);
                    await botClient.SendTextMessageAsync(chatId: GroupChatId, text: $"Removed Key {Key}");
                    return;
                }
                // Set key
                string Setting = Args[1];
                string Value = Args[2];
                IniFile["Settings"][Setting] = Value;
                parser.WriteFile(VFCPath + "/config.ini", IniFile);
                // Send Message
                await botClient.SendTextMessageAsync(chatId: GroupChatId, text: $"Set {Setting} To {Value}", cancellationToken: new CancellationToken());
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
                    await botClient.SendTextMessageAsync(chatId: GroupChatId, text: "Next Post Time: " + NextPost, cancellationToken: new CancellationToken());
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
                    await botClient.SendTextMessageAsync(GroupChatId, "Cant Update Bot While Video Gen Is In Progress");
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
                await botClient.SendTextMessageAsync(GroupChatId, "Bot Is Being Updated");
                // Update From Git
                GitUpdate();
            }
            // Exit Program
            if (update.Message.Text.Substring(0, Math.Min(update.Message.Text.Length, ExitProgramCommand.Length)) == ExitProgramCommand)
            {
                await botClient.SendTextMessageAsync(GroupChatId, "Program Has Been Closed");
                Environment.Exit(0);
            }
        }
        static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(ErrorMessage);
            return Task.CompletedTask;
        }

        // --- Video Focused Functions --- \\
        static public async Task StartRedditCommentVideo()
        {
            VideoGenInProgress++;
            Console.ForegroundColor = ConsoleColor.Gray; 
            Console.WriteLine("--- Starting Creation Of RedditCommentVideo ---");
            // Send Message
            Message StatusMessage = await botClient.SendTextMessageAsync(chatId: GroupChatId, text: rcBaseText + " Initializing Creation", cancellationToken: new CancellationToken());

            // Create Folders
            if (!Directory.Exists(VFCPath + "\\DebugVars"))
            {
                Directory.CreateDirectory(VFCPath + "\\DebugVars");
            }
            if (!Directory.Exists(VFCPath + "\\FinalVideos"))
            {
                Directory.CreateDirectory(VFCPath + "\\FinalVideos");
            }
            if (!Directory.Exists(VFCPath + "\\Logs"))
            {
                Directory.CreateDirectory(VFCPath + "\\Logs");
            }

            // Get Script
            var VideoGen = new rc_VideoGen();

            // Create LogFile
            var LoggerFolderCount = new DirectoryInfo(VFCPath + "/Logs");
            VideoGen.LoggerPath = VFCPath + "/Logs/RedditCommentVideo" + LoggerFolderCount.GetFiles().Length.ToString() + ".txt";
            var Logger = VideoGen.LoggerPath;
            System.IO.File.WriteAllText(Logger, "");
            Console.WriteLine("Log File: " + Logger);
            // Manage Log Updates
            var LogUpdateManager = new LogUpdates(Logger, StatusMessage, VideoGen, rcBaseText);
            VideoGen.rcLogUpdates = LogUpdateManager;

            // Start Script
            var Gen = Task.Run(VideoGen.StartCreation);
            Gen.Wait();

            // Remove Event
            Thread.Sleep(1000);
            // Get Result
            if (VideoGen.IsSuccessful)
            {
                // Modify Caption
                if (VideoGen.VideoCaption[0] == '\"')
                    VideoGen.VideoCaption = VideoGen.VideoCaption[1..];
                if (VideoGen.VideoCaption[VideoGen.VideoCaption.Length-1] == '\"')
                    VideoGen.VideoCaption = VideoGen.VideoCaption[..^1];
                string EscapePattern = @"(?:\\)?[_\*\[\]\(\)~>#\+\-=|{}.!]";
                // Create Succsess Text
                string successText = $"--- Video Created! ---\n\nTitle: {VideoGen.SelectedTitle.Title}\nCaption: `{VideoGen.VideoCaption}`\n\nFinal Video Is Being Uploaded";
                successText = Regex.Replace(successText, EscapePattern, @"\$0");
                // Send Message
                await botClient.DeleteMessageAsync(StatusMessage.Chat.Id, StatusMessage.MessageId);
                StatusMessage = await botClient.SendTextMessageAsync(chatId: GroupChatId, successText, parseMode: ParseMode.MarkdownV2);

                // Upload File
                if (VideoGen.IniFile["Settings"]["ForceGoFile"] == "1")
                {
                    UploadToGoFile(VideoGen.FinalVideoPath, StatusMessage, successText).Wait();
                    return;
                }
                UploadToTiktok(VideoGen.FinalVideoPath, VideoGen.VideoCaption, VideoGen.CookieFile, TiktokUploaderPath, StatusMessage, successText);
            }
            else
            {
                // Display Error
                var DisplayTitle = VideoGen.SelectedTitle == null ? "Unselected" : VideoGen.SelectedTitle.Title;
                try
                {
                    await botClient.EditMessageTextAsync(chatId: StatusMessage.Chat.Id, messageId: StatusMessage.MessageId, "--- An Error Occured While Creating Video ---\nTitle: " + DisplayTitle + "\nError: " + VideoGen.Error, parseMode: ParseMode.Html);
                }
                catch
                {
                    Console.WriteLine("Error Editing Message For Unsuccessfull Video");
                }
            }

            // Cleanup
            VideoGenInProgress--;
            Console.WriteLine("--- Done Creating Video ---\nSuccsess: " + VideoGen.IsSuccessful);
        }

        static public async Task StartProfileToSongVideo()
        {
            VideoGenInProgress++;
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("--- Starting Creation Of ProfileToSong ---");
            // Send Message
            Message StatusMessage = await botClient.SendTextMessageAsync(chatId: GroupChatId, text: psBaseText + " Initializing Creation", cancellationToken: new CancellationToken());

            // Create Folders
            if (!Directory.Exists(PSPath + "\\Logs"))
            {
                Directory.CreateDirectory(PSPath + "\\Logs");
            }
            if (!Directory.Exists(PSPath + "\\FinalVideos"))
            {
                Directory.CreateDirectory(PSPath + "\\FinalVideos");
            }

            // Get Script
            var VideoGen = new ps_VideoGen();

            // Create LogFile
            var LoggerFolderCount = new DirectoryInfo(PSPath + "/Logs");
            VideoGen.LoggerPath = PSPath + "/Logs/ProfileSongVideo" + LoggerFolderCount.GetFiles().Length.ToString() + ".txt";
            var Logger = VideoGen.LoggerPath;
            System.IO.File.WriteAllText(Logger, "");
            Console.WriteLine("Log File: " + Logger);
            // Manage Log Updates
            var LogUpdateManager = new LogUpdates(Logger, StatusMessage, VideoGen, psBaseText);
            VideoGen.psLogUpdates = LogUpdateManager;

            // Start Script
            var Gen = Task.Run(VideoGen.StartCreation);
            Gen.Wait();

            // Remove Event
            Thread.Sleep(1000);
            // Get Result
            if (VideoGen.IsSuccessful)
            {
                // Get Caption
                string VideoCaption = VideoGen.Caption;
                string EscapePattern = @"(?:\\)?[_\*\[\]\(\)~>#\+\-=|{}.!]";
                // Create Succsess Text
                string successText = "--- Video Created! ---\n\nCaption: `" + VideoCaption + "`\n\nFinal Video Is Being Uploaded";
                successText = Regex.Replace(successText, EscapePattern, @"\$0");
                // Send Message
                await botClient.DeleteMessageAsync(StatusMessage.Chat.Id, StatusMessage.MessageId);
                StatusMessage = await botClient.SendTextMessageAsync(chatId: GroupChatId, successText, parseMode: ParseMode.MarkdownV2);

                // Upload File
                UploadToTiktok(VideoGen.FinalVideoPath, VideoCaption, VideoGen.CookieFile, TiktokUploaderPath, StatusMessage, successText);
            }
            else
            {
                // Display Error
                try
                {
                    await botClient.EditMessageTextAsync(chatId: StatusMessage.Chat.Id, messageId: StatusMessage.MessageId, "--- An Error Occured While Creating Video ---\nError: " + VideoGen.Error, parseMode: ParseMode.Html);
                }
                catch
                {
                    Console.WriteLine("Error Editing Message For Unsuccessfull Creaton");
                }
            }

            // Cleanup
            VideoGenInProgress--;
            Console.WriteLine("--- Done Creating Video ---\nSuccsess: " + VideoGen.IsSuccessful);
        }

        // --- Overall Funcitons --- \\
        static void UploadToTiktok(string VideoPath, string Caption, string CookiesPath, string UploaderPath, Message StatusMessage, string SuccsessText)
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
                Arguments = $"\"{VideoPath}\" \"{Caption}\" \"{CookiesPath}\" --headless",
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
            string EscapePattern = @"(?:\\)?[_\*\[\]\(\)~>#\+\-=|{}.!]";
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
                            botClient.EditMessageTextAsync(chatId: StatusMessage.Chat.Id, messageId: StatusMessage.MessageId, SuccsessText, parseMode: ParseMode.MarkdownV2);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Failed To Set Failed Upload Status:\n" + ex.Message);
                        }
                        StopUpdates = true;
                        if (!process.HasExited) process.Kill();
                        UploadToGoFile(VideoPath, StatusMessage, SuccsessText).Wait();
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
                                    botClient.EditMessageTextAsync(chatId: StatusMessage.Chat.Id, messageId: StatusMessage.MessageId,  EscapedText, parseMode: ParseMode.MarkdownV2);
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
            string EscapePattern = @"(?:\\)?[_\*\[\]\(\)~>#\+\-=|{}.!']";
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
                        botClient.EditMessageTextAsync(chatId: StatusMessage.Chat.Id, messageId: StatusMessage.MessageId, $"{successText}\nProgress: {percent}%", parseMode: ParseMode.MarkdownV2);
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
                    await botClient.EditMessageTextAsync(chatId: StatusMessage.Chat.Id, messageId: StatusMessage.MessageId, $"{successText}\n\n{FailText}", parseMode: ParseMode.MarkdownV2);
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
                await botClient.EditMessageTextAsync(chatId: StatusMessage.Chat.Id, messageId: StatusMessage.MessageId, $"{successText}\n\n{FinalText}", parseMode: ParseMode.MarkdownV2);
            }
            catch
            {
                await Task.Delay(31000);
                await botClient.EditMessageTextAsync(chatId: StatusMessage.Chat.Id, messageId: StatusMessage.MessageId, $"{successText}\n\n{FinalText}", parseMode: ParseMode.MarkdownV2);
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
            var StatusMessage = await botClient.SendTextMessageAsync(GroupChatId, $"{UpdateText}File Is Being Downlaoded");
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
                    await botClient.EditMessageTextAsync(GroupChatId, StatusMessage.MessageId, $"{UpdateText}ERROR: Error Occored While Downloading File");
                    return;
                }
                while (!DownloadDone) await Task.Delay(1000);
            }
            // Extract Files
            await botClient.EditMessageTextAsync(GroupChatId, StatusMessage.MessageId, $"{UpdateText}Extracting Files");
            try
            {
                ZipFile.ExtractToDirectory(filePath, RepoPath, true);
            }
            catch(Exception ex)
            {
                await botClient.EditMessageTextAsync(GroupChatId, StatusMessage.MessageId, $"{UpdateText}ERROR: Could Not Extract Files:\n" + ex.Message);
                return;
            }
            // Delete Zip
            System.IO.File.Delete(filePath);
            // Done
            await botClient.EditMessageTextAsync(GroupChatId, StatusMessage.MessageId, "-- Updated --\nBot Files Updated!");
        }
        static void UpdateDownloadProgress(Message StatusMessage, int Progress, string BaseText)
        {
            if (DateTime.Now.Subtract(lastDownloadUpdate).TotalSeconds >= 2)
            {
                lastDownloadUpdate = DateTime.Now;
                botClient.EditMessageTextAsync(GroupChatId, StatusMessage.MessageId, BaseText + "\nProgress: " + Progress);
            }  
        }
    }
}
