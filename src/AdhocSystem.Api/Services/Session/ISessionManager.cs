namespace AdhocSystem.Api.Services.Session;

public interface ISessionManager
{
    string CreateSession();
    (string sessionId, SessionState state) GetOrCreateSession(string? sessionId);
    TurnContext RecordTurn(string sessionId, string query, string intent, string? generatedSql, List<Dictionary<string, object?>> results, Dictionary<string, object?>? selectedRowContext = null);
    TurnContext? GetLastTurn(string? sessionId);
    List<TurnContext> GetSessionHistory(string? sessionId);
    void ClearSession(string sessionId);
}
