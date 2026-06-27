using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Blitztext.Core.Abstractions;
using Blitztext.Core.Models;

namespace Blitztext.Core.Services;

/// <summary>
/// Text rewriting via the OpenAI Chat Completions API. Ported from the macOS
/// <c>LLMService</c>: fast edits use <c>gpt-4o-mini</c>, "Dampf ablassen" uses <c>gpt-4o</c>.
/// </summary>
public sealed class OpenAiLlmService : ILlmService
{
    private const string ChatCompletionsUrl = "https://api.openai.com/v1/chat/completions";
    private const string FastEditModel = "gpt-4o-mini";
    private const string RageModel = "gpt-4o";

    private readonly HttpClient _http;
    private readonly ICredentialStore _credentials;

    public OpenAiLlmService(HttpClient http, ICredentialStore credentials)
    {
        _http = http;
        _credentials = credentials;
    }

    public Task<string> ImproveAsync(string text, TextImprovementSettings settings, CancellationToken ct = default) =>
        CompleteAsync(text, LlmPromptBuilder.BuildImproveSystemPrompt(settings), FastEditModel, 0.3, ct);

    public Task<string> DampfAblassenAsync(string text, string systemPrompt, CancellationToken ct = default) =>
        CompleteAsync(text, systemPrompt, RageModel, 0.4, ct);

    public Task<string> AddEmojisAsync(string text, EmojiTextSettings settings, CancellationToken ct = default) =>
        CompleteAsync(text, LlmPromptBuilder.BuildEmojiSystemPrompt(settings.EmojiDensity), FastEditModel, 0.3, ct);

    private async Task<string> CompleteAsync(string text, string systemPrompt, string model, double temperature, CancellationToken ct)
    {
        var apiKey = _credentials.Load(CredentialKey.OpenAiApiKey);
        if (string.IsNullOrEmpty(apiKey)) throw BlitztextException.NotConfigured();

        var payload = new ChatRequest
        {
            Model = model,
            Temperature = temperature,
            Messages = new[]
            {
                new ChatRequest.Message { Role = "system", Content = systemPrompt },
                new ChatRequest.Message { Role = "user", Content = text }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, ChatCompletionsUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

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

        var content = JsonSerializer.Deserialize<ChatResponse>(body)?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content)) throw BlitztextException.NoContent();

        return content.Trim();
    }

    internal static string? ExtractError(string body)
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

    private sealed class ChatRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = "";
        [JsonPropertyName("messages")] public Message[] Messages { get; set; } = Array.Empty<Message>();
        [JsonPropertyName("temperature")] public double Temperature { get; set; }

        public sealed class Message
        {
            [JsonPropertyName("role")] public string Role { get; set; } = "";
            [JsonPropertyName("content")] public string Content { get; set; } = "";
        }
    }

    private sealed class ChatResponse
    {
        [JsonPropertyName("choices")] public List<Choice>? Choices { get; set; }

        public sealed class Choice
        {
            [JsonPropertyName("message")] public Msg? Message { get; set; }
            public sealed class Msg { [JsonPropertyName("content")] public string? Content { get; set; } }
        }
    }

    private sealed class ErrorResponse
    {
        [JsonPropertyName("error")] public Err? Error { get; set; }
        public sealed class Err { [JsonPropertyName("message")] public string? Message { get; set; } }
    }
}
