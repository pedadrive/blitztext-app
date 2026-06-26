namespace Blitztext.Core.Models;

/// <summary>
/// Hotkey behaviour. <see cref="Hold"/> = push-to-talk (record while held, stop on release).
/// <see cref="Toggle"/> = press once to start, again (or Escape) to stop.
/// Mirrors the macOS <c>HotkeyMode</c>.
/// </summary>
public enum HotkeyMode
{
    Hold,
    Toggle
}

public static class HotkeyModeInfo
{
    public static string DisplayName(this HotkeyMode mode) => mode switch
    {
        HotkeyMode.Hold => "Halten",
        HotkeyMode.Toggle => "Drücken",
        _ => mode.ToString()
    };

    public static string Description(this HotkeyMode mode) => mode switch
    {
        HotkeyMode.Hold => "Tasten halten zum Aufnehmen, loslassen zum Stoppen",
        HotkeyMode.Toggle => "Einmal drücken zum Starten, nochmal oder Escape zum Stoppen",
        _ => string.Empty
    };
}

/// <summary>High-level hotkey events raised by the platform hotkey service.</summary>
public enum HotkeyEventKind
{
    Down,
    Up,
    Cancel
}

public readonly record struct HotkeyEvent(HotkeyEventKind Kind, WorkflowType Type)
{
    public static HotkeyEvent Down(WorkflowType type) => new(HotkeyEventKind.Down, type);
    public static HotkeyEvent Up(WorkflowType type) => new(HotkeyEventKind.Up, type);
    public static HotkeyEvent Cancel { get; } = new(HotkeyEventKind.Cancel, WorkflowType.Transcription);
}
