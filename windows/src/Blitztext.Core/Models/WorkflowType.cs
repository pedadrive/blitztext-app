namespace Blitztext.Core.Models;

/// <summary>
/// The five Blitztext workflows. Mirrors the macOS <c>WorkflowType</c> enum,
/// including display names, subtitles and the default hotkey labels.
/// </summary>
public enum WorkflowType
{
    Transcription,
    LocalTranscription,
    TextImprover,
    DampfAblassen,
    EmojiText
}

public static class WorkflowTypeInfo
{
    public static IReadOnlyList<WorkflowType> AllCases { get; } = new[]
    {
        WorkflowType.Transcription,
        WorkflowType.LocalTranscription,
        WorkflowType.TextImprover,
        WorkflowType.DampfAblassen,
        WorkflowType.EmojiText
    };

    /// <summary>Cases shown in the main tray menu (everything except the dedicated local entry).</summary>
    public static IReadOnlyList<WorkflowType> MainMenuCases { get; } =
        AllCases.Where(t => t != WorkflowType.LocalTranscription).ToArray();

    public static string Id(this WorkflowType type) => type switch
    {
        WorkflowType.Transcription => "transcription",
        WorkflowType.LocalTranscription => "localTranscription",
        WorkflowType.TextImprover => "textImprover",
        WorkflowType.DampfAblassen => "dampfAblassen",
        WorkflowType.EmojiText => "emojiText",
        _ => type.ToString()
    };

    public static string DisplayName(this WorkflowType type) => type switch
    {
        WorkflowType.Transcription => "Blitztext",
        WorkflowType.LocalTranscription => "Blitztext Lokal",
        WorkflowType.TextImprover => "Blitztext+",
        WorkflowType.DampfAblassen => "Blitztext $%&!",
        WorkflowType.EmojiText => "Blitztext :)",
        _ => type.ToString()
    };

    public static string Subtitle(this WorkflowType type) => type switch
    {
        WorkflowType.Transcription => "Sprache rein. Text raus.",
        WorkflowType.LocalTranscription => "Nur lokal. Kein Server.",
        WorkflowType.TextImprover => "Geschrieben sprechen.",
        WorkflowType.DampfAblassen => "Frust rein. Entspannt raus.",
        WorkflowType.EmojiText => "Text rein. Emojis dazu.",
        _ => string.Empty
    };
}
