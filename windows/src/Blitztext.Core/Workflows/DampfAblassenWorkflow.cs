using Blitztext.Core.Abstractions;
using Blitztext.Core.Models;

namespace Blitztext.Core.Workflows;

/// <summary>Blitztext $%&!: record → transcribe (remote) → calm rewrite via GPT.</summary>
public sealed class DampfAblassenWorkflow : WorkflowBase
{
    private readonly ILlmService _llm;
    private readonly DampfAblassenSettings _settings;

    public DampfAblassenWorkflow(
        IAudioRecorder recorder,
        IRemoteTranscriptionService remoteTranscription,
        ILocalTranscriptionService localTranscription,
        ILlmService llm,
        DampfAblassenSettings settings,
        IReadOnlyList<string> customTerms,
        string language)
        : base(recorder, remoteTranscription, localTranscription,
            customTerms, language, TranscriptionBackend.Remote, LocalModelCatalog.RecommendedFastModelName)
    {
        _llm = llm;
        _settings = settings;
    }

    public override WorkflowType Type => WorkflowType.DampfAblassen;

    protected override async Task<string> RewriteAsync(string transcript, CancellationToken ct)
    {
        SetRunning("Wird umformuliert ...");
        return await _llm.DampfAblassenAsync(transcript, _settings.SystemPrompt, ct).ConfigureAwait(false);
    }
}
