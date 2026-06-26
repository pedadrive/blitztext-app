using System.Text;
using Blitztext.Core.Abstractions;
using Blitztext.Core.Services;
using Whisper.net;

namespace Blitztext.App.Platform;

/// <summary>
/// On-device transcription using Whisper.net (whisper.cpp / ggml). This is the Windows
/// equivalent of the macOS WhisperKit/CoreML path. Models are ggml <c>.bin</c> files stored
/// under <c>%APPDATA%\Blitztext\models\whisper</c>. The loaded factory is cached per model so
/// repeated dictations do not reload the model from disk.
/// </summary>
public sealed class WhisperNetLocalTranscriber : ILocalTranscriptionService, IDisposable
{
    private readonly object _gate = new();
    private string? _loadedModel;
    private WhisperFactory? _factory;

    public bool IsModelInstalled(string modelName) => File.Exists(ModelPath(modelName));

    public static string ModelPath(string modelName) =>
        Path.Combine(AppDataPaths.ModelsDirectory, modelName + ".bin");

    public async Task<string> TranscribeAsync(string audioPath, string? language, string modelName, CancellationToken ct = default)
    {
        var modelPath = ModelPath(modelName);
        if (!File.Exists(modelPath))
            throw new BlitztextException(BlitztextErrorKind.NotConfigured, $"Lokales Modell fehlt: {modelPath}");

        var factory = GetOrLoadFactory(modelName, modelPath);

        var lang = string.IsNullOrWhiteSpace(language) ? "auto" : language.Trim();
        await using var processor = factory.CreateBuilder().WithLanguage(lang).Build();

        var builder = new StringBuilder();
        await using var fileStream = File.OpenRead(audioPath);
        await foreach (var segment in processor.ProcessAsync(fileStream, ct).ConfigureAwait(false))
            builder.Append(segment.Text);

        var text = builder.ToString().Trim();
        if (text.Length == 0)
            throw new BlitztextException(BlitztextErrorKind.NoContent, "Das lokale Modell hat keinen Text erkannt.");

        return text;
    }

    private WhisperFactory GetOrLoadFactory(string modelName, string modelPath)
    {
        lock (_gate)
        {
            if (_factory is not null && _loadedModel == modelName) return _factory;
            _factory?.Dispose();
            _factory = WhisperFactory.FromPath(modelPath);
            _loadedModel = modelName;
            return _factory;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _factory?.Dispose();
            _factory = null;
            _loadedModel = null;
        }
    }
}
