namespace AdhocSystem.Api.Services.Session;

public class TurnContext
{
    public int TurnIndex { get; set; } = 1;
    public string Query { get; set; } = string.Empty;
    public string Intent { get; set; } = string.Empty;
    public string? GeneratedSql { get; set; }
    public int TotalRows { get; set; } = 0;
    public List<Dictionary<string, object?>> SampleRows { get; set; } = new();
    public Dictionary<string, object?>? SelectedRowContext { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class SessionState
{
    public string SessionId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastAccessed { get; set; } = DateTime.UtcNow;
    public List<TurnContext> Turns { get; set; } = new();
}
