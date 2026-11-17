# Clipper

Makes short vertical clips (TikTok / YouTube Shorts) from a stream that is live **right now** on Twitch or YouTube, to bring viewers to the live broadcast.

## How it works

```
 Browser (extension)                 Core Clipper.App (C#)                     Disk
 ┌──────────────────────┐   POST    ┌──────────────────────────────┐
 │ watches the chat,    │/api/spikes│ ClipPlanner: spike → window  │
 │ detects spikes       ├──────────►│ [−25 s; +10 s], merges       │
 └──────────────────────┘           │ nearby spikes                │
                                    │                              │
                                    │ StreamlinkRecorder ──────────┼──► recordings/stream_*.ts
                                    │                              │
                                    │ ClipWorker → FfmpegClipCutter┼──► clips/clip_*.mp4 (1080×1920)
                                    └──────────────────────────────┘
```

1. **Recorder** (`src/Clipper.Recorder`) records each stream with `streamlink` into `.ts` files, several streams at once (one recorder per stream, `RecorderPool`). MPEG-TS can be read while it is still being written, so clips are cut during the broadcast. Every `RotateRecordingMinutes` a new file is started (the old one keeps recording for a short overlap so no clip is split), and `RecordingCleaner` deletes recordings older than `KeepRecordingsMinutes`. Clips are never deleted.
2. **Extension** (`extension/`) measures chat activity: a normal message counts 1, a message with laughter/hype emotes (KEKW, LUL, ахах, 😂… editable list) counts 2, a sub 3, a donation (bits, Super Chat) 5, and a raid triggers a clip immediately. When the last 10 seconds have 2.5× more activity than usual, it sends a "spike" with the exact time and channel name to the core, which matches it to the recorded stream with the same channel. Open a tab with the chat of every stream you record.
3. **Core** (`src/Clipper.App` + `src/Clipper.Core`) turns a spike into a time window (by default 25 s before and 10 s after, because chat reacts with a delay). Back-to-back spikes are merged into one clip of up to 60 seconds.
4. **Subtitles** (`src/Clipper.Subtitles`): before cutting, the clip's audio is transcribed locally with [Whisper](https://github.com/ggerganov/whisper.cpp) (via Whisper.net). The language is detected automatically. Lines are split short for Shorts and saved next to the clip as `clip_….ru.srt`, then burned into the video. The model is downloaded once on the first clip (`base` ≈ 150 MB).
5. **Cutter** (`src/Clipper.Cutter`) waits until the end of the window is recorded, then uses `ffmpeg` to make a vertical video: the original picture centered over a blurred background (`blur`), or a center crop (`crop`).

## Layout

| Folder | Contents |
|---|---|
| `src/Clipper.Core` | Models (`ChatSpike`, `ClipJob`, `RecordingSession`), `ClipperOptions`, module interfaces, `ClipPlanner` |
| `src/Clipper.Recorder` | `StreamlinkRecorder`: records the stream |
| `src/Clipper.Cutter` | `FfmpegClipCutter`: cutting, vertical format, burning in subtitles |
| `src/Clipper.Subtitles` | `WhisperTranscriber`: local speech recognition |
| `src/Clipper.YouTube` | `YouTubeAuth` (Google sign-in) and `YouTubeShortsPublisher` (upload) |
| `src/Clipper.Server` | Wires the core together: HTTP API, background recorder and clip worker (`ClipperServer.Build`) |
| `src/Clipper.App` | Console entry point, reads `appsettings.json` |
| `src/Clipper.Desktop` | Desktop client (Avalonia, Windows / macOS / Linux): stream link, settings, start/stop, clip list, log |
| `tests/Clipper.Tests` | xUnit tests |
| `extension/` | Chrome/Edge extension (Manifest V3) |

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (or Visual Studio 2022 with the "ASP.NET and web development" workload).
- [ffmpeg](https://ffmpeg.org/download.html) on `PATH`. Windows: `winget install Gyan.FFmpeg`.
- [streamlink](https://streamlink.github.io/install.html) on `PATH`. Windows: `winget install streamlink.streamlink`.

Check: `ffmpeg -version` and `streamlink --version` should work in a terminal.

## Running the desktop client (recommended)

- **Visual Studio:** open `Clipper.sln`, set `Clipper.Desktop` as the startup project, press F5.
  For a XAML previewer install the "Avalonia for Visual Studio" extension (optional).
- **Terminal (Windows or macOS):** `dotnet run --project src/Clipper.Desktop`

Paste one or more stream links (**+ Добавить стрим**), press **▶ Старт**, and the core starts inside the client on `http://127.0.0.1:5055`, so the browser extension works the same way. The **Настройки** tab holds all options; they are saved to `settings.json` (Windows: `%APPDATA%\Clipper`, macOS: `~/Library/Application Support/Clipper`). By default recordings and clips go to `Videos/Clipper` (`~/Movies/Clipper` on macOS).

On macOS, install the tools with Homebrew: `brew install ffmpeg streamlink`. The client also looks in `/opt/homebrew/bin` and `/usr/local/bin`, because apps started from Finder don't see the terminal's `PATH`.

Build a standalone app that runs without the .NET SDK:
```
dotnet publish src/Clipper.Desktop -c Release -r win-x64 --self-contained
dotnet publish src/Clipper.Desktop -c Release -r osx-arm64 --self-contained   # Apple Silicon (osx-x64 for Intel)
```
The result is in `src/Clipper.Desktop/bin/Release/net8.0/<runtime>/publish/`.

## Running the console core (without the client)

1. List the streams in `src/Clipper.App/appsettings.json`:
   ```json
   "Streams": [ "https://www.twitch.tv/channel_one", "https://www.twitch.tv/channel_two" ]
   ```
   For YouTube use the broadcast link `https://www.youtube.com/watch?v=...`.
2. Start the core:
   - **Visual Studio:** open `Clipper.sln`, set `Clipper.App` as the startup project, press F5.
   - **Terminal:** `dotnet run --project src/Clipper.App`

   The core listens on `http://127.0.0.1:5055`. Status: `http://127.0.0.1:5055/api/status`.
3. Install the extension: open `chrome://extensions`, enable "Developer mode", click "Load unpacked" and pick the `extension` folder.
4. Open the same stream in the browser with the chat visible. The page console (F12) will show `[Clipper] слежу за чатом ...`.
5. Finished clips appear in `clips/<channel>/<date>/clip_HH-mm-ss.mp4` (relative to the app's working directory in console mode).

The "✂ Клип сейчас" (clip now) button in the extension popup queues a clip manually, without a spike.

## YouTube Shorts upload

Finished clips can be uploaded to YouTube Shorts automatically (checkbox **Загружать автоматически**) or one by one with the **В Shorts** button in the clip list. The title is built from the beginning of the transcribed speech, and the description includes the stream link (templates are in the settings).

### One-time Google setup (~10 minutes)

YouTube only accepts uploads from apps registered in Google Cloud, so you need your own key:

1. Open [Google Cloud Console](https://console.cloud.google.com/) and create a project (any name, e.g. "Clipper").
2. **APIs & Services → Library**, find **YouTube Data API v3**, press **Enable**.
3. **Google Auth Platform** (OAuth consent screen): choose **External**, fill in the app name and your email. Under **Audience → Test users** add the Google account that owns your YouTube channel.
4. **Clients → Create client**, type **Desktop app**, then download the JSON (`client_secret_….json`).
5. In Clipper: **Настройки → YouTube Shorts → Ключ Google → Выбрать…**, pick that file, then press **Войти через Google** and allow access in the browser.

The sign-in token is stored next to `settings.json`, so you only sign in once.

### Limits to know about

- **Private only until audit.** YouTube makes videos uploaded through the API by an unverified Google project **private**. To publish as public/unlisted, the project has to pass the [YouTube API audit](https://support.google.com/youtube/contact/yt_api_form). Until then, upload as private and switch visibility in YouTube Studio.
- **Weekly re-login in testing mode.** While the consent screen is in "Testing", Google expires the sign-in after 7 days, so press **Войти через Google** again. Publishing the app (Audience → Publish app) removes that.
- **Daily quota.** The default quota is 10,000 units per day, and one upload costs a large share of it (1,600 units at the time of writing), so only a handful of uploads per day fit. More quota can be requested in Google Cloud.

## Core API

| Method | What it does |
|---|---|
| `GET /api/status` | Current recordings and clip list with statuses |
| `POST /api/spikes` | A spike: `{"timestamp":"2026-11-17T16:43:09Z","platform":"twitch","channel":"name","messagesPerSecond":6,"baselinePerSecond":1}` |
| `POST /api/clip-now` | Clip at the current moment from all streams (`?stream=channel` for one) |
| `POST /api/clips/{id}/upload` | Upload a finished clip to YouTube Shorts |

Test without the extension (PowerShell):
```powershell
Invoke-RestMethod -Method Post http://127.0.0.1:5055/api/clip-now
```

## Settings

**Core** (`appsettings.json`, `Clipper` section):

| Setting | Default | Meaning |
|---|---|---|
| `SecondsBefore` / `SecondsAfter` | 25 / 10 | Seconds to take before and after a spike |
| `MaxClipSeconds` | 60 | Max length of a merged clip |
| `Streams` | — | Stream links recorded at the same time |
| `RotateRecordingMinutes` | 30 | Start a new recording file this often |
| `KeepRecordingsMinutes` | 120 | Delete recordings older than this (0 = keep forever). Clips are kept |
| `LatencyCorrectionSeconds` | 0 | Shift spike time back if clips start too late |
| `Quality` | best | streamlink quality (`720p`, `1080p60`, …) |
| `Vertical.Mode` | blur | `blur` (blurred background) or `crop` (center crop) |
| `Subtitles.Enabled` / `BurnIn` | true / true | Make subtitles / burn them into the video (`.srt` is always saved) |
| `Subtitles.Model` | base | Whisper model: `tiny`, `base`, `small`, `medium`, `largev3turbo`. `small` is noticeably better for Russian but slower |
| `Subtitles.Language` | auto | `auto` or a language code (`ru`, `en`, …) |

**Extension:** the popup lets you tune the counting window, the "times above normal" threshold, the minimum message count and the cooldown between spikes. Reload the chat tab after saving.

## Tests

```
dotnet test
```
The cutter test generates a test video and cuts it with real ffmpeg. If ffmpeg is not installed, that check is skipped.

## Next steps

- Auto-publishing clips to TikTok.
- Captions and a "Watch the stream live: link" overlay.
