## Download

| System | File |
|---|---|
| Windows 10/11 (64-bit) | `ClipperSetup-…-win-x64.exe` |
| macOS, Apple Silicon (M1/M2/M3/M4) | `Clipper-…-osx-arm64.dmg` |
| macOS, Intel | `Clipper-…-osx-x64.dmg` |
| Chrome extension only | `Clipper-extension-….zip` (the extension is also inside the app) |

## Install

**Windows.** Run the `.exe`. If SmartScreen says "Windows protected your PC", click **More info → Run anyway** (the installer is not signed). No admin rights needed.

**macOS.** Open the `.dmg` and drag **Clipper** to **Applications**. The first time, right-click Clipper → **Open** → **Open** (the app is not notarized). If macOS still blocks it: System Settings → Privacy & Security → **Open Anyway**.

**ffmpeg and streamlink** are required:
- Windows: `winget install Gyan.FFmpeg` and `winget install Streamlink.Streamlink`, then restart Clipper.
- macOS: `brew install ffmpeg streamlink`.

## Connect the Chrome extension

The extension watches the stream chat and tells Clipper when activity spikes. Works in Chrome, Edge, Brave and Opera.

1. In Clipper open the **Расширение** tab and press **1. Подготовить папку расширения**. It copies the extension to `Documents\Clipper\chrome-extension` (Windows) or `~/Clipper/chrome-extension` (macOS) and opens that folder. (Alternatively unzip `Clipper-extension-….zip` anywhere.)
2. Go to `chrome://extensions` (Edge: `edge://extensions`).
3. Turn on **Developer mode** (top right; in Edge bottom left).
4. Click **Load unpacked** and choose the `chrome-extension` folder.
5. Pin the Clipper icon (puzzle icon → pin). Its popup lets you tune sensitivity.
6. Press **▶ Старт** in Clipper and keep a tab open with the chat of every stream you record (Twitch channel page or `twitch.tv/popout/<channel>/chat`; YouTube live page with chat).

After updating Clipper, press step 1 again and click ⟳ (reload) on the extension in `chrome://extensions`.
