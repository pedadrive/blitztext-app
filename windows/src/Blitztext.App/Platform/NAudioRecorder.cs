using Blitztext.Core.Abstractions;
using Blitztext.Core.Services;
using NAudio.Wave;

namespace Blitztext.App.Platform;

/// <summary>
/// Microphone recorder using NAudio. Captures 16 kHz mono 16-bit PCM WAV, matching the
/// macOS recorder's 16 kHz mono format (which Whisper expects). Mirrors <c>AudioRecorder</c>.
/// </summary>
public sealed class NAudioRecorder : IAudioRecorder
{
    private static readonly WaveFormat Format = new(16000, 16, 1);

    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;
    private string? _currentFilePath;
    private long _totalBytes;

    // WaveInEvent raises RecordingStopped asynchronously on its own worker thread (not the
    // SynchronizationContext), so StopRecording can safely block on this until the WAV is
    // fully flushed and the byte count is final.
    private readonly ManualResetEventSlim _finalized = new(true);

    public bool IsRecording { get; private set; }
    public double LastRecordingDuration { get; private set; }
    public float AudioLevel { get; private set; }
    public string? RecordingPath { get; private set; }
    public string? ErrorMessage { get; private set; }

    public event Action<float>? AudioLevelChanged;

    public void StartRecording()
    {
        ErrorMessage = null;
        LastRecordingDuration = 0;
        RecordingPath = null;
        _totalBytes = 0;

        try
        {
            AppDataPaths.EnsureBaseDirectory();
            _currentFilePath = Path.Combine(Path.GetTempPath(), $"blitztext-{Guid.NewGuid():N}.wav");

            _finalized.Reset();
            _waveIn = new WaveInEvent { WaveFormat = Format, BufferMilliseconds = 50 };
            _writer = new WaveFileWriter(_currentFilePath, Format);
            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;
            _waveIn.StartRecording();
            IsRecording = true;
        }
        catch (Exception ex)
        {
            _finalized.Set();
            Cleanup();
            ErrorMessage = $"Aufnahme konnte nicht gestartet werden: {ex.Message}";
        }
    }

    public void StopRecording()
    {
        if (!IsRecording) return;
        IsRecording = false;
        try { _waveIn?.StopRecording(); } catch { /* ignore */ }

        // WaveInEvent flushes the final buffers and raises RecordingStopped on its own worker
        // thread. Wait for OnRecordingStopped to dispose the writer (finalising the WAV header)
        // and settle _totalBytes before reporting the duration / path.
        _finalized.Wait(TimeSpan.FromSeconds(3));

        LastRecordingDuration = _totalBytes / (double)Format.AverageBytesPerSecond;
        RecordingPath = _currentFilePath;
        AudioLevel = 0;
        AudioLevelChanged?.Invoke(0f);
    }

    public void DiscardRecording()
    {
        Cleanup();
        TryDelete(RecordingPath);
        TryDelete(_currentFilePath);
        RecordingPath = null;
        _currentFilePath = null;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        _writer?.Write(e.Buffer, 0, e.BytesRecorded);
        _totalBytes += e.BytesRecorded;

        // Peak amplitude → normalised 0..1 level for the live waveform.
        short peak = 0;
        for (int i = 0; i + 1 < e.BytesRecorded; i += 2)
        {
            short sample = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));
            short abs = sample == short.MinValue ? short.MaxValue : Math.Abs(sample);
            if (abs > peak) peak = abs;
        }

        AudioLevel = peak / (float)short.MaxValue;
        AudioLevelChanged?.Invoke(AudioLevel);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        // Disposing the writer finalises the WAV header so the file can be read safely.
        _writer?.Dispose();
        _writer = null;
        _waveIn?.Dispose();
        _waveIn = null;
        _finalized.Set();
    }

    private void Cleanup()
    {
        try { _waveIn?.StopRecording(); } catch { /* ignore */ }
        _writer?.Dispose();
        _writer = null;
        _waveIn?.Dispose();
        _waveIn = null;
        IsRecording = false;
        _finalized.Set();
    }

    private static void TryDelete(string? path)
    {
        if (path is null) return;
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }
}
