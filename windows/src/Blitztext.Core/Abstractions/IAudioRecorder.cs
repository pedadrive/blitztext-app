namespace Blitztext.Core.Abstractions;

/// <summary>
/// Microphone recorder abstraction. The Windows implementation uses NAudio to capture
/// 16 kHz mono PCM WAV (matching the macOS recorder's 16 kHz mono settings).
/// </summary>
public interface IAudioRecorder
{
    bool IsRecording { get; }

    /// <summary>Duration of the most recently finished recording, in seconds.</summary>
    double LastRecordingDuration { get; }

    /// <summary>Normalised 0..1 input level for the live waveform.</summary>
    float AudioLevel { get; }

    /// <summary>Path to the finished recording file, or null if none.</summary>
    string? RecordingPath { get; }

    /// <summary>Set when starting failed; null on success.</summary>
    string? ErrorMessage { get; }

    /// <summary>Raised whenever <see cref="AudioLevel"/> changes (UI metering).</summary>
    event Action<float>? AudioLevelChanged;

    void StartRecording();
    void StopRecording();
    void DiscardRecording();
}
