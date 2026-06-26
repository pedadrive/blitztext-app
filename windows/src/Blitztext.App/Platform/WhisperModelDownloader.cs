using System.Net.Http;
using Blitztext.Core.Services;

namespace Blitztext.App.Platform;

/// <summary>
/// Downloads ggml Whisper models on demand from the official whisper.cpp model repo on
/// Hugging Face into <c>%APPDATA%\Blitztext\models\whisper</c>. Mirrors the macOS app's
/// on-demand WhisperKit model download (different source, same idea).
/// </summary>
public sealed class WhisperModelDownloader
{
    private const string BaseUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/";

    private readonly HttpClient _http;

    public WhisperModelDownloader(HttpClient? http = null) =>
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromMinutes(30) };

    public async Task DownloadAsync(string modelName, IProgress<double>? progress, CancellationToken ct = default)
    {
        Directory.CreateDirectory(AppDataPaths.ModelsDirectory);
        var destination = WhisperNetLocalTranscriber.ModelPath(modelName);
        var tempPath = destination + ".part";

        var url = BaseUrl + modelName + ".bin";
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1L;
        await using (var input = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
        await using (var output = File.Create(tempPath))
        {
            var buffer = new byte[81920];
            long read = 0;
            int n;
            while ((n = await input.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
                read += n;
                if (total > 0) progress?.Report(Math.Clamp(read / (double)total, 0, 1));
            }
        }

        if (File.Exists(destination)) File.Delete(destination);
        File.Move(tempPath, destination);
        progress?.Report(1.0);
    }
}
