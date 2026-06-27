# Blitztext for Windows — Architecture & Technology Decision

This is a from-scratch Windows port of the macOS Blitztext app. It reproduces the
**same features** on a native Windows stack, chosen to stay maintainable and runnable
on many Windows machines for a long time.

## Why .NET 10 + WPF (and not Electron / Python / Tauri)

The original is a native macOS menu-bar utility (SwiftUI + AppKit + CoreML). The
Windows equivalent has to do four things that are awkward in cross-platform runtimes:

1. A **system-tray (notification area) app with no main window** that stays resident.
2. **Global push-to-talk hotkeys** with reliable key-down / key-up (hold) detection.
3. **Auto-paste** into whatever app had focus a moment ago (foreground capture + synthetic Ctrl+V).
4. **On-device speech-to-text** (the macOS app uses CoreML/WhisperKit).

| Option | Verdict |
| --- | --- |
| **.NET 10 (LTS) + WPF** | ✅ Chosen. First-party Microsoft stack on the current LTS (released Nov 2025, supported into ~Nov 2028), runs on every Windows 10/11 (x64 **and** ARM64). Clean Win32 interop for the tray, low-level keyboard hook, `SendInput`, and Credential Manager. WPF has been stable for ~15 years and is still shipped/supported. Local Whisper via `Whisper.net` (whisper.cpp). |
| Electron / Node | Works, but a heavier runtime that churns faster; push-to-talk and auto-paste still need native modules. Worse longevity for a tiny tray utility. |
| Rust + Tauri | Lightweight and modern, but smaller ecosystem for low-level keyboard-hook hold detection + audio; more bespoke glue. |
| Python + pystray/PyQt | Fast to write, but durable single-exe packaging across Windows versions is the weakest long-term story. |

**Target framework:** `.NET 10.0` (LTS). UI shell: `net10.0-windows` (WPF). Portable core: `net10.0`.

## Project layout

```
windows/
  Blitztext.sln
  src/
    Blitztext.Core/        net10.0     — platform-neutral: models, OpenAI clients,
                                         workflow state machines, settings, quality logic.
                                         No Windows dependency → unit-testable on any OS.
    Blitztext.App/         net10.0-windows (WPF, WinExe) — the Windows shell:
                                         tray icon, hotkeys, NAudio mic, SendInput paste,
                                         Credential Manager, Whisper.net, Settings window.
    Blitztext.Core.Tests/  net10.0 (xUnit) — tests for the portable core.
```

This mirrors the macOS split (`Services` / `Features/Workflows` / `App`) but pushes every
piece of testable logic into `Blitztext.Core` behind interfaces, so the bulk of the app is
covered by tests that do **not** require a Mac, a microphone, or an API key.

## Feature parity map (macOS → Windows)

| macOS | Windows port |
| --- | --- |
| `NSStatusItem` menu-bar item, `.accessory` policy | `NotifyIcon` tray icon, no taskbar window |
| `NSEvent` flagsChanged global monitor (push-to-talk) | `WH_KEYBOARD_LL` low-level keyboard hook |
| `AVAudioRecorder` → 16 kHz mono m4a | `NAudio` `WaveInEvent` → 16 kHz mono WAV |
| OpenAI `whisper-1` (online transcription) | identical HTTPS call (`HttpClient`, multipart) |
| OpenAI `gpt-4o-mini` / `gpt-4o` (rewrite) | identical Chat Completions call |
| WhisperKit / CoreML (local mode) | `Whisper.net` (whisper.cpp, ggml models) |
| macOS Keychain | Windows **Credential Manager** (DPAPI-backed) |
| `~/Library/Application Support/Blitztext/` JSON | `%APPDATA%\Blitztext\settings.json` |
| `CGEvent` synthetic Cmd+V + frontmost capture | `SendInput` synthetic Ctrl+V + `GetForegroundWindow` |
| Hotkeys: fn+Shift, fn+Ctrl, fn+Opt, fn+Cmd, fn+Shift+Ctrl | Ctrl+Alt+B / +P / +R / +E / +L (configurable, hold or toggle) |

The five workflows (Blitztext, Blitztext Lokal, Blitztext+, Blitztext $%&!, Blitztext :))
and all their prompts, tones, emoji densities, custom terms, and quality/artifact rejection
are ported verbatim.

## Verification

Because this Linux build container cannot install the .NET SDK (egress policy blocks
`builds.dotnet.microsoft.com`) and WPF only runs on Windows, verification is done by a
**GitHub Actions workflow on `windows-latest`** that restores, builds the whole solution,
and runs the `Blitztext.Core.Tests` suite. See `.github/workflows/windows-build.yml`.
