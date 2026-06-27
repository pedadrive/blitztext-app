using Blitztext.Core.Abstractions;
using Blitztext.Core.Models;
using Blitztext.Core.Workflows;
using Xunit;

namespace Blitztext.Core.Tests;

public class WorkflowTests
{
    private static async Task<WorkflowPhase> RunToCompletionAsync(IWorkflow workflow)
    {
        var tcs = new TaskCompletionSource<WorkflowPhase>(TaskCreationOptions.RunContinuationsAsynchronously);
        workflow.PhaseChanged += phase =>
        {
            if (phase.Kind is WorkflowPhaseKind.Done or WorkflowPhaseKind.Error)
                tcs.TrySetResult(phase);
        };

        workflow.Start();
        Assert.True(workflow.IsRecording);
        workflow.Stop();

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(completed == tcs.Task, "Workflow did not reach a terminal phase in time.");
        return await tcs.Task;
    }

    [Fact]
    public async Task Transcription_Remote_EmitsTranscriptAndDone()
    {
        var recorder = new FakeAudioRecorder();
        var remote = new FakeRemoteTranscription { Result = "hallo welt" };
        string? output = null;

        var workflow = new TranscriptionWorkflow(
            recorder, remote, new FakeLocalTranscription(),
            WorkflowType.Transcription, Array.Empty<string>(), "de",
            TranscriptionBackend.Remote, LocalModelCatalog.RecommendedFastModelName);
        workflow.Output += t => output = t;

        var phase = await RunToCompletionAsync(workflow);

        Assert.Equal(WorkflowPhaseKind.Done, phase.Kind);
        Assert.Equal("hallo welt", phase.Message);
        Assert.Equal("hallo welt", output);
    }

    [Fact]
    public async Task Transcription_Local_UsesLocalBackend()
    {
        var recorder = new FakeAudioRecorder();
        var local = new FakeLocalTranscription { Result = "lokal erkannt" };

        var workflow = new TranscriptionWorkflow(
            recorder, new FakeRemoteTranscription(), local,
            WorkflowType.LocalTranscription, Array.Empty<string>(), "de",
            TranscriptionBackend.Local, LocalModelCatalog.RecommendedFastModelName);

        var phase = await RunToCompletionAsync(workflow);

        Assert.Equal(WorkflowPhaseKind.Done, phase.Kind);
        Assert.Equal("lokal erkannt", phase.Message);
    }

    [Fact]
    public async Task TextImprovement_RunsTwoPhase()
    {
        var recorder = new FakeAudioRecorder();
        var remote = new FakeRemoteTranscription { Result = "roher text" };
        var llm = new FakeLlm { Result = "verbesserter text" };

        var workflow = new TextImprovementWorkflow(
            recorder, remote, new FakeLocalTranscription(), llm,
            new TextImprovementSettings(), "de");

        var phase = await RunToCompletionAsync(workflow);

        Assert.Equal(WorkflowPhaseKind.Done, phase.Kind);
        Assert.Equal("verbesserter text", phase.Message);
    }

    [Fact]
    public async Task DampfAblassen_SentinelFromLlm_BecomesError()
    {
        var recorder = new FakeAudioRecorder();
        var llm = new FakeLlm { Result = "KEINE_AUFNAHME_ERKANNT" };

        var workflow = new DampfAblassenWorkflow(
            recorder, new FakeRemoteTranscription(), new FakeLocalTranscription(), llm,
            new DampfAblassenSettings(), Array.Empty<string>(), "de");

        var phase = await RunToCompletionAsync(workflow);

        Assert.Equal(WorkflowPhaseKind.Error, phase.Kind);
        Assert.Equal("Keine Aufnahme erkannt.", phase.Message);
    }

    [Fact]
    public async Task Emoji_RunsTwoPhase()
    {
        var recorder = new FakeAudioRecorder();
        var llm = new FakeLlm { Result = "text 🎉" };

        var workflow = new EmojiTextWorkflow(
            recorder, new FakeRemoteTranscription(), new FakeLocalTranscription(), llm,
            new EmojiTextSettings(), Array.Empty<string>(), "de");

        var phase = await RunToCompletionAsync(workflow);

        Assert.Equal(WorkflowPhaseKind.Done, phase.Kind);
        Assert.Equal("text 🎉", phase.Message);
    }

    [Fact]
    public void Stop_TooShortRecording_IsRejected()
    {
        var recorder = new FakeAudioRecorder { LastRecordingDuration = 0.1 };
        var workflow = new TranscriptionWorkflow(
            recorder, new FakeRemoteTranscription(), new FakeLocalTranscription(),
            WorkflowType.Transcription, Array.Empty<string>(), "de",
            TranscriptionBackend.Remote, LocalModelCatalog.RecommendedFastModelName);

        workflow.Start();
        workflow.Stop();

        Assert.Equal(WorkflowPhaseKind.Error, workflow.Phase.Kind);
        Assert.True(recorder.Discarded);
    }

    [Fact]
    public async Task Transcription_TranscriptIsArtifact_BecomesError()
    {
        var recorder = new FakeAudioRecorder { LastRecordingDuration = 0.4 };
        // Short recording but long text -> artifact.
        var remote = new FakeRemoteTranscription { Result = "eins zwei drei vier fünf sechs" };

        var workflow = new TranscriptionWorkflow(
            recorder, remote, new FakeLocalTranscription(),
            WorkflowType.Transcription, Array.Empty<string>(), "de",
            TranscriptionBackend.Remote, LocalModelCatalog.RecommendedFastModelName);

        var phase = await RunToCompletionAsync(workflow);

        Assert.Equal(WorkflowPhaseKind.Error, phase.Kind);
        Assert.Equal("Keine Aufnahme erkannt.", phase.Message);
    }

    [Fact]
    public async Task RemoteError_PropagatesMessage()
    {
        var recorder = new FakeAudioRecorder();
        var remote = new FakeRemoteTranscription
        {
            ThrowOnTranscribe = Blitztext.Core.Services.BlitztextException.NotConfigured()
        };

        var workflow = new TranscriptionWorkflow(
            recorder, remote, new FakeLocalTranscription(),
            WorkflowType.Transcription, Array.Empty<string>(), "de",
            TranscriptionBackend.Remote, LocalModelCatalog.RecommendedFastModelName);

        var phase = await RunToCompletionAsync(workflow);

        Assert.Equal(WorkflowPhaseKind.Error, phase.Kind);
        Assert.Contains("API Key fehlt", phase.Message);
    }
}
