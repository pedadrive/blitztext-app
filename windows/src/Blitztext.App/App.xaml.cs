using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using Blitztext.App.Platform;
using Blitztext.App.UI;
using Blitztext.Core;
using Blitztext.Core.Abstractions;
using Blitztext.Core.Models;
using Blitztext.Core.Services;
using Blitztext.Core.Workflows;

namespace Blitztext.App;

/// <summary>
/// Application entry point and composition root. Mirrors the macOS <c>AppDelegate</c>:
/// builds the tray icon, starts the global hotkey listener, and routes hotkey events to
/// the coordinator using the configured hold/toggle mode. No main window is shown — the
/// app lives in the notification area.
/// </summary>
public partial class App : System.Windows.Application
{
    private AppCoordinator _coordinator = null!;
    private IHotkeyService _hotkeys = null!;
    private TrayIconController _tray = null!;
    private SettingsStore _settingsStore = null!;
    private Dispatcher _dispatcher = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _dispatcher = Dispatcher.CurrentDispatcher;

        AppDataPaths.EnsureBaseDirectory();

        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        var credentials = new WindowsCredentialStore();
        var remote = new OpenAiTranscriptionService(http, credentials);
        var llm = new OpenAiLlmService(http, credentials);
        var local = new WhisperNetLocalTranscriber();
        var paste = new WindowsPasteService();

        _settingsStore = new SettingsStore(AppDataPaths.SettingsFilePath);
        var factory = new WorkflowFactory(() => new NAudioRecorder(), remote, local, llm);

        _coordinator = new AppCoordinator(_settingsStore, factory, credentials, paste, local);

        _hotkeys = new GlobalHotkeyService();
        _hotkeys.HotkeyEvent += OnHotkeyEvent;

        _tray = new TrayIconController(_coordinator, () => ShowSettings());
        _coordinator.MenuBarStatusChanged += status => _dispatcher.Invoke(() => _tray.Update(status));

        _hotkeys.Start();

        if (_coordinator.ShouldShowOnboarding)
        {
            _coordinator.MarkOnboardingSeen();
            ShowSettings();
        }
    }

    private void OnHotkeyEvent(HotkeyEvent ev)
    {
        // The low-level keyboard hook callback runs on the hook thread and must return fast
        // (Windows drops slow LL hooks and serialises all input behind them). The swallow
        // decision is already made inside the hook, so post the app logic without blocking.
        _dispatcher.BeginInvoke(() =>
        {
            switch (ev.Kind)
            {
                case HotkeyEventKind.Down:
                    HandleHotkeyDown(ev.Type);
                    break;
                case HotkeyEventKind.Up:
                    HandleHotkeyUp(ev.Type);
                    break;
                case HotkeyEventKind.Cancel:
                    _coordinator.StopCurrentWorkflow();
                    break;
            }
        });
    }

    private void HandleHotkeyDown(WorkflowType type)
    {
        if (!_coordinator.IsConfigured) return;

        var mode = _coordinator.Settings.App.HotkeyMode;
        if (mode == HotkeyMode.Hold)
        {
            _coordinator.StartWorkflow(type, WorkflowLaunchSource.HotkeyBackground);
        }
        else // Toggle
        {
            var active = _coordinator.ActiveWorkflow;
            if (active is not null && active.Type == type && active.Phase.IsActive)
                active.Stop();
            else
                _coordinator.StartWorkflow(type, WorkflowLaunchSource.HotkeyBackground);
        }
    }

    private void HandleHotkeyUp(WorkflowType type)
    {
        if (_coordinator.Settings.App.HotkeyMode != HotkeyMode.Hold) return;

        var active = _coordinator.ActiveWorkflow;
        if (active is not null && active.Type == type &&
            active.Phase.Kind == WorkflowPhaseKind.Running && active.IsRecording)
        {
            active.Stop();
        }
    }

    private SettingsWindow? _settingsWindow;

    private void ShowSettings()
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_coordinator, new WindowsCredentialStore());
        _settingsWindow.Closed += (_, _) =>
        {
            _coordinator.SaveSettings();
            _settingsWindow = null;
        };
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeys?.Stop();
        _tray?.Dispose();
        _coordinator?.SaveSettings();
        base.OnExit(e);
    }
}
