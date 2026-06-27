using Blitztext.Core.Abstractions;
using Blitztext.Core.Models;

namespace Blitztext.Core.Workflows;

/// <summary>Blitztext :): record → transcribe (remote) → add emojis via GPT.</summary>
public sealed class EmojiTextWorkflow : WorkflowBase
{
    private readonly ILlmService _llm;
    private readonly EmojiTextSettings _settings;

    public EmojiTextWorkflow(
        IAudioRecorder recorder,
        IRemoteTranscriptionService remoteTranscription,
        ILocalTranscriptionService localTranscription,
        ILlmService llm,
        EmojiTextSettings settings,
        IReadOnlyList<string> customTerms,
        string language)
        : base(recorder, remoteTranscription, localTranscription,
            customTerms, language, TranscriptionBackend.Remote, LocalModelCatalog.RecommendedFastModelName)
    {
        _llm = llm;
        _settings = settings;
    }

    public override WorkflowType Type => WorkflowType.EmojiText;

    protected override async Task<string> RewriteAsync(string transcript, CancellationToken ct)
    {
        SetRunning("Emojis werden eingefügt ...");
        return await _llm.AddEmojisAsync(transcript, _settings, ct).ConfigureAwait(false);
    }
}
