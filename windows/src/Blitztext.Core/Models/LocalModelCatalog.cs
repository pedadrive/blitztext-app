namespace Blitztext.Core.Models;

/// <summary>
/// Catalog of local Whisper models for the Windows port.
///
/// The macOS app uses WhisperKit/CoreML models from <c>argmaxinc/whisperkit-coreml</c>.
/// CoreML models do not run on Windows, so the Windows port uses the equivalent
/// whisper.cpp <c>ggml</c> models (consumed by Whisper.net). The concept — pick a model,
/// download it on demand into the app-support folder, run transcription fully on-device —
/// is identical.
/// </summary>
public static class LocalModelCatalog
{
    /// <summary>Light, multilingual, good first choice — mirrors the macOS "recommended fast" default.</summary>
    public const string RecommendedFastModelName = "ggml-small";

    public const string BaseModelName = "ggml-base";
    public const string LargeTurboModelName = "ggml-large-v3-turbo";
    public const string LargeModelName = "ggml-large-v3";

    public static IReadOnlyList<string> SupportedModelNames { get; } = new[]
    {
        RecommendedFastModelName,
        BaseModelName,
        LargeTurboModelName,
        LargeModelName
    };

    public static string DisplayName(string modelName)
    {
        if (modelName.Contains("small")) return "Whisper Small";
        if (modelName.Contains("base")) return "Whisper Base";
        if (modelName.Contains("tiny")) return "Whisper Tiny";
        if (modelName.Contains("turbo")) return "Whisper Large v3 Turbo";
        if (modelName.Contains("large-v3")) return "Whisper Large v3";
        return modelName.Replace("ggml-", "").Replace("-", " ");
    }

    public static string NormalizedModelName(string? modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName)) return RecommendedFastModelName;
        return SupportedModelNames.Contains(modelName) ? modelName : RecommendedFastModelName;
    }
}
