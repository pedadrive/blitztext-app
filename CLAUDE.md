# CLAUDE.md

Orientation for AI sessions working in this repo. Read this first.

## What this repo is

**Blitztext** is a speech-to-text dictation app (hotkey → speak → transcribe → optionally
rewrite with an LLM → paste into the app you were using). The repo contains **two
independent implementations** that share the product concept and workflows, **not code**:

- `BlitztextMac/` — the original **macOS** app: Swift / SwiftUI / AppKit, CoreML/WhisperKit.
  Built with Xcode 16 + XcodeGen via `./build.sh`.
- `windows/` — a native **Windows** port: **.NET 10 / WPF** tray app, Whisper.net.
  Built with the `dotnet` CLI / `windows/build.ps1`.

There is no hosted backend. Online mode talks directly to the OpenAI API; the secure local
mode transcribes fully on-device.

## ⚠️ You probably cannot build or run either app from a Linux dev container

- The macOS app needs **macOS + Xcode**. The Windows app needs **Windows** (WPF does not
  build or run on Linux).
- Installing the .NET SDK in the container may be blocked by the egress proxy
  (`builds.dotnet.microsoft.com` → 403).
- **So verify changes through CI, not local builds.** Push to a branch, then read results
  with the GitHub MCP tools (`actions_list`, `actions_get`, `get_job_logs`). That is the
  primary verification path for both apps from a Linux session.

## Repo map

```
BlitztextMac/                  macOS app (Swift)        → ./build.sh
windows/
  Blitztext.sln
  src/Blitztext.Core/          net10.0   portable logic (models, OpenAI clients,
                                         workflow state machines, settings) — unit-tested
  src/Blitztext.App/           net10.0-windows (WPF)  Windows shell: tray, hotkeys,
                                         NAudio, SendInput paste, Credential Manager, Whisper.net
  src/Blitztext.Core.Tests/    net10.0   xUnit (41 tests)
  ARCHITECTURE.md              technology decision + macOS→Windows mapping
  README.md / build.ps1
.github/workflows/
  ci.yml                       macOS build on macos-14 (+ secret-scan)
  windows-build.yml            Windows build/test/publish on windows-latest (.NET 10)
```

## Build / verify

- **macOS:** `./build.sh --debug` (needs macOS + Xcode). CI: `ci.yml` on `macos-14`.
- **Windows:** `cd windows && ./build.ps1` (needs Windows + .NET 10 SDK). CI:
  `windows-build.yml` on `windows-latest` (`setup-dotnet` pulls the SDK per run).

## Windows port specifics (.NET 10)

- **Design:** `Blitztext.Core` has **zero Windows dependencies**. Everything platform-specific
  sits behind interfaces (`IAudioRecorder`, `ICredentialStore`, `IPasteService`,
  `IHotkeyService`, `IRemoteTranscriptionService`, `ILocalTranscriptionService`,
  `ILlmService`) implemented in `Blitztext.App/Platform/`. That keeps the bulk of the app
  unit-testable on Linux/CI with fakes.
- **WindowsDesktop SDK gotchas** (these cost several CI rounds — keep them in mind):
  - Implicit usings **omit `System.IO` and `System.Net.Http`** → `System.IO` is added as a
    project-wide `<Using>` in the App csproj; import `System.Net.Http` per file.
  - `UseWindowsForms` pulls WinForms into scope, so `Application`, `MessageBox`, `Clipboard`
    are **ambiguous** with WPF — always fully-qualify `System.Windows.*` in App code.
  - The `WFAC010` DPI warning is **suppressed on purpose**: a WPF host reads Per-Monitor-V2
    DPI awareness from `app.manifest`; the WinForms `ApplicationHighDpiMode` recommendation
    is a no-op here.
- **Runtime locations:** API key → Windows Credential Manager; settings →
  `%APPDATA%\Blitztext\settings.json`; local Whisper models → `%APPDATA%\Blitztext\models\whisper`.
- Local transcription uses **Whisper.net (ggml)** — the Windows counterpart to the macOS
  WhisperKit/CoreML path. Default hotkeys: `Ctrl+Alt+B/L/P/R/E`, `Esc` to cancel.
- Deep dive: `windows/ARCHITECTURE.md`.

## Conventions

- Work on a **feature branch**, never push to `main`. (Current Windows work lives on
  `claude/repo-structure-deployment-afi6at`, Pull Request #1.)
- **Never commit** API keys, tokens, recordings, or transcripts. `ci.yml` runs a secret
  scan — keep it green.
- PR bodies: mirror `.github/PULL_REQUEST_TEMPLATE.md`.
- Keep the macOS preview scope intact; the Windows port is additive under `windows/`.
