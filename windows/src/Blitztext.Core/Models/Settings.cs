using System.Text.Json.Serialization;

namespace Blitztext.Core.Models;

public enum TranscriptionBackend
{
    Remote,
    Local
}

public enum TextTone
{
    Formal,
    Neutral,
    Casual
}

public static class TextToneInfo
{
    public static string DisplayName(this TextTone tone) => tone switch
    {
        TextTone.Formal => "Formell",
        TextTone.Neutral => "Neutral",
        TextTone.Casual => "Locker",
        _ => tone.ToString()
    };
}

public enum EmojiDensity
{
    Wenig,
    Mittel,
    Viel
}

public static class EmojiDensityInfo
{
    public static string DisplayName(this EmojiDensity density) => density switch
    {
        EmojiDensity.Wenig => "Wenig",
        EmojiDensity.Mittel => "Mittel",
        EmojiDensity.Viel => "Viel",
        _ => density.ToString()
    };
}

/// <summary>App-wide settings. Mirrors the macOS <c>AppSettings</c>.</summary>
public sealed class AppSettings
{
    public HotkeyMode HotkeyMode { get; set; } = HotkeyMode.Hold;
    public bool HasSeenOnboarding { get; set; }
    public bool SecureLocalModeEnabled { get; set; }
    public string SelectedLocalTranscriptionModelName { get; set; } = LocalModelCatalog.RecommendedFastModelName;
    public bool HasAutoSelectedFastLocalModel { get; set; }
    public bool LaunchAtLogin { get; set; }
}

public sealed class TranscriptionSettings
{
    /// <summary>ISO language hint for Whisper, e.g. "de". Empty = auto-detect.</summary>
    public string Language { get; set; } = "de";
}

public sealed class TextImprovementSettings
{
    public string SystemPrompt { get; set; } = string.Empty;
    public List<string> CustomTerms { get; set; } = new();
    public string Context { get; set; } = string.Empty;
    public TextTone Tone { get; set; } = TextTone.Neutral;
    public string CustomName { get; set; } = string.Empty;
}

public sealed class DampfAblassenSettings
{
    public string SystemPrompt { get; set; } =
        "Du erhältst ein emotional gesprochenes Transkript. Erkenne zuerst das eigentliche Ziel, " +
        "Anliegen und den wahren Frust der Person. Formuliere daraus eine klare, respektvolle und " +
        "wirksame Nachricht, mit der die Person ihr Ziel eher erreicht. Bewahre relevante Fakten, " +
        "konkrete Probleme, Grenzen, Erwartungen und die nötige Dringlichkeit. Entferne Beleidigungen, " +
        "Drohungen, Sarkasmus, Unterstellungen und unnötige Eskalation. Wenn mehrere Vorwürfe genannt " +
        "werden, verdichte sie auf die entscheidenden Kernpunkte. Der Ton soll ruhig, menschlich, " +
        "bestimmt und lösungsorientiert sein. Gib NUR die fertige Nachricht zurück.";

    public string CustomName { get; set; } = string.Empty;
}

public sealed class EmojiTextSettings
{
    public EmojiDensity EmojiDensity { get; set; } = EmojiDensity.Mittel;
    public string CustomName { get; set; } = string.Empty;
}

/// <summary>Serialized container persisted as JSON. Mirrors the macOS <c>SettingsContainer</c>.</summary>
public sealed class SettingsContainer
{
    public AppSettings App { get; set; } = new();
    public TranscriptionSettings Transcription { get; set; } = new();
    public TextImprovementSettings TextImprovement { get; set; } = new();
    public DampfAblassenSettings DampfAblassen { get; set; } = new();
    public EmojiTextSettings EmojiText { get; set; } = new();
}
