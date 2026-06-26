using Blitztext.Core.Abstractions;
using Blitztext.Core.Models;
using Blitztext.Core.Services;

namespace Blitztext.Core.Workflows;

/// <summary>
/// Shared record → transcribe → (optional) rewrite state machine for all workflows.
/// Ported from the macOS workflow classes, which all repeat this pattern.
/// </summary>
public abstract class WorkflowBase : IWorkflow
{
    private const string NoRecordingSentinel = "KEINE_AUFNAHME_ERKANNT";

    protected readonly IAudioRecorder Recorder;
    protected readonly IRemoteTranscriptionService RemoteTranscription;
    protected readonly ILocalTranscriptionService LocalTranscription;

    protected readonly IReadOnlyList<string> CustomTerms;
    protected readonly string Language;
    protected readonly TranscriptionBackend Backend;
    protected readonly string LocalModelName;

    private CancellationTokenSource? _cts;
    private WorkflowPhase _phase = WorkflowPhase.Idle;

    protected WorkflowBase(
        IAudioRecorder recorder,
        IRemoteTranscriptionService remoteTranscription,
        ILocalTranscriptionService localTranscription,
        IReadOnlyList<string> customTerms,
        string language,
        TranscriptionBackend backend,
        string localModelName)
    {
        Recorder = recorder;
        RemoteTranscription = remoteTranscription;
        LocalTranscription = localTranscription;
        CustomTerms = customTerms;
        Language = language;
        Backend = backend;
        LocalModelName = localModelName;
    }

    public abstract WorkflowType Type { get; }

    public WorkflowPhase Phase
    {
        get => _phase;
        private set
        {
            _phase = value;
            PhaseChanged?.Invoke(value);
        }
    }

    public bool IsRecording => Recorder.IsRecording;

    public event Action<WorkflowPhase>? PhaseChanged;
    public event Action<string>? Output;

    /// <summary>Message shown while transcribing (remote vs. local differ).</summary>
    protected virtual string TranscribeRunningMessage =>
        Backend == TranscriptionBackend.Local ? "Wird lokal transkribiert ..." : "Wird transkribiert ...";

    public void Start()
    {
        Phase = WorkflowPhase.Running("Aufnahme läuft ...");
        Recorder.StartRecording();
        if (Recorder.ErrorMessage is { } error)
            Phase = WorkflowPhase.Error(error);
    }

    public void Stop()
    {
        if (Recorder.IsRecording)
        {
            Recorder.StopRecording();
            if (TranscriptionQualityService.ShouldRejectRecording(Recorder.LastRecordingDuration))
            {
                Recorder.DiscardRecording();
                Phase = WorkflowPhase.Error("Keine Aufnahme erkannt.");
                return;
            }

            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            _ = ProcessAsync(_cts.Token);
        }
        else
        {
            _cts?.Cancel();
            Phase = WorkflowPhase.Idle;
        }
    }

    public void Reset()
    {
        _cts?.Cancel();
        if (Recorder.IsRecording) Recorder.StopRecording();
        Recorder.DiscardRecording();
        Phase = WorkflowPhase.Idle;
    }

    private async Task ProcessAsync(CancellationToken ct)
    {
        var path = Recorder.RecordingPath;
        if (path is null)
        {
            Phase = WorkflowPhase.Error("Keine Aufnahme vorhanden.");
            return;
        }

        Phase = WorkflowPhase.Running(TranscribeRunningMessage);
        var recordingDuration = Recorder.LastRecordingDuration;
        var vocabularyHints = recordingDuration >= 0.9 ? CustomTerms : Array.Empty<string>();

        try
        {
            string rawText = Backend == TranscriptionBackend.Local
                ? await LocalTranscription.TranscribeAsync(path, Language, LocalModelName, ct).ConfigureAwait(false)
                : await RemoteTranscription.TranscribeAsync(path, vocabularyHints, Language, ct).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();

            var cleaned = TranscriptionQualityService.CleanedTranscript(rawText);
            if (TranscriptionQualityService.IsLikelyArtifact(cleaned, recordingDuration))
            {
                Phase = WorkflowPhase.Error("Keine Aufnahme erkannt.");
                return;
            }

            var final = await RewriteAsync(cleaned, ct).ConfigureAwait(false);
            final = TranscriptionQualityService.CleanedTranscript(final);

            if (final == NoRecordingSentinel)
            {
                Phase = WorkflowPhase.Error("Keine Aufnahme erkannt.");
                return;
            }

            Phase = WorkflowPhase.Done(final);
            Output?.Invoke(final);
        }
        catch (OperationCanceledException)
        {
            // Cancelled by Reset/Stop — leave phase as-is.
        }
        catch (Exception ex)
        {
            Phase = WorkflowPhase.Error(ex.Message);
        }
        finally
        {
            // Remote transcription deletes the file itself on success, but early throws
            // (not-configured / missing file) and the local backend don't — so always try.
            try { File.Delete(path); } catch { /* best effort */ }
        }
    }

    /// <summary>
    /// Phase-2 rewrite hook. Pure transcription returns the transcript unchanged; the
    /// rewrite workflows override this to call the LLM. May set an intermediate Running phase.
    /// </summary>
    protected virtual Task<string> RewriteAsync(string transcript, CancellationToken ct) =>
        Task.FromResult(transcript);

    /// <summary>Set a Running phase from inside <see cref="RewriteAsync"/>.</summary>
    protected void SetRunning(string message) => Phase = WorkflowPhase.Running(message);
}
