using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AdhocSystem.Api.Models.Common;
using Microsoft.Extensions.Options;

namespace AdhocSystem.Api.Services.Llm;

public class OpenRouterChatClient : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly LlmOptions _options;
    private readonly ILogger<OpenRouterChatClient> _logger;

    public string ProviderName => "openrouter";

    public OpenRouterChatClient(HttpClient httpClient, IOptions<LlmOptions> options, ILogger<OpenRouterChatClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> CompleteAsync(IEnumerable<ChatMessage> messages, double temperature = 0.0, string? model = null, int? maxTokens = null, CancellationToken cancellationToken = default)
    {
        var apiKey = _options.OpenRouterApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OpenRouter API key is not configured.");
        }

        var url = !string.IsNullOrWhiteSpace(_options.OpenRouterUrl) ? _options.OpenRouterUrl : "https://openrouter.ai/api/v1";
        if (!url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            url = url.TrimEnd('/') + "/chat/completions";
        }

        var targetModel = !string.IsNullOrWhiteSpace(model) ? model : (!string.IsNullOrWhiteSpace(_options.OpenRouterModel) ? _options.OpenRouterModel : "nvidia/nemotron-3-ultra-550b-a55b:free");

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Add("HTTP-Referer", "https://github.com/AdvRAG");
        request.Headers.Add("X-Title", "Advanced RAG C#");

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
            throw new HttpRequestException($"OpenRouter API call failed with status {(int)response.StatusCode}: {responseContent}", null, response.StatusCode);
        }

        using var doc = JsonDocument.Parse(responseContent);
        if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            if (doc.RootElement.TryGetProperty("error", out var errorElement))
            {
                throw new InvalidOperationException($"OpenRouter returned error: {errorElement}");
            }
            throw new InvalidOperationException($"OpenRouter returned 0 choices. Payload: {responseContent}");
        }

        return choices[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }
}
