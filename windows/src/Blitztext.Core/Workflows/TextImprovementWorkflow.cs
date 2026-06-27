using Blitztext.Core.Abstractions;
using Blitztext.Core.Models;

namespace Blitztext.Core.Workflows;

/// <summary>Blitztext+: record → transcribe (remote) → improve via GPT.</summary>
public sealed class TextImprovementWorkflow : WorkflowBase
{
    private readonly ILlmService _llm;
    private readonly TextImprovementSettings _settings;

    public TextImprovementWorkflow(
        IAudioRecorder recorder,
        IRemoteTranscriptionService remoteTranscription,
        ILocalTranscriptionService localTranscription,
        ILlmService llm,
        TextImprovementSettings settings,
        string language)
        : base(recorder, remoteTranscription, localTranscription,
            settings.CustomTerms, language, TranscriptionBackend.Remote, LocalModelCatalog.RecommendedFastModelName)
    {
        _llm = llm;
        _settings = settings;
    }

    public override WorkflowType Type => WorkflowType.TextImprover;

    protected override async Task<string> RewriteAsync(string transcript, CancellationToken ct)
    {
        SetRunning("Text wird verbessert ...");
        return await _llm.ImproveAsync(transcript, _settings, ct).ConfigureAwait(false);
    }
}
