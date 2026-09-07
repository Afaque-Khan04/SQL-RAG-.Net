using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using AdhocSystem.Api.Models.Common;
using Microsoft.Extensions.Options;

namespace AdhocSystem.Api.Services.Llm;

public class CircuitBreakerLlm : ICircuitBreakerLlm
{
    private readonly IEnumerable<IChatClient> _clients;
    private readonly LlmOptions _options;
    private readonly ILogger<CircuitBreakerLlm> _logger;

    private class ProviderHealth
    {
        public DateTime CooldownUntil { get; set; } = DateTime.MinValue;
        public string Reason { get; set; } = string.Empty;
        public int FailureCount { get; set; } = 0;
    }

    private static readonly ConcurrentDictionary<string, ProviderHealth> ProviderHealthMap = new(StringComparer.OrdinalIgnoreCase);

    public CircuitBreakerLlm(IEnumerable<IChatClient> clients, IOptions<LlmOptions> options, ILogger<CircuitBreakerLlm> logger)
    {
        _clients = clients;
        _options = options.Value;
        _logger = logger;
    }

    private static (TimeSpan cooldown, string reason) ClassifyError(Exception ex)
    {
        var msg = ex.Message.ToLowerInvariant();

        if (msg.Contains("429") || msg.Contains("too many requests") || msg.Contains("rate limit") || msg.Contains("quota"))
        {
            var match = Regex.Match(msg, @"(?:retry[\s_-]?after|try again in|wait)\s*:?\s*(?:(\d+)m)?\s*(\d+(?:\.\d+)?)?\s*(?:s|sec|seconds)?", RegexOptions.IgnoreCase);
            var secs = 15.0;
            if (match.Success)
            {
                var mins = match.Groups[1].Success && double.TryParse(match.Groups[1].Value, out var m) ? m : 0.0;
                var s = match.Groups[2].Success && double.TryParse(match.Groups[2].Value, out var secVal) ? secVal : 0.0;
                var total = (mins * 60.0) + s;
                if (total > 0)
                {
                    secs = Math.Clamp(total, 5.0, 60.0);
                }
            }
            return (TimeSpan.FromSeconds(secs), $"Rate Limit 429 ({secs:F1}s cooldown)");
        }

        if (msg.Contains("404") || msg.Contains("model not found") || msg.Contains("does not exist"))
        {
            return (TimeSpan.FromSeconds(300), "Model Not Found 404 (300s cooldown)");
        }

        if (msg.Contains("400") || msg.Contains("context_length_exceeded") || msg.Contains("maximum context length"))
        {
            return (TimeSpan.FromSeconds(60), "Bad Request / Context Limit 400 (60s cooldown)");
        }

        if (msg.Contains("500") || msg.Contains("502") || msg.Contains("503") || msg.Contains("504") || msg.Contains("bad gateway") || msg.Contains("service unavailable"))
        {
            return (TimeSpan.FromSeconds(30), "Service Unavailable 50x (30s cooldown)");
        }

        if (msg.Contains("timeout") || msg.Contains("timed out") || msg.Contains("connection"))
        {
            return (TimeSpan.FromSeconds(20), "Connection Timeout (20s cooldown)");
        }

        return (TimeSpan.FromSeconds(15), $"General Error (15s cooldown): {ex.Message.Substring(0, Math.Min(ex.Message.Length, 60))}");
    }

    private static bool IsAvailable(string provider)
    {
        if (!ProviderHealthMap.TryGetValue(provider, out var health))
        {
            return true;
        }

        var now = DateTime.UtcNow;
        return now >= health.CooldownUntil;
    }

    private void RecordFailure(string provider, Exception ex)
    {
        var (cooldown, reason) = ClassifyError(ex);
        var health = ProviderHealthMap.GetOrAdd(provider, _ => new ProviderHealth());
        health.FailureCount++;
        health.CooldownUntil = DateTime.UtcNow.Add(cooldown);
        health.Reason = reason;

        _logger.LogWarning("⚠️ Circuit Breaker Triggered for '{Provider}': {Reason}. Tripping cooldown for {Secs:F1}s.",
            provider, reason, cooldown.TotalSeconds);
    }

    private void RecordSuccess(string provider)
    {
        if (ProviderHealthMap.TryGetValue(provider, out var health))
        {
            health.FailureCount = 0;
            health.CooldownUntil = DateTime.MinValue;
            health.Reason = string.Empty;
        }
    }

    public async Task<string> CompleteWithFallbackAsync(
        IEnumerable<ChatMessage> messages,
        double temperature = 0.0,
        string? model = null,
        int? maxTokens = null,
        CancellationToken cancellationToken = default)
    {
        var primaryName = _options.Provider.ToLowerInvariant();
        var clientList = _clients.ToList();

        // Order clients: primary provider first, then remaining in fallback order
        var orderedClients = clientList
            .OrderByDescending(c => c.ProviderName.Equals(primaryName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // 1. Filter out clients currently in active cooldown
        var candidates = orderedClients.Where(c => IsAvailable(c.ProviderName)).ToList();

        // 2. If all in cooldown, fallback to trying all clients
        if (!candidates.Any())
        {
            candidates = orderedClients;
        }

        var errors = new List<string>();

        foreach (var client in candidates)
        {
            try
            {
                _logger.LogInformation("Invoking LLM via provider: '{Provider}'", client.ProviderName);
                var result = await client.CompleteAsync(messages, temperature, model, maxTokens, cancellationToken);
                RecordSuccess(client.ProviderName);
                return result;
            }
            catch (Exception ex)
            {
                RecordFailure(client.ProviderName, ex);
                errors.Add($"[{client.ProviderName}]: {ex.Message}");
                _logger.LogWarning("Provider '{Provider}' failed: {Msg}. Cascading to next fallback tier...", client.ProviderName, ex.Message);
            }
        }

        var summary = string.Join(" | ", errors);
        throw new InvalidOperationException($"All LLM providers in fallback chain failed. Errors: {summary}");
    }
}
