using Blitztext.Core.Abstractions;
using Blitztext.Core.Models;
using Blitztext.Core.Services;
using Blitztext.Core.Workflows;

namespace Blitztext.Core;

/// <summary>
/// Central orchestrator. Ported from the macOS <c>AppState</c>: owns settings, decides
/// which workflows are available, runs them, and routes the result through the paste
/// service. Platform concerns (recording, hotkeys, pasting, local model install) are
/// injected as abstractions so this class is fully unit-testable.
/// </summary>
public sealed class AppCoordinator
{
    private readonly SettingsStore _settingsStore;
    private readonly WorkflowFactory _workflowFactory;
    private readonly ICredentialStore _credentials;
    private readonly IPasteService _paste;
    private readonly ILocalTranscriptionService _localTranscription;

    private IWorkflow? _activeWorkflow;
    private IPasteTarget? _activePasteTarget;
    private WorkflowLaunchSource _activeLaunchSource = WorkflowLaunchSource.Manual;
    private MenuBarStatus _menuBarStatus = MenuBarStatus.Idle;

    public AppCoordinator(
        SettingsStore settingsStore,
        WorkflowFactory workflowFactory,
        ICredentialStore credentials,
        IPasteService paste,
        ILocalTranscriptionService localTranscription)
    {
        _settingsStore = settingsStore;
        _workflowFactory = workflowFactory;
        _credentials = credentials;
        _paste = paste;
        _localTranscription = localTranscription;

        Settings = _settingsStore.Load();
        AutoSelectFastLocalModelIfNeeded();
    }

    public SettingsContainer Settings { get; }

    public IWorkflow? ActiveWorkflow => _activeWorkflow;

    public MenuBarStatus CurrentStatus
    {
        get => _menuBarStatus;
        private set
        {
            if (_menuBarStatus == value) return;
            _menuBarStatus = value;
            MenuBarStatusChanged?.Invoke(value);
        }
    }

    public event Action<MenuBarStatus>? MenuBarStatusChanged;

    public string SelectedLocalModelName =>
        LocalModelCatalog.NormalizedModelName(Settings.App.SelectedLocalTranscriptionModelName);

    public bool SelectedLocalModelIsInstalled => _localTranscription.IsModelInstalled(SelectedLocalModelName);

    public bool IsConfigured => _credentials.IsConfigured() || SelectedLocalModelIsInstalled;

    public bool ShouldShowOnboarding => !IsConfigured && !Settings.App.HasSeenOnboarding;

    // MARK: - Workflow management

    public void StartWorkflow(WorkflowType type, WorkflowLaunchSource source = WorkflowLaunchSource.Manual)
    {
        if (!IsWorkflowAvailable(type)) return;

        _activeWorkflow?.Stop();
        _activeLaunchSource = source;
        _activePasteTarget = _paste.CaptureForegroundTarget();

        var workflow = _workflowFactory.Create(type, Settings, SelectedLocalModelName);
        workflow.Output += OnWorkflowOutput;
        workflow.PhaseChanged += phase => OnWorkflowPhaseChanged(workflow, phase);
        _activeWorkflow = workflow;
        workflow.Start();
    }

    public bool IsWorkflowAvailable(WorkflowType type) => type switch
    {
        WorkflowType.LocalTranscription => SelectedLocalModelIsInstalled,
        WorkflowType.Transcription => Settings.App.SecureLocalModeEnabled
            ? SelectedLocalModelIsInstalled
            : _credentials.IsConfigured(),
        WorkflowType.TextImprover or WorkflowType.DampfAblassen or WorkflowType.EmojiText =>
            !Settings.App.SecureLocalModeEnabled && _credentials.IsConfigured(),
        _ => false
    };

    public void StopCurrentWorkflow() => _activeWorkflow?.Stop();

    public void ResetCurrentWorkflow()
    {
        _activeWorkflow?.Reset();
        _activeWorkflow = null;
        _activePasteTarget = null;
        _activeLaunchSource = WorkflowLaunchSource.Manual;
        CurrentStatus = MenuBarStatus.Idle;
    }

    private void OnWorkflowOutput(string text)
    {
        _paste.PasteAtCursor(text, _activePasteTarget);
    }

    private void OnWorkflowPhaseChanged(IWorkflow workflow, WorkflowPhase phase)
    {
        CurrentStatus = phase.Kind switch
        {
            WorkflowPhaseKind.Idle => MenuBarStatus.Idle,
            WorkflowPhaseKind.Running => workflow.IsRecording
                ? MenuBarStatus.Recording(workflow.Type)
                : MenuBarStatus.Processing(workflow.Type),
            WorkflowPhaseKind.Done => MenuBarStatus.Success(workflow.Type),
            WorkflowPhaseKind.Error => MenuBarStatus.Error(workflow.Type),
            _ => CurrentStatus
        };

        // Once a workflow reaches a terminal phase, drop our reference so the next hotkey
        // (toggle mode especially) starts a fresh recording instead of treating the finished
        // Done/Error workflow as still active. The paste target is left until the next start.
        if (phase.Kind is WorkflowPhaseKind.Done or WorkflowPhaseKind.Error &&
            ReferenceEquals(workflow, _activeWorkflow))
        {
            _activeWorkflow = null;
        }
    }

    // MARK: - Settings

    public void SaveSettings() => _settingsStore.Save(Settings);

    public void MarkOnboardingSeen()
    {
        if (Settings.App.HasSeenOnboarding) return;
        Settings.App.HasSeenOnboarding = true;
        SaveSettings();
    }

    public void EnableSecureLocalMode()
    {
        Settings.App.SecureLocalModeEnabled = true;
        SaveSettings();
    }

    public void SetApiKey(string apiKey)
    {
        var trimmed = apiKey.Trim();
        if (trimmed.Length == 0)
            _credentials.Delete(CredentialKey.OpenAiApiKey);
        else
            _credentials.Save(CredentialKey.OpenAiApiKey, trimmed);
    }

    private void AutoSelectFastLocalModelIfNeeded()
    {
        if (Settings.App.HasAutoSelectedFastLocalModel) return;
        Settings.App.SelectedLocalTranscriptionModelName = LocalModelCatalog.RecommendedFastModelName;
        Settings.App.HasAutoSelectedFastLocalModel = true;
    }
}
