using Blitztext.Core.Models;

namespace Blitztext.Core.Abstractions;

/// <summary>Online transcription via OpenAI Whisper. Implemented in Core.</summary>
public interface IRemoteTranscriptionService
{
    Task<string> TranscribeAsync(
        string audioPath,
        IReadOnlyList<string> customTerms,
        string? language,
        CancellationToken cancellationToken = default);
}

/// <summary>On-device transcription via Whisper.net (ggml). Implemented in the Windows shell.</summary>
public interface ILocalTranscriptionService
{
    Task<string> TranscribeAsync(
        string audioPath,
        string? language,
        string modelName,
        CancellationToken cancellationToken = default);

    bool IsModelInstalled(string modelName);
}

/// <summary>Text rewriting via OpenAI Chat Completions. Implemented in Core.</summary>
public interface ILlmService
{
    Task<string> ImproveAsync(string text, TextImprovementSettings settings, CancellationToken cancellationToken = default);
    Task<string> DampfAblassenAsync(string text, string systemPrompt, CancellationToken cancellationToken = default);
    Task<string> AddEmojisAsync(string text, EmojiTextSettings settings, CancellationToken cancellationToken = default);
}
