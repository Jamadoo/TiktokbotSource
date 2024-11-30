using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Exceptions;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;

namespace RedditBotNew
{
    internal class LogUpdates
    {
        // Queue Vars
        static ConcurrentQueue<(string LogString, Message TelegramMessage, TelegramBotClient botClient)> _queue = new ConcurrentQueue<(string, Message, TelegramBotClient)>();
        static SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        // Vars
        static string LogPath;
        static Message StatusMessage;
        static bool EditTimeout = false;

        // Tracker Vars
        string CurText = "";
        string OrignalBaseText = "";

        // --- Log Updates --- \\
        public LogUpdates(string logPath, Message statusMessage, string basetext) 
        {
            LogPath = logPath;
            StatusMessage = statusMessage;
            OrignalBaseText = basetext;
            CurText = OrignalBaseText;
        }

        // --- Handle Updates --- \\
        public void Log(string message)
        {
            try
            {
                using (FileStream LogFileStream = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.Read))
                using (StreamWriter LogFileWriter = new StreamWriter(LogFileStream))
                {
                    LogFileWriter.WriteLine(message);
                }
                HandleTelegramUpdate(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error logging message, will retry: {ex.Message}");
                Thread.Sleep(500);
                Log(message);
            }
        }
        public async Task HandleTelegramUpdate(string LogString)
        {
            // Check Importance
            if (EditTimeout && LogString.Substring(0, 3) != "---")
                return;
            // Add To Queue
            _queue.Enqueue((LogString, StatusMessage, Program.botClient));
            // Wait For Timeout
            while (EditTimeout) { await Task.Delay(1000); }

            // Wait In Queue
            await _semaphore.WaitAsync();
            Message newMessage = null;
            bool gotTask = _queue.TryDequeue(out var eventData);
            if (gotTask)
            {
                try
                {
                    newMessage = await UpdateMessageStatus(eventData.LogString, eventData.TelegramMessage, eventData.botClient);
                }
                catch(Exception ex)
                {
                    // Qeeue Update
                    HandleTelegramUpdate(LogString);
                    // Add Timeout
                    string ExtractedNumber = ex.Message.Substring(ex.Message.Length - 2, ex.Message.Length).Trim();
                    Console.WriteLine("Error While Updating Message Status:\n" + ex.Message);
                    Console.WriteLine("Extracted Numbers: " + ExtractedNumber);
                    int Time;
                    int.TryParse(ExtractedNumber, out Time);
                    if (Time > 0)
                    {
                        EditTimeout = true;
                        await Task.Delay(Time + 1);
                        EditTimeout = false;
                    }
                }
                finally
                {
                    StatusMessage = newMessage == null ? StatusMessage : newMessage;
                    _semaphore.Release();
                }
            }
        }
        async Task<Message> UpdateMessageStatus(string newUpdate, Message TelegramMessage, TelegramBotClient botClient)
        {
            // Safty Check
            if (string.IsNullOrEmpty(newUpdate))
            {
                return TelegramMessage;
            }
            try
            {
                // Get LastUpate
                newUpdate = newUpdate.Replace("|||", "\n");
                // -- RedditToVideoe Title -- \\
                if (newUpdate.Substring(0, Math.Min(7, newUpdate.Length)) == "[TITLE]")
                {
                    string baseText = CurText.Substring(0, CurText.IndexOf("Status") - 1);
                    if (baseText == "") baseText = OrignalBaseText;
                    var Title = newUpdate.Substring(newUpdate.IndexOf(" ") + 1);
                    var newText = baseText + "\nTitle: " + Title + "\n\nStatus:";

                    if (newText != CurText)
                    {
                        var NewMessage = await botClient.EditMessageText(chatId: TelegramMessage.Chat.Id, messageId: TelegramMessage.MessageId, newText);
                        CurText = newText;
                        return NewMessage;
                    }
                }
                
                // -- General -- \\
                if (newUpdate.Substring(0, 2) == "--")
                {
                    string baseText = CurText.Substring(0, CurText.IndexOf("Status") - 1);
                    if (baseText == "") baseText = OrignalBaseText;
                    var newText = baseText + "\nStatus: " + newUpdate;

                    if (newText != CurText)
                    {
                        var NewMessage = await botClient.EditMessageText(chatId: TelegramMessage.Chat.Id, messageId: TelegramMessage.MessageId, newText);
                        CurText = newText;
                        return NewMessage;
                    }
                }
            }
            catch (AggregateException)
            {
                Console.WriteLine("Telegram Timeout");
            }
            return TelegramMessage;
        }
    }
}
