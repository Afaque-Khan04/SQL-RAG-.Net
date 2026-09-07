using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AdhocSystem.Api.Models.Common;
using Microsoft.Extensions.Options;

namespace AdhocSystem.Api.Services.Llm;

public class NvidiaNimChatClient : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly LlmOptions _options;
    private readonly ILogger<NvidiaNimChatClient> _logger;

    public string ProviderName => "nvidia_nim";

    public NvidiaNimChatClient(HttpClient httpClient, IOptions<LlmOptions> options, ILogger<NvidiaNimChatClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> CompleteAsync(IEnumerable<ChatMessage> messages, double temperature = 0.0, string? model = null, int? maxTokens = null, CancellationToken cancellationToken = default)
    {
        var apiKey = _options.NvidiaApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("NVIDIA API key is not configured.");
        }

        var url = !string.IsNullOrWhiteSpace(_options.NvidiaUrl) ? _options.NvidiaUrl : "https://integrate.api.nvidia.com/v1";
        if (!url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            url = url.TrimEnd('/') + "/chat/completions";
        }

        var targetModel = !string.IsNullOrWhiteSpace(model) ? model : (!string.IsNullOrWhiteSpace(_options.NvidiaModel) ? _options.NvidiaModel : "minimaxai/minimax-m3");

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
            throw new HttpRequestException($"NVIDIA NIM API call failed with status {(int)response.StatusCode}: {responseContent}", null, response.StatusCode);
        }

        using var doc = JsonDocument.Parse(responseContent);
        var choices = doc.RootElement.GetProperty("choices");
        if (choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("NVIDIA NIM returned 0 choices.");
        }

        return choices[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }
}
