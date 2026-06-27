using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Blitztext.Core.Abstractions;

namespace Blitztext.Core.Services;

/// <summary>
/// Online transcription via the OpenAI Audio Transcriptions API (<c>whisper-1</c>).
/// Ported from the macOS <c>TranscriptionService</c>. The audio file is deleted after
/// the request, like the original.
/// </summary>
public sealed class OpenAiTranscriptionService : IRemoteTranscriptionService
{
    private const string RemoteModel = "whisper-1";
    private const string TranscriptionsUrl = "https://api.openai.com/v1/audio/transcriptions";

    private readonly HttpClient _http;
    private readonly ICredentialStore _credentials;

    public OpenAiTranscriptionService(HttpClient http, ICredentialStore credentials)
    {
        _http = http;
        _credentials = credentials;
    }

    public async Task<string> TranscribeAsync(
        string audioPath,
        IReadOnlyList<string> customTerms,
        string? language,
        CancellationToken ct = default)
    {
        var apiKey = _credentials.Load(CredentialKey.OpenAiApiKey);
        if (string.IsNullOrEmpty(apiKey)) throw BlitztextException.NotConfigured();

        if (!File.Exists(audioPath)) throw BlitztextException.NoFile();

        try
        {
            using var form = new MultipartFormDataContent();

            var audioBytes = await File.ReadAllBytesAsync(audioPath, ct).ConfigureAwait(false);
            var audioContent = new ByteArrayContent(audioBytes);
            audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            form.Add(audioContent, "file", "audio.wav");

            form.Add(new StringContent(RemoteModel), "model");
            form.Add(new StringContent("text"), "response_format");

            if (customTerms.Count > 0)
            {
                var prompt = "Eigennamen und Begriffe: " + string.Join(", ", customTerms);
                form.Add(new StringContent(prompt), "prompt");
            }

            if (!string.IsNullOrWhiteSpace(language))
                form.Add(new StringContent(language.Trim()), "language");

            using var request = new HttpRequestMessage(HttpMethod.Post, TranscriptionsUrl) { Content = form };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                throw BlitztextException.Network("Zeitüberschreitung");
            }
            catch (HttpRequestException ex)
            {
                throw BlitztextException.Network(ex.Message);
            }

            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                throw BlitztextException.Api(ExtractError(body) ?? $"Status {(int)response.StatusCode}");

            var text = body.Trim();
            if (text.Length == 0) throw BlitztextException.Api("Transkription fehlgeschlagen");

            return text;
        }
        finally
        {
            try { File.Delete(audioPath); } catch { /* best effort, matches macOS defer */ }
        }
    }

    private static string? ExtractError(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<ErrorResponse>(body)?.Error?.Message;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class ErrorResponse
    {
        [JsonPropertyName("error")] public Err? Error { get; set; }
        public sealed class Err { [JsonPropertyName("message")] public string? Message { get; set; } }
    }
}
