namespace Blitztext.Core.Models;

public enum MenuBarStatusKind
{
    Idle,
    Recording,
    Processing,
    Success,
    Error
}

/// <summary>
/// Tray-icon status, mirroring the macOS <c>MenuBarStatus</c>. The optional
/// <see cref="Type"/> identifies which workflow is active (for accent colour / tooltip).
/// </summary>
public readonly record struct MenuBarStatus(MenuBarStatusKind Kind, WorkflowType? Type)
{
    public static MenuBarStatus Idle { get; } = new(MenuBarStatusKind.Idle, null);
    public static MenuBarStatus Recording(WorkflowType type) => new(MenuBarStatusKind.Recording, type);
    public static MenuBarStatus Processing(WorkflowType type) => new(MenuBarStatusKind.Processing, type);
    public static MenuBarStatus Success(WorkflowType type) => new(MenuBarStatusKind.Success, type);
    public static MenuBarStatus Error(WorkflowType? type) => new(MenuBarStatusKind.Error, type);

    public string Tooltip => Kind switch
    {
        MenuBarStatusKind.Idle => "Blitztext",
        MenuBarStatusKind.Recording => "Aufnahme läuft …",
        MenuBarStatusKind.Processing => "Wird verarbeitet …",
        MenuBarStatusKind.Success => "Fertig",
        MenuBarStatusKind.Error => "Fehler",
        _ => "Blitztext"
    };
}
