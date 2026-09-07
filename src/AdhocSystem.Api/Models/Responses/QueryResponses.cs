using System.Text.Json.Serialization;
using AdhocSystem.Api.Models.Common;

namespace AdhocSystem.Api.Models.Responses;

public class QueryResponse
{
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    [JsonPropertyName("intent")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public IntentType Intent { get; set; }

    [JsonPropertyName("generated_sql")]
    public string? GeneratedSql { get; set; }

    [JsonPropertyName("results")]
    public List<Dictionary<string, object?>> Results { get; set; } = new();

    [JsonPropertyName("total_records")]
    public int TotalRecords { get; set; }

    [JsonPropertyName("is_truncated")]
    public bool IsTruncated { get; set; }

    [JsonPropertyName("answer")]
    public string Answer { get; set; } = string.Empty;

    [JsonPropertyName("execution_time_ms")]
    public double ExecutionTimeMs { get; set; }

    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("turn_index")]
    public int TurnIndex { get; set; } = 1;

    [JsonPropertyName("parent_query")]
    public string? ParentQuery { get; set; }
}

public class TranscribeResponse
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";

    [JsonPropertyName("duration")]
    public double Duration { get; set; }
}

public class AudioQueryResponse
{
    [JsonPropertyName("transcribed_text")]
    public string TranscribedText { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";

    [JsonPropertyName("audio_duration_seconds")]
    public double AudioDurationSeconds { get; set; }

    [JsonPropertyName("intent")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public IntentType Intent { get; set; }

    [JsonPropertyName("generated_sql")]
    public string? GeneratedSql { get; set; }

    [JsonPropertyName("results")]
    public List<Dictionary<string, object?>> Results { get; set; } = new();

    [JsonPropertyName("total_records")]
    public int TotalRecords { get; set; }

    [JsonPropertyName("is_truncated")]
    public bool IsTruncated { get; set; }

    [JsonPropertyName("answer")]
    public string Answer { get; set; } = string.Empty;

    [JsonPropertyName("execution_time_ms")]
    public double ExecutionTimeMs { get; set; }

    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("turn_index")]
    public int TurnIndex { get; set; } = 1;
}

public class HealthResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "ok";

    [JsonPropertyName("database")]
    public string Database { get; set; } = "unknown";

    [JsonPropertyName("vector_store")]
    public string VectorStore { get; set; } = "unknown";
}
