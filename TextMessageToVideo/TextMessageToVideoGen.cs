using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedditBotNew.TextMessageToVideo
{
    internal class TextMessageToVideoGen
    {
        // Public Run Vars
        public LogUpdates Logger;
        public bool IsSuccessful;
        public string Error;
        public FinalVideo VideoToUpload = new();

        // --- Base Functions --- \\
        public void StartCreation()
        {
            // Set Account Logger.Login
            VideoToUpload.InstaEmail = "upvotevoices@gmail.com";
            VideoToUpload.InstaPassword = "69251Janko!!";

            // Link Error Handeling
            Console.ForegroundColor = ConsoleColor.Gray;
            IsSuccessful = false;

            try
            {
                // Get New TextMessage
                // Create Screenshots
                // Create Voices
                // Create FFMpeg Command
                // Create Video
                // Finish
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
        }
    
        // --- Sub Functions --- \\

    }
}
