using System.Text.Json.Serialization;

namespace AdhocSystem.Api.Models.Requests;

public class QueryRequest
{
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

    [JsonPropertyName("is_follow_up")]
    public bool IsFollowUp { get; set; } = false;

    [JsonPropertyName("top_k")]
    public int? TopK { get; set; }

    [JsonPropertyName("selected_row_context")]
    public Dictionary<string, object?>? SelectedRowContext { get; set; }
}

public class AudioQueryRequest
{
    public IFormFile? File { get; set; }
    public string? SessionId { get; set; }
    public bool IsFollowUp { get; set; } = false;
}
