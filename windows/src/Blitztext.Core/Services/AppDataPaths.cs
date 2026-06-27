namespace Blitztext.Core.Services;

/// <summary>
/// Resolves the per-user data directory. On Windows this is
/// <c>%APPDATA%\Blitztext</c>, mirroring the macOS app-support folder.
/// </summary>
public static class AppDataPaths
{
    public const string FolderName = "Blitztext";

    public static string BaseDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), FolderName);

    public static string SettingsFilePath => Path.Combine(BaseDirectory, "settings.json");

    public static string ModelsDirectory => Path.Combine(BaseDirectory, "models", "whisper");

    public static void EnsureBaseDirectory() => Directory.CreateDirectory(BaseDirectory);
}
