# Blitztext for Windows

A native Windows port of the macOS Blitztext app. Same idea, same five workflows: press a
hotkey, speak, get text back, optionally let an LLM rewrite it, and have it pasted into the
app you were using. It lives in the **notification area (system tray)** — there is no main
window.

> Like the macOS preview: **bring your own OpenAI API key**, no hosted backend, no warranty.
> See the technology rationale in [`ARCHITECTURE.md`](ARCHITECTURE.md).

## Features (parity with the macOS app)

- **Blitztext** – record speech and transcribe it.
- **Blitztext Lokal** – transcribe fully on-device (no server).
- **Blitztext+** – transcribe, then clean up the writing with GPT.
- **Blitztext $%&!** – turn a frustrated rant into a calm message.
- **Blitztext :)** – add fitting emojis to dictated text.
- Hold (push-to-talk) **and** toggle hotkey modes, custom terms, tone, emoji density,
  language hint, and the same transcription-quality / artifact rejection.

## How it runs (host & form)

Everything runs **locally on the user's Windows PC** as a single tray application. There is
**no Blitztext server**. The only outbound traffic is, in online mode, direct HTTPS calls
from the PC to the OpenAI API; in **Sicherer Lokaler Modus** nothing leaves the machine.

```
Online transcription:  Your PC ──HTTPS──► OpenAI Whisper (whisper-1)
Text rewriting:        Your PC ──HTTPS──► OpenAI Chat Completions (gpt-4o-mini / gpt-4o)
Local transcription:   Your PC ──► Whisper.net (whisper.cpp, on-device)
```

The OpenAI API key is stored in the **Windows Credential Manager**. Settings live in
`%APPDATA%\Blitztext\settings.json`; local models in `%APPDATA%\Blitztext\models\whisper`.

## Running costs

- The app itself is free (MIT). No subscription, no hosting fees.
- **Online** transcription/rewrite uses **your** OpenAI key and is billed by OpenAI per use.
- **Local mode** (download a Whisper ggml model once, then transcribe offline) has **no
  per-use cost** — only the rewrite workflows still need OpenAI.

## Requirements

- Windows 10 (1809+) or Windows 11, x64 or ARM64.
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) to build from source.
- An OpenAI API key for online transcription/rewriting (optional if you only use local mode).

## Build & run

```powershell
cd windows
./build.ps1 -Run         # restore, build, test, then launch
```

Or with the SDK directly:

```powershell
dotnet build windows/Blitztext.sln -c Release
dotnet run --project windows/src/Blitztext.App
```

Self-contained single-file build (no .NET needed on the target machine):

```powershell
./build.ps1 -Publish win-x64      # or win-arm64
```

CI builds and tests this solution on `windows-latest` and publishes both architectures —
see [`.github/workflows/windows-build.yml`](../.github/workflows/windows-build.yml).

## Default hotkeys

| Shortcut | Workflow |
| --- | --- |
| `Ctrl+Alt+B` | Blitztext |
| `Ctrl+Alt+L` | Blitztext Lokal |
| `Ctrl+Alt+P` | Blitztext+ |
| `Ctrl+Alt+R` | Blitztext $%&! |
| `Ctrl+Alt+E` | Blitztext :) |
| `Esc` | Abbrechen |

In **Hold** mode you hold the chord while speaking and release to stop; in **Toggle** mode you
press once to start and again (or `Esc`) to stop.

## Permissions

- **Microphone** – Windows will prompt on first recording (Settings → Privacy → Microphone).
- Auto-paste uses synthetic `Ctrl+V`; no special permission is required, and the result always
  stays on the clipboard as a fallback.

## Project structure

```
windows/
  Blitztext.sln
  src/
    Blitztext.Core/        portable logic (models, OpenAI clients, workflows, settings) — tested
    Blitztext.App/         WPF tray shell: NAudio, hotkeys, paste, Credential Manager, Whisper.net
    Blitztext.Core.Tests/  xUnit tests for the core
```
