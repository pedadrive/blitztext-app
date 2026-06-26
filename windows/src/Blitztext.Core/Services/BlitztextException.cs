namespace Blitztext.Core.Services;

public enum BlitztextErrorKind
{
    NotConfigured,
    Network,
    Api,
    NoContent,
    NoFile
}

/// <summary>
/// User-facing error with German messages, mirroring the macOS <c>LLMError</c> /
/// <c>TranscriptionError</c> cases.
/// </summary>
public sealed class BlitztextException : Exception
{
    public BlitztextErrorKind Kind { get; }

    public BlitztextException(BlitztextErrorKind kind, string message) : base(message)
    {
        Kind = kind;
    }

    public static BlitztextException NotConfigured() =>
        new(BlitztextErrorKind.NotConfigured, "OpenAI API Key fehlt. Bitte in den Einstellungen hinterlegen.");

    public static BlitztextException Network(string detail) =>
        new(BlitztextErrorKind.Network, "Verbindungsproblem: " + detail);

    public static BlitztextException Api(string detail) =>
        new(BlitztextErrorKind.Api, "Fehler von OpenAI: " + detail);

    public static BlitztextException NoContent() =>
        new(BlitztextErrorKind.NoContent, "Keine Antwort erhalten. Bitte nochmal versuchen.");

    public static BlitztextException NoFile() =>
        new(BlitztextErrorKind.NoFile, "Keine Audio-Datei gefunden");
}
