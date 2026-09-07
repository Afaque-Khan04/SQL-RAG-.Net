using System.Text.Json.Serialization;

namespace AdhocSystem.Api.Services.Llm;

public class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    public static ChatMessage System(string content) => new() { Role = "system", Content = content };
    public static ChatMessage User(string content) => new() { Role = "user", Content = content };
    public static ChatMessage Assistant(string content) => new() { Role = "assistant", Content = content };
}

public interface IChatClient
{
    string ProviderName { get; }
    Task<string> CompleteAsync(IEnumerable<ChatMessage> messages, double temperature = 0.0, string? model = null, int? maxTokens = null, CancellationToken cancellationToken = default);
}
