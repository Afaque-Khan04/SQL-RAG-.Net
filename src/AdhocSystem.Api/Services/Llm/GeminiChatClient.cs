using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AdhocSystem.Api.Models.Common;
using Microsoft.Extensions.Options;

namespace AdhocSystem.Api.Services.Llm;

public class GeminiChatClient : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly LlmOptions _options;
    private readonly ILogger<GeminiChatClient> _logger;

    public string ProviderName => "gemini";

    public GeminiChatClient(HttpClient httpClient, IOptions<LlmOptions> options, ILogger<GeminiChatClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> CompleteAsync(IEnumerable<ChatMessage> messages, double temperature = 0.0, string? model = null, int? maxTokens = null, CancellationToken cancellationToken = default)
    {
        var apiKey = _options.GoogleApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Google Gemini API key is not configured.");
        }

        // Use Google's OpenAI-compatible endpoint
        var url = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";
        var targetModel = !string.IsNullOrWhiteSpace(model) ? model : (!string.IsNullOrWhiteSpace(_options.GeminiModel) ? _options.GeminiModel : "gemini-3.7-flash");

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var bodyObj = new Dictionary<string, object?>
        {
            ["model"] = targetModel,
            ["messages"] = messages,
            ["temperature"] = temperature,
            ["max_tokens"] = maxTokens ?? 3000
        };

        request.Content = new StringContent(JsonSerializer.Serialize(bodyObj), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Gemini API call failed with status {(int)response.StatusCode}: {responseContent}", null, response.StatusCode);
        }

        using var doc = JsonDocument.Parse(responseContent);
        var choices = doc.RootElement.GetProperty("choices");
        if (choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Gemini returned 0 choices.");
        }

        return choices[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }
}
