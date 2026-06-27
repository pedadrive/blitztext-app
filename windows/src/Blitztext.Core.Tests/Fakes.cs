using Blitztext.Core.Abstractions;
using Blitztext.Core.Models;

namespace Blitztext.Core.Tests;

internal sealed class FakeAudioRecorder : IAudioRecorder
{
    public bool IsRecording { get; private set; }
    public double LastRecordingDuration { get; set; } = 2.0;
    public float AudioLevel { get; private set; }
    public string? RecordingPath { get; set; }
    public string? ErrorMessage { get; set; }
    public event Action<float>? AudioLevelChanged;

    public bool Discarded { get; private set; }

    public void StartRecording()
    {
        IsRecording = true;
        // Create a throwaway file so File.Delete in the workflow has something real.
        RecordingPath ??= Path.Combine(Path.GetTempPath(), $"blitztext-fake-{Guid.NewGuid():N}.wav");
        File.WriteAllText(RecordingPath, "fake audio");
        AudioLevelChanged?.Invoke(0f);
    }

    public void StopRecording() => IsRecording = false;

    public void DiscardRecording()
    {
        Discarded = true;
        if (RecordingPath is not null && File.Exists(RecordingPath)) File.Delete(RecordingPath);
    }
}

internal sealed class FakeRemoteTranscription : IRemoteTranscriptionService
{
    public string Result { get; set; } = "transkribierter text";
    public Exception? ThrowOnTranscribe { get; set; }
    public IReadOnlyList<string>? LastCustomTerms { get; private set; }

    public Task<string> TranscribeAsync(string audioPath, IReadOnlyList<string> customTerms, string? language, CancellationToken ct = default)
    {
        LastCustomTerms = customTerms;
        if (ThrowOnTranscribe is not null) return Task.FromException<string>(ThrowOnTranscribe);
        return Task.FromResult(Result);
    }
}

internal sealed class FakeLocalTranscription : ILocalTranscriptionService
{
    public string Result { get; set; } = "lokaler text";
    public HashSet<string> InstalledModels { get; } = new();

    public Task<string> TranscribeAsync(string audioPath, string? language, string modelName, CancellationToken ct = default)
        => Task.FromResult(Result);

    public bool IsModelInstalled(string modelName) => InstalledModels.Contains(modelName);
}

internal sealed class FakeLlm : ILlmService
{
    public string Result { get; set; } = "umgeschriebener text";
    public Task<string> ImproveAsync(string text, TextImprovementSettings settings, CancellationToken ct = default) => Task.FromResult(Result);
    public Task<string> DampfAblassenAsync(string text, string systemPrompt, CancellationToken ct = default) => Task.FromResult(Result);
    public Task<string> AddEmojisAsync(string text, EmojiTextSettings settings, CancellationToken ct = default) => Task.FromResult(Result);
}

internal sealed class FakeCredentialStore : ICredentialStore
{
    private readonly Dictionary<CredentialKey, string> _store = new();
    public void Save(CredentialKey key, string value) => _store[key] = value;
    public string? Load(CredentialKey key) => _store.TryGetValue(key, out var v) ? v : null;
    public void Delete(CredentialKey key) => _store.Remove(key);
    public bool HasValue(CredentialKey key) => _store.ContainsKey(key) && !string.IsNullOrEmpty(_store[key]);
}

internal sealed class FakePasteTarget : IPasteTarget { }

internal sealed class FakePasteService : IPasteService
{
    public string? LastPasted { get; private set; }
    public int CaptureCount { get; private set; }

    public IPasteTarget? CaptureForegroundTarget() { CaptureCount++; return new FakePasteTarget(); }
    public void CopyToClipboard(string text) => LastPasted = text;
    public bool PasteAtCursor(string text, IPasteTarget? target) { LastPasted = text; return true; }
}
