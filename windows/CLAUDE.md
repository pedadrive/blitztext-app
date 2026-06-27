# CLAUDE.md — `windows/` (Windows port deep notes)

Scoped notes for working **inside `windows/`**. The repo-root `CLAUDE.md` has the
overview; this file is the deep dive and the hard-won gotchas.

## TL;DR

- **Stack:** .NET 10 (LTS) + WPF tray app. Solution: `Blitztext.sln`.
- You **cannot** build or run WPF on Linux. Verify on **CI** (`.github/workflows/windows-build.yml`
  on `windows-latest`) or on a Windows box with the .NET 10 SDK via `./build.ps1`.

## Projects

- **`src/Blitztext.Core`** (`net10.0`) — portable, **zero Windows dependencies**. Models, OpenAI
  clients (`whisper-1`, `gpt-4o`/`gpt-4o-mini`), workflow state machines, settings, quality
  heuristics. Fully unit-tested.
- **`src/Blitztext.App`** (`net10.0-windows`, WPF `WinExe`) — the Windows shell. All native code
  lives under `Platform/`.
- **`src/Blitztext.Core.Tests`** (`net10.0`, xUnit) — 41 tests; run in CI.

## The seam: interfaces (Core) ↔ implementations (App/Platform)

| Interface (Core) | Windows impl (`App/Platform`) | Native mechanism |
| --- | --- | --- |
| `IAudioRecorder` | `NAudioRecorder` | NAudio `WaveInEvent`, 16 kHz mono WAV |
| `IHotkeyService` | `GlobalHotkeyService` | `WH_KEYBOARD_LL` low-level keyboard hook |
| `IPasteService` | `WindowsPasteService` | `GetForegroundWindow` + `SendInput` (Ctrl+V) |
| `ICredentialStore` | `WindowsCredentialStore` | `advapi32` `Cred*` (Credential Manager) |
| `ILocalTranscriptionService` | `WhisperNetLocalTranscriber` | Whisper.net (ggml) |
| `IRemoteTranscriptionService`, `ILlmService` | `OpenAi*` (in Core) | `HttpClient` |

Composition root: `App.xaml.cs` wires everything; `AppCoordinator` (port of the macOS
`AppState`) owns settings, availability gating, and routes output through the paste service.

## Build / test

- `cd windows && ./build.ps1` — restore + build + test. `-Run` launches; `-Publish win-x64`
  (or `win-arm64`) makes a self-contained single build.
- CI `windows-build.yml`: `setup-dotnet` (`10.0.x`) → `restore` → `build` → `test` →
  `publish` matrix (x64 + arm64). Triggers on changes under `windows/**`.

## WindowsDesktop SDK gotchas (these cost several CI rounds)

- Implicit usings **omit `System.IO` and `System.Net.Http`**. `System.IO` is added as a
  project-wide `<Using>` in the App `.csproj`; import `System.Net.Http` per file.
- `UseWindowsForms` makes `Application`, `MessageBox`, `Clipboard` **ambiguous** with WPF —
  always fully-qualify `System.Windows.*` in App code.
- The `WFAC010` DPI warning is **suppressed** (`<NoWarn>`): a WPF host reads Per-Monitor-V2
  DPI awareness from `app.manifest`; the WinForms `ApplicationHighDpiMode`/`SetHighDpiMode`
  path never runs here, so following the analyzer would *regress* DPI awareness.
- `out var` pattern variables scope to the **whole method** — watch for later name collisions.

## Threading notes (subtle, already handled — don't regress)

- Workflow async continuations use `ConfigureAwait(false)`, so `PhaseChanged`/`Output` can fire
  **off the UI thread**. Tray updates are marshalled via `Dispatcher`; clipboard + paste are
  marshalled to the **STA UI thread** inside `WindowsPasteService`.
- The keyboard-hook callback must return fast (Windows drops slow LL hooks). It posts app logic
  to the UI thread via `Dispatcher.BeginInvoke` (**non-blocking**) — never run workflow logic on
  the hook thread.
- NAudio `StopRecording()` is **asynchronous**: `NAudioRecorder` blocks on a latch until
  `RecordingStopped` finalizes the WAV before reporting duration/path. `WorkflowBase.Start()`
  starts the recorder **before** raising the `Running` phase so the tray shows *Recording*.

## Runtime locations

- API key → Windows Credential Manager (target `Blitztext:OpenAiApiKey`).
- Settings → `%APPDATA%\Blitztext\settings.json`.
- Local models → `%APPDATA%\Blitztext\models\whisper\<name>.bin` (ggml, downloaded on demand
  from Hugging Face).

## Default hotkeys

`Ctrl+Alt+B` Blitztext · `+L` Lokal · `+P` Blitztext+ · `+R` Dampf ($%&!) · `+E` Emoji ·
`Esc` cancel. **Hold** = push-to-talk; **Toggle** = press to start / press again to stop.

## Adding a workflow

1. Add a `WorkflowType` case (+ display name/subtitle).
2. Add a `WorkflowBase` subclass; override `RewriteAsync` for the LLM step (or none for pure
   transcription).
3. Wire it in `WorkflowFactory` and in `AppCoordinator.IsWorkflowAvailable`.
4. Add a tray menu entry in `TrayIconController`.
5. Cover the pure-logic parts with xUnit tests using the fakes in `Blitztext.Core.Tests/Fakes.cs`.

## Deep dive

`ARCHITECTURE.md` (technology decision + macOS→Windows mapping) · `README.md` (user-facing).
