using System.Net.Http.Headers;
using System.Text.Json;
using AdhocSystem.Api.Models.Common;
using Microsoft.Extensions.Options;

namespace AdhocSystem.Api.Services.Audio;

public class SpeechToTextService : ISpeechToTextService
{
    private readonly HttpClient _httpClient;
    private readonly LlmOptions _options;
    private readonly ILogger<SpeechToTextService> _logger;

    public SpeechToTextService(HttpClient httpClient, IOptions<LlmOptions> options, ILogger<SpeechToTextService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<(string text, string language, double duration)> TranscribeAudioAsync(
        Stream audioStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Transcribing audio file: {FileName}...", fileName);

        var apiKey = _options.GroqApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Groq API key is not configured for Whisper transcription.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/audio/transcriptions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(audioStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

        content.Add(streamContent, "file", fileName);
        content.Add(new StringContent("whisper-large-v3-turbo"), "model");
        content.Add(new StringContent("verbose_json"), "response_format");

        request.Content = content;

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Audio transcription failed: {Error}", responseBody);
            throw new HttpRequestException($"Audio transcription failed: {responseBody}", null, response.StatusCode);
        }

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        var text = root.GetProperty("text").GetString() ?? string.Empty;
        var language = root.TryGetProperty("language", out var langProp) ? langProp.GetString() ?? "en" : "en";
        var duration = root.TryGetProperty("duration", out var durProp) ? durProp.GetDouble() : 0.0;

        _logger.LogInformation("Audio transcription complete: '{Text}' ({Language}, {Duration:F2}s)", text, language, duration);
        return (text, language, duration);
    }
}
