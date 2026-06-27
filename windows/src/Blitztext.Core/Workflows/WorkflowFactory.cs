using Blitztext.Core.Abstractions;
using Blitztext.Core.Models;

namespace Blitztext.Core.Workflows;

/// <summary>
/// Builds workflow instances from the current settings. Keeps the wiring of recorder /
/// transcription / LLM dependencies in one place so the coordinator stays small.
/// </summary>
public sealed class WorkflowFactory
{
    private readonly Func<IAudioRecorder> _recorderFactory;
    private readonly IRemoteTranscriptionService _remote;
    private readonly ILocalTranscriptionService _local;
    private readonly ILlmService _llm;

    public WorkflowFactory(
        Func<IAudioRecorder> recorderFactory,
        IRemoteTranscriptionService remote,
        ILocalTranscriptionService local,
        ILlmService llm)
    {
        _recorderFactory = recorderFactory;
        _remote = remote;
        _local = local;
        _llm = llm;
    }

    public IWorkflow Create(WorkflowType type, SettingsContainer settings, string localModelName)
    {
        var language = settings.Transcription.Language;
        var customTerms = settings.TextImprovement.CustomTerms;

        return type switch
        {
            WorkflowType.Transcription => new TranscriptionWorkflow(
                _recorderFactory(), _remote, _local, WorkflowType.Transcription,
                customTerms, language,
                settings.App.SecureLocalModeEnabled ? TranscriptionBackend.Local : TranscriptionBackend.Remote,
                localModelName),

            WorkflowType.LocalTranscription => new TranscriptionWorkflow(
                _recorderFactory(), _remote, _local, WorkflowType.LocalTranscription,
                customTerms, language, TranscriptionBackend.Local, localModelName),

            WorkflowType.TextImprover => new TextImprovementWorkflow(
                _recorderFactory(), _remote, _local, _llm, settings.TextImprovement, language),

            WorkflowType.DampfAblassen => new DampfAblassenWorkflow(
                _recorderFactory(), _remote, _local, _llm, settings.DampfAblassen, customTerms, language),

            WorkflowType.EmojiText => new EmojiTextWorkflow(
                _recorderFactory(), _remote, _local, _llm, settings.EmojiText, customTerms, language),

            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}
