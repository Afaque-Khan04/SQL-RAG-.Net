using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AdhocSystem.Api.Models.Common;
using Microsoft.Extensions.Options;

namespace AdhocSystem.Api.Services.Llm;

public class GroqChatClient : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly LlmOptions _options;
    private readonly ILogger<GroqChatClient> _logger;

    public string ProviderName => "groq";

    public GroqChatClient(HttpClient httpClient, IOptions<LlmOptions> options, ILogger<GroqChatClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> CompleteAsync(IEnumerable<ChatMessage> messages, double temperature = 0.0, string? model = null, int? maxTokens = null, CancellationToken cancellationToken = default)
    {
        var apiKey = _options.GroqApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Groq API key is not configured.");
        }

        var targetModel = !string.IsNullOrWhiteSpace(model) ? model : (!string.IsNullOrWhiteSpace(_options.GroqModel) ? _options.GroqModel : "openai/gpt-oss-120b");
        
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var bodyObj = new Dictionary<string, object?>
        {
            ["model"] = targetModel,
            ["messages"] = messages,
            ["temperature"] = temperature,
            ["max_tokens"] = maxTokens ?? 3000
        };

        if (!string.IsNullOrWhiteSpace(_options.ReasoningEffort))
        {
            bodyObj["reasoning_effort"] = _options.ReasoningEffort;
        }

        request.Content = new StringContent(JsonSerializer.Serialize(bodyObj), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Groq API call failed with status {(int)response.StatusCode}: {responseContent}", null, response.StatusCode);
        }

        using var doc = JsonDocument.Parse(responseContent);
        var choices = doc.RootElement.GetProperty("choices");
        if (choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Groq returned 0 choices.");
        }

        return choices[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }
}
