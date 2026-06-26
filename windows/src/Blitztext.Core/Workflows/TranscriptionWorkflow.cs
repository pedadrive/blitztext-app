using Blitztext.Core.Abstractions;
using Blitztext.Core.Models;

namespace Blitztext.Core.Workflows;

/// <summary>Blitztext / Blitztext Lokal: record → transcribe (remote or local). No rewrite.</summary>
public sealed class TranscriptionWorkflow : WorkflowBase
{
    public TranscriptionWorkflow(
        IAudioRecorder recorder,
        IRemoteTranscriptionService remoteTranscription,
        ILocalTranscriptionService localTranscription,
        WorkflowType type,
        IReadOnlyList<string> customTerms,
        string language,
        TranscriptionBackend backend,
        string localModelName)
        : base(recorder, remoteTranscription, localTranscription, customTerms, language, backend, localModelName)
    {
        Type = type;
    }

    public override WorkflowType Type { get; }
}
