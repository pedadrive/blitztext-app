using Blitztext.Core;
using Blitztext.Core.Abstractions;
using Blitztext.Core.Models;
using Blitztext.Core.Services;
using Blitztext.Core.Workflows;
using Xunit;

namespace Blitztext.Core.Tests;

public class AppCoordinatorTests
{
    private static (AppCoordinator coord, FakeCredentialStore creds, FakeLocalTranscription local, FakePasteService paste)
        Build(string? settingsPath = null)
    {
        var path = settingsPath ?? Path.Combine(Path.GetTempPath(), $"blitztext-coord-{Guid.NewGuid():N}.json");
        var store = new SettingsStore(path);
        var creds = new FakeCredentialStore();
        var local = new FakeLocalTranscription();
        var paste = new FakePasteService();
        var factory = new WorkflowFactory(() => new FakeAudioRecorder(), new FakeRemoteTranscription(), local, new FakeLlm());
        var coord = new AppCoordinator(store, factory, creds, paste, local);
        return (coord, creds, local, paste);
    }

    [Fact]
    public void NotConfigured_NoKeyNoModel_ShowsOnboarding()
    {
        var (coord, _, _, _) = Build();
        Assert.False(coord.IsConfigured);
        Assert.True(coord.ShouldShowOnboarding);
    }

    [Fact]
    public void ApiKeyConfigured_IsConfigured()
    {
        var (coord, creds, _, _) = Build();
        coord.SetApiKey("sk-test-123");
        Assert.True(coord.IsConfigured);
        Assert.Equal("sk-test-123", creds.Load(CredentialKey.OpenAiApiKey));
    }

    [Fact]
    public void SetApiKey_Empty_Deletes()
    {
        var (coord, creds, _, _) = Build();
        coord.SetApiKey("sk-test");
        coord.SetApiKey("   ");
        Assert.False(creds.HasValue(CredentialKey.OpenAiApiKey));
    }

    [Fact]
    public void RewriteWorkflows_RequireApiKey_AndNotLocalMode()
    {
        var (coord, creds, _, _) = Build();
        Assert.False(coord.IsWorkflowAvailable(WorkflowType.TextImprover));

        coord.SetApiKey("sk-test");
        Assert.True(coord.IsWorkflowAvailable(WorkflowType.TextImprover));

        coord.Settings.App.SecureLocalModeEnabled = true;
        Assert.False(coord.IsWorkflowAvailable(WorkflowType.TextImprover));
    }

    [Fact]
    public void LocalTranscription_RequiresInstalledModel()
    {
        var (coord, _, local, _) = Build();
        Assert.False(coord.IsWorkflowAvailable(WorkflowType.LocalTranscription));

        local.InstalledModels.Add(coord.SelectedLocalModelName);
        Assert.True(coord.IsWorkflowAvailable(WorkflowType.LocalTranscription));
    }

    [Fact]
    public void Transcription_InLocalMode_NeedsModel_NotKey()
    {
        var (coord, creds, local, _) = Build();
        coord.SetApiKey("sk-test");
        coord.Settings.App.SecureLocalModeEnabled = true;

        // Local mode on, but no model installed yet.
        Assert.False(coord.IsWorkflowAvailable(WorkflowType.Transcription));

        local.InstalledModels.Add(coord.SelectedLocalModelName);
        Assert.True(coord.IsWorkflowAvailable(WorkflowType.Transcription));
    }

    [Fact]
    public void StartWorkflow_Unavailable_DoesNothing()
    {
        var (coord, _, _, paste) = Build();
        coord.StartWorkflow(WorkflowType.TextImprover); // no key
        Assert.Null(coord.ActiveWorkflow);
        Assert.Equal(0, paste.CaptureCount);
    }

    [Fact]
    public void StartWorkflow_Available_CapturesTargetAndStarts()
    {
        var (coord, creds, _, paste) = Build();
        coord.SetApiKey("sk-test");

        MenuBarStatus? seen = null;
        coord.MenuBarStatusChanged += s => seen = s;

        coord.StartWorkflow(WorkflowType.Transcription);

        Assert.NotNull(coord.ActiveWorkflow);
        Assert.Equal(WorkflowType.Transcription, coord.ActiveWorkflow!.Type);
        Assert.Equal(1, paste.CaptureCount);
        Assert.Equal(MenuBarStatusKind.Recording, seen!.Value.Kind);
    }

    [Fact]
    public void Settings_PersistAcrossInstances()
    {
        var path = Path.Combine(Path.GetTempPath(), $"blitztext-coord-{Guid.NewGuid():N}.json");
        try
        {
            var (coord, _, _, _) = Build(path);
            coord.Settings.Transcription.Language = "en";
            coord.SaveSettings();

            var (coord2, _, _, _) = Build(path);
            Assert.Equal("en", coord2.Settings.Transcription.Language);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
