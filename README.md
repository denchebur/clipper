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

1. **Recorder** (`src/Clipper.Recorder`) records the stream with `streamlink` into a `.ts` file. MPEG-TS can be read while it is still being written, so clips are cut during the broadcast. If the stream drops, recording restarts into a new file.
2. **Extension** (`extension/`) counts chat messages. When the last 10 seconds have 2.5× more messages than usual, it sends a "spike" with the exact time to the core.
3. **Core** (`src/Clipper.App` + `src/Clipper.Core`) turns a spike into a time window (by default 25 s before and 10 s after, because chat reacts with a delay). Back-to-back spikes are merged into one clip of up to 60 seconds.
4. **Cutter** (`src/Clipper.Cutter`) waits until the end of the window is recorded, then uses `ffmpeg` to make a vertical video: the original picture centered over a blurred background (`blur`), or a center crop (`crop`).

## Layout

| Folder | Contents |
|---|---|
| `src/Clipper.Core` | Models (`ChatSpike`, `ClipJob`, `RecordingSession`), `ClipperOptions`, module interfaces, `ClipPlanner` |
| `src/Clipper.Recorder` | `StreamlinkRecorder`: records the stream |
| `src/Clipper.Cutter` | `FfmpegClipCutter`: cutting and vertical format |
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

Paste the stream link, press **▶ Старт**, and the core starts inside the client on `http://127.0.0.1:5055`, so the browser extension works the same way. The **Настройки** tab holds all options; they are saved to `settings.json` (Windows: `%APPDATA%\Clipper`, macOS: `~/Library/Application Support/Clipper`). By default recordings and clips go to `Videos/Clipper` (`~/Movies/Clipper` on macOS).

On macOS, install the tools with Homebrew: `brew install ffmpeg streamlink`. The client also looks in `/opt/homebrew/bin` and `/usr/local/bin`, because apps started from Finder don't see the terminal's `PATH`.

Build a standalone app that runs without the .NET SDK:
```
dotnet publish src/Clipper.Desktop -c Release -r win-x64 --self-contained
dotnet publish src/Clipper.Desktop -c Release -r osx-arm64 --self-contained   # Apple Silicon (osx-x64 for Intel)
```
The result is in `src/Clipper.Desktop/bin/Release/net8.0/<runtime>/publish/`.

## Running the console core (without the client)

1. Set the stream in `src/Clipper.App/appsettings.json`:
   ```json
   "StreamUrl": "https://www.twitch.tv/channel_name"
   ```
   For YouTube use the broadcast link `https://www.youtube.com/watch?v=...`.
2. Start the core:
   - **Visual Studio:** open `Clipper.sln`, set `Clipper.App` as the startup project, press F5.
   - **Terminal:** `dotnet run --project src/Clipper.App`

   The core listens on `http://127.0.0.1:5055`. Status: `http://127.0.0.1:5055/api/status`.
3. Install the extension: open `chrome://extensions`, enable "Developer mode", click "Load unpacked" and pick the `extension` folder.
4. Open the same stream in the browser with the chat visible. The page console (F12) will show `[Clipper] слежу за чатом ...`.
5. Finished clips appear in `src/Clipper.App/clips/` (relative to the app's working directory).

The "✂ Клип сейчас" (clip now) button in the extension popup queues a clip manually, without a spike.

## Core API

| Method | What it does |
|---|---|
| `GET /api/status` | Current recordings and clip list with statuses |
| `POST /api/spikes` | A spike: `{"timestamp":"2026-11-17T16:43:09Z","platform":"twitch","channel":"name","messagesPerSecond":6,"baselinePerSecond":1}` |
| `POST /api/clip-now` | Clip at the current moment |

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
| `LatencyCorrectionSeconds` | 0 | Shift spike time back if clips start too late |
| `Quality` | best | streamlink quality (`720p`, `1080p60`, …) |
| `Vertical.Mode` | blur | `blur` (blurred background) or `crop` (center crop) |

**Extension:** the popup lets you tune the counting window, the "times above normal" threshold, the minimum message count and the cooldown between spikes. Reload the chat tab after saving.

## Tests

```
dotnet test
```
The cutter test generates a test video and cuts it with real ffmpeg. If ffmpeg is not installed, that check is skipped.

## Next steps

- Auto-publishing clips to TikTok / YouTube Shorts.
- Captions and a "Watch the stream live: link" overlay.
- Deleting old recordings so the disk doesn't fill up.
- A smarter detector: emotes, "KEKW", donations.
