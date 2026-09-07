namespace AdhocSystem.Api.Services.Llm;

public interface ICircuitBreakerLlm
{
    Task<string> CompleteWithFallbackAsync(IEnumerable<ChatMessage> messages, double temperature = 0.0, string? model = null, int? maxTokens = null, CancellationToken cancellationToken = default);
}
