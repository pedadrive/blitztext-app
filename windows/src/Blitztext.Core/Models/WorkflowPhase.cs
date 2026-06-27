namespace Blitztext.Core.Models;

public enum WorkflowPhaseKind
{
    Idle,
    Running,
    Done,
    Error
}

/// <summary>
/// Immutable workflow state, mirroring the macOS <c>WorkflowPhase</c> enum with
/// its associated message string.
/// </summary>
public readonly record struct WorkflowPhase(WorkflowPhaseKind Kind, string Message)
{
    public static WorkflowPhase Idle { get; } = new(WorkflowPhaseKind.Idle, string.Empty);
    public static WorkflowPhase Running(string message) => new(WorkflowPhaseKind.Running, message);
    public static WorkflowPhase Done(string text) => new(WorkflowPhaseKind.Done, text);
    public static WorkflowPhase Error(string message) => new(WorkflowPhaseKind.Error, message);

    public bool IsActive => Kind != WorkflowPhaseKind.Idle;
}

public enum WorkflowLaunchSource
{
    Manual,
    HotkeyBackground
}

public static class WorkflowLaunchSourceInfo
{
    public static bool PresentsWorkflowPage(this WorkflowLaunchSource source) =>
        source == WorkflowLaunchSource.Manual;
}
