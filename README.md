# 🚀 TikTok Bot – One of My Most Complex Projects Yet
![TikTok](https://img.shields.io/badge/TikTok-Automation-blueviolet?style=for-the-badge\&logo=tiktok)
![FFMPEG](https://img.shields.io/badge/FFMPEG-green?style=for-the-badge\&logo=ffmpeg)
![AI](https://img.shields.io/badge/AI-Driven-orange?style=for-the-badge\&logo=openai)
![Status](https://img.shields.io/badge/Status-Legacy-red?style=for-the-badge)

**Stats:**

* **Total lines:** 3,869
* **Non-empty lines:** 3,622
* **Comment lines:** 497
* **Methods:** 82
* **Dependencies:** Multiple external libraries & APIs

---

## 🔥 TL;DR

* **One of my most complex projects ever.**
* **3,869 lines of code**, **82 methods**, tons of APIs & AI models.
* A **Telegram-powered TikTok bot** that:

  * Uses **ChatGPT + ElevenLabs + Suno**
  * Reverse-engineers APIs & sites
  * Automates video creation (Reddit posts, fake chat convos, or follower-inspired music videos)
* Was capable of **scheduling posts, generating captions, and uploading to TikTok (semi-automated)**.
* 🔥 Proof that I can build large-scale AI automation systems **from scratch**.

---

## 🧩 Tech Stack

| Category        | Tech Used                                      |
| --------------- | -----------------------------------------------|
| **Programming** | C#, AsyncIO, REST APIs, Web Scraping   |
| **AI Models**   | OpenAI GPT, ElevenLabs TTS, Suno Music Gen     |
| **Automation**  | Telegram Bot API, Reverse-Engineered APIs      |
| **Video/Audio** | FFMPEG, MP3 Raw Data Manipulation              |
| **Infra**       | Cron Scheduling                                |
| **Hosting**     | GoFile (Video Hosting)                         |

---

## 🔥 Overview

This is an **advanced Telegram bot** powered by **ChatGPT** and paired with multiple AI models (like **ElevenLabs**, **Suno**, and **reverse-engineered APIs**) to generate **three unique types of TikTok videos**.

---

## 🎥 Video Types

### 1️⃣ Reddit to Video

* Scrapes the **top 50 posts** from a chosen subreddit (defaults to `r/`) via the Reddit API.
* Extracts **titles** and **body text** from posts.
* Uses **ChatGPT** (with a 10,000-char prompt) to pick the post most likely to **perform best** in a short video format.
* Fetches **top 100 comments** (plus replies up to 3 levels deep) and stores them in an array of objects.
* Applies hardcoded methods to **filter and format comments**.
* Another 10,000-char ChatGPT prompt is used to refine, merge, and clean comments into a **story-driven, safe-for-work script**.
* Generates a **custom Reddit-like UI** (HTML-based), with **per-sentence screenshots** that reveal each line as it's spoken.
* Uses **ElevenLabs** for text-to-speech (sentence by sentence).
* Picks a **random background video & song** (ChatGPT matches them to the topic).
* Builds the final video using a **complex FFMPEG pipeline**.
* ChatGPT also creates **captions & hashtags**.
* **Auto-upload attempts to TikTok** (via contribution to [TikTokUploader Bot](https://github.com/wkaisertexas/tiktok-uploader))—abandoned due to unreliability. Switched to **GoFile** for hosting, then manual upload.
* Sends final video link to Telegram.
* ⚠️ Even plays with **raw MP3 data** at some point.
* Posted on TikTok: [UpvoteVoices](https://www.tiktok.com/@upvotevoices)
  * Example: [Watch Here](https://www.tiktok.com/@upvotevoices/video/7365241679189724459)

---

### 2️⃣ Text Message Convo to Video

* ChatGPT creates a **weird/funny conversation** idea between two fictional people (5,000-char prompt).
* Picks **voice actors** via ElevenLabs.
* Splits conversation into structured objects.
* Reverse-engineers a **website API** (via Burp Suite) to generate **WhatsApp/Instagram-style chat UIs** for each message.
* Generates **voiceovers** for each line.
* ChatGPT chooses a **matching background video & music track**.
* Final video rendered using **another dynamic FFMPEG command**.
* Uploaded to **GoFile**; link sent over Telegram.
* ChatGPT writes the caption.
* Posted on TikTok: [TextMeStories](https://www.tiktok.com/@textmestories8)
  * Example: [Watch Here](https://www.tiktok.com/@textmestories8/video/7447537722253675782)

---

### 3️⃣ Profile to Video

* Picks a **random follower** (via reverse-engineered TikTok API).
* Downloads their **profile picture**.
* ChatGPT writes a **music prompt** inspired by the photo.
* **Suno** composes a custom track.
* Splits track into stems (drums, melody, etc.).
* Creates waveforms for each stem and **syncs to 60 BPM**.
* Uses a **pre-recorded FL Studio timeline video** to simulate a live music session.
* Animates waveforms and overlays them for a dynamic effect.
* Profile picture is featured at the start of the video.
* **FFMPEG renders** the final edit, adding extra visualizers.
* ChatGPT creates a caption **tagging the featured user**.
* Uploads to GoFile, shares link in Telegram.
* Posted on TikTok: [SongFrames](https://www.tiktok.com/@upvotevoices_)
  * Example: [Watch Here](https://www.tiktok.com/@upvotevoices_/video/7369872164159343915)

---

## ⚙️ Extra Features

* Full **error handling**: Logs issues in Telegram but keeps running.
* Supports **scheduled video creation & posting**.
* Telegram channel shows **live updates** on the bot’s workflow.
* Can be **remotely updated** via Telegram bot commands.

---

## 🖼️ Screenshots  

<p align="center">
  <img src="https://github.com/user-attachments/assets/a4d18eaa-acfe-4bcd-bcf0-6c12e844c0da" width="30%" /> 
  <img src="https://github.com/user-attachments/assets/269aba49-671e-4eb3-b43f-75400e701b5b" width="30%" />
  <img src="https://github.com/user-attachments/assets/aedc2b16-cc8c-408d-aca1-41ef5bafd8b2" width="30%" />
</p>  

---

## 🔒 Privacy Notice

This is a **private bot**.  
Thus, I will **not**:
- Provide a build or installation guide  
- Share the `.zip` file containing `PersistentStorage` data (config, videos, and image assets)  

This project is purely for **portfolio and demonstration purposes**.  

---

## ⚠️ Status: Outdated

This bot **no longer works** due to:

* AI platform updates
* Token exhaustion
* Reverse-engineered API changes
* Dependency deprecation

But, it’s designed so I could **revive it in the future** with relative ease.

---

## 🎬 Watch the Old Videos!

The accounts are still up. You can check out all the content this bot created:

* [UpvoteVoices TikTok](https://www.tiktok.com/@upvotevoices)
* [TextMeStories TikTok](https://www.tiktok.com/@textmestories8)
* [SongFrames TikTok](https://www.tiktok.com/@upvotevoices_)
