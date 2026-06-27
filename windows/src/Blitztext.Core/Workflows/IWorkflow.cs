using Blitztext.Core.Models;

namespace Blitztext.Core.Workflows;

/// <summary>
/// A recordable workflow. Mirrors the macOS <c>Workflow</c> protocol: start recording,
/// stop to process, reset to abort. Emits phase changes and a final output string.
/// </summary>
public interface IWorkflow
{
    WorkflowType Type { get; }
    WorkflowPhase Phase { get; }
    bool IsRecording { get; }

    event Action<WorkflowPhase>? PhaseChanged;
    event Action<string>? Output;

    void Start();
    void Stop();
    void Reset();
}
