One of my most complex projects yet.
Total lines: 3 869
Non-empty lines: 3 622
Comment lines: 497
Methods: 82
+ all dependencies and external libraries

Advanced telegram Bot that uses Chatgpt, paired multiple other AI models (like Elevenlabs, suno and reverse engineered websites) to create one of 3 types of videos
1) Reddit To Video
   - Uses the Reddit API to scrape the top 50 most recent posts on a specified subreddit (defaults to r/)
   - Scrapes the post's title and body text
   - Feed all the posts with a 10 000 character prompt to chatgpt, for chatgpt to pick out the best post from the 50 (in terms of which would preform the best in a shirt video format)
   - Then the AI scrapes the top 100 comments of the selected post (includes replies until the 3rd level reply), and saves them in a array of objects
   - Filter, arrange and modify the comments a bit with hardcoded methods
   - It then feeds this with another 10 000 character prompt to chatgpt, for chatgpt to select the best comments. Chatgpt edits and merges comments together to ensure a compelling story while keeping the language clean.
   - These comments are then fetched, and using a custom HTML website, a reddit "lookalike" UI created for that comment (with the user's profile picture, name and rewards)
         > Note, each sentence has its own "screenshot" as on each sentence spoken, the sentence is revealed
   - These comments are then fed into elenlabs to be spoken. Sentence for sentence
   - Then a random background video and song is selected from storage
   - Then using a complex and dynamic FFMPEG command, all of these elements are put together to create a sosial media video
   - The final script and post title was then send to chatgpt, paied with a prompt, to create a fitted caption and hashtags
   - There was attampts to automaticly upload these videos to tiktok (i even contributed to a opensource [TiktokUploader Bot](https://github.com/wkaisertexas/tiktok-uploader)), but this was unreliable, thus i resorted in using GoFile to upload the files
   - The video file link is then send over the telegram channel for me to manually upload
   - This bot messes with Mp3 raw data at some point :/
   - Tiktok Channel used to post on: https://www.tiktok.com/@upvotevoices
       > Note, i suspect tiktok detected my videos where uploaded by a bot (when i was still using that), so my account got shadow banned. As i see my watch time being pretty high of the few viewers i get.
2) TextMessage Convo To Video
   - Here the chatgpt genorates a new topic/idea for a weird text message convoration between 2 poeple
   - Chatgpt is then intructed to select voiceover-voices and create the text message convo between the 2 people, following a 5 000 character prompt


The bot supports scheduled creations, where i can set the times with each of which bots uploads when.

Outdated:
 - This bot does not currently work anymore. As updated rolled out on AI platforms, tokens been used up, reverse engineered change their API and dependancies get discontinued, the bot saw the end of its day
 - No reason why it shouldnt be brought back to live with relative effort, as this was written with the idea of "i should stil understand this code in 2 years time"

Check Out the videos it created in the past
