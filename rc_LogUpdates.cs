using Microsoft.Extensions.Logging;
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
using RedditBot;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;

namespace RedditBotNew
{
    internal class rc_LogUpdates
    {
        // Queue Vars
        static ConcurrentQueue<(string LogString, Message TelegramMessage, TelegramBotClient botClient, int CommentsCount)> _queue = new ConcurrentQueue<(string, Message, TelegramBotClient, int)>();
        static SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        // Vars
        static string LogPath;
        static Message StatusMessage;
        static rc_VideoGen VideoGen;
        static bool EditTimeout = false;

        // Tracker Vars
        string CurText = Program.RedComBaseText;
        TimeSpan lastEdit = DateTime.Now.TimeOfDay;

        // --- Log Updates --- \\
        public rc_LogUpdates(string logPath, Message statusMessage, rc_VideoGen videoGen) 
        {
            LogPath = logPath;
            StatusMessage = statusMessage;
            VideoGen = videoGen;
        }

        // --- Handle Updates --- \\
        public async Task HandleLogUpdate(string LogString)
        {
            // Check Importance
            if (EditTimeout && LogString.Substring(0, 3) != "---")
                return;
            // Add To Queue
            _queue.Enqueue((LogString, StatusMessage, Program.botClient, VideoGen.FilteredComments.Count));
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
                    newMessage = await UpdateMessageStatus(eventData.LogString, eventData.TelegramMessage, eventData.botClient, eventData.CommentsCount);
                }
                catch(Exception ex)
                {
                    Console.WriteLine("Error While Updating Message Status:\n" + ex.Message);
                    Console.WriteLine("Extracted Numbers: " + ex.Message.Substring(ex.Message.Length - 2, ex.Message.Length).Trim());
                    int Time;
                    int.TryParse(ex.Message.Substring(ex.Message.Length - 2, ex.Message.Length).Trim(), out Time);
                    if (Time != -1)
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
        async Task<Message> UpdateMessageStatus(string LogString, Message TelegramMessage, TelegramBotClient botClient, int CommentsCount)
        {
            // Safty Check
            if (string.IsNullOrEmpty(LogString))
            {
                return TelegramMessage;
            }
            try
            {
                // Get LastUpate
                int StartIndex = LogString.LastIndexOf("\n") + 1;
                var newUpdate = LogString.Substring(StartIndex, LogString.Length - StartIndex);
                // Safty Check
                if (string.IsNullOrEmpty(newUpdate))
                {
                    return TelegramMessage;
                }
                // Start Update Type
                if (newUpdate.Substring(0, Math.Min(9, newUpdate.Length)) == "[COMMENT]")
                {
                    // Get Number
                    string baseText = CurText.Substring(0, CurText.LastIndexOf("-")+1);
                    if (baseText == "") baseText = Program.RedComBaseText;
                    string NewText = baseText + $"\nScrapped Comments: {CommentsCount}";
                    // Show
                    if (DateTime.Now.TimeOfDay.Subtract(lastEdit).TotalSeconds >= 5)
                    {
                        if (NewText != CurText)
                        {
                            lastEdit = DateTime.Now.TimeOfDay;
                            Message NewMessage = null;
                            try
                            {
                                NewMessage = await botClient.EditMessageTextAsync(chatId: TelegramMessage.Chat.Id, messageId: TelegramMessage.MessageId, NewText);
                                CurText = NewText;
                            }
                            catch { }
                            return NewMessage == null ? TelegramMessage : NewMessage;
                        }
                    }
                }
                if (newUpdate.Substring(0, Math.Min(7, newUpdate.Length)) == "[TITLE]")
                {
                    string baseText = CurText.Substring(0, CurText.IndexOf("Status") - 1);
                    if (baseText == "") baseText = Program.RedComBaseText;
                    var Title = newUpdate.Substring(newUpdate.IndexOf(" ") + 1);
                    var newText = baseText + "\nTitle: " + Title + "\n\nStatus:";

                    if (newText != CurText)
                    {
                        var NewMessage = await botClient.EditMessageTextAsync(chatId: TelegramMessage.Chat.Id, messageId: TelegramMessage.MessageId, newText);
                        CurText = newText;
                        return NewMessage;
                    }
                }
                if (newUpdate.Substring(0, 2) == "--")
                {
                    string baseText = CurText.Substring(0, CurText.IndexOf("Status") - 1);
                    if (baseText == "") baseText = Program.RedComBaseText;
                    var newText = baseText + "\nStatus: " + newUpdate;

                    if (newText != CurText)
                    {
                        var NewMessage = await botClient.EditMessageTextAsync(chatId: TelegramMessage.Chat.Id, messageId: TelegramMessage.MessageId, newText);
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
