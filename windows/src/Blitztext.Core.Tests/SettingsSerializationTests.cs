using Blitztext.Core.Models;
using Blitztext.Core.Services;
using Xunit;

namespace Blitztext.Core.Tests;

public class SettingsSerializationTests
{
    [Fact]
    public void RoundTrip_PreservesValues()
    {
        var container = new SettingsContainer();
        container.App.HotkeyMode = HotkeyMode.Toggle;
        container.App.SecureLocalModeEnabled = true;
        container.Transcription.Language = "en";
        container.TextImprovement.Tone = TextTone.Formal;
        container.TextImprovement.CustomTerms.Add("Blitztext");
        container.EmojiText.EmojiDensity = EmojiDensity.Viel;

        var json = SettingsStore.Serialize(container);
        var restored = SettingsStore.Deserialize(json);

        Assert.NotNull(restored);
        Assert.Equal(HotkeyMode.Toggle, restored!.App.HotkeyMode);
        Assert.True(restored.App.SecureLocalModeEnabled);
        Assert.Equal("en", restored.Transcription.Language);
        Assert.Equal(TextTone.Formal, restored.TextImprovement.Tone);
        Assert.Contains("Blitztext", restored.TextImprovement.CustomTerms);
        Assert.Equal(EmojiDensity.Viel, restored.EmojiText.EmojiDensity);
    }

    [Fact]
    public void Defaults_AreSensible()
    {
        var container = new SettingsContainer();
        Assert.Equal(HotkeyMode.Hold, container.App.HotkeyMode);
        Assert.Equal("de", container.Transcription.Language);
        Assert.Equal(LocalModelCatalog.RecommendedFastModelName, container.App.SelectedLocalTranscriptionModelName);
        Assert.False(container.App.SecureLocalModeEnabled);
        Assert.Contains("Gib NUR die fertige Nachricht", container.DampfAblassen.SystemPrompt);
    }

    [Fact]
    public void Store_LoadMissingFile_ReturnsDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"blitztext-test-{Guid.NewGuid():N}.json");
        var store = new SettingsStore(path);
        var loaded = store.Load();
        Assert.Equal(HotkeyMode.Hold, loaded.App.HotkeyMode);
    }

    [Fact]
    public void Store_SaveThenLoad_RoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"blitztext-test-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SettingsStore(path);
            var container = new SettingsContainer();
            container.Transcription.Language = "fr";
            store.Save(container);

            var loaded = new SettingsStore(path).Load();
            Assert.Equal("fr", loaded.Transcription.Language);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Store_CorruptFile_ReturnsDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"blitztext-test-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{ not valid json ");
            var loaded = new SettingsStore(path).Load();
            Assert.Equal("de", loaded.Transcription.Language);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
