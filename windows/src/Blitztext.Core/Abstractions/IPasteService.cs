namespace Blitztext.Core.Abstractions;

/// <summary>
/// Opaque handle to the app/window that had focus when a workflow was launched, so the
/// result can be pasted back into it. The Windows implementation wraps the foreground
/// window handle + process id.
/// </summary>
public interface IPasteTarget
{
}

/// <summary>
/// Clipboard + auto-paste. Mirrors the macOS AppState paste logic: put the text on the
/// clipboard (as a fallback), restore focus to the original app, then synthesize Ctrl+V.
/// </summary>
public interface IPasteService
{
    /// <summary>Snapshot the currently focused foreground app to paste into later.</summary>
    IPasteTarget? CaptureForegroundTarget();

    /// <summary>Copy text to the clipboard, marking it as transient/concealed where supported.</summary>
    void CopyToClipboard(string text);

    /// <summary>
    /// Copy <paramref name="text"/>, restore focus to <paramref name="target"/> if needed,
    /// then synthesize a paste. Returns false if accessibility/permission prevents pasting
    /// (the text still remains on the clipboard as a fallback).
    /// </summary>
    bool PasteAtCursor(string text, IPasteTarget? target);
}
