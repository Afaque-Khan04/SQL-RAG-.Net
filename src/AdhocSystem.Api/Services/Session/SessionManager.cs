using System.Collections.Concurrent;

namespace AdhocSystem.Api.Services.Session;

public class SessionManager : ISessionManager
{
    private readonly TimeSpan _ttl = TimeSpan.FromHours(2);
    private readonly ConcurrentDictionary<string, SessionState> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<SessionManager> _logger;

    public SessionManager(ILogger<SessionManager> logger)
    {
        _logger = logger;
    }

    public string CreateSession()
    {
        CleanupExpired();
        var sessionId = $"sess_{Guid.NewGuid():N}"[..17];
        var state = new SessionState { SessionId = sessionId };
        _sessions[sessionId] = state;
        _logger.LogInformation("Initialized new session: {SessionId}", sessionId);
        return sessionId;
    }

    public (string sessionId, SessionState state) GetOrCreateSession(string? sessionId)
    {
        CleanupExpired();
        if (!string.IsNullOrWhiteSpace(sessionId) && _sessions.TryGetValue(sessionId, out var existing))
        {
            existing.LastAccessed = DateTime.UtcNow;
            return (sessionId, existing);
        }

        var newId = CreateSession();
        return (newId, _sessions[newId]);
    }

    public TurnContext RecordTurn(
        string sessionId,
        string query,
        string intent,
        string? generatedSql,
        List<Dictionary<string, object?>> results,
        Dictionary<string, object?>? selectedRowContext = null)
    {
        var session = _sessions.GetOrAdd(sessionId, sid => new SessionState { SessionId = sid });
        session.LastAccessed = DateTime.UtcNow;

        var turnIndex = session.Turns.Count + 1;
        var sampleRows = results.Take(10).ToList();

        var turn = new TurnContext
        {
            TurnIndex = turnIndex,
            Query = query,
            Intent = intent,
            GeneratedSql = generatedSql,
            TotalRows = results.Count,
            SampleRows = sampleRows,
            SelectedRowContext = selectedRowContext,
            Timestamp = DateTime.UtcNow
        };

        lock (session.Turns)
        {
            session.Turns.Add(turn);
        }

        _logger.LogInformation("Recorded Turn #{Turn} for session {SessionId} (Rows: {Count})", turnIndex, sessionId, results.Count);
        return turn;
    }

    public TurnContext? GetLastTurn(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || !_sessions.TryGetValue(sessionId, out var session))
        {
            return null;
        }

        lock (session.Turns)
        {
            return session.Turns.LastOrDefault();
        }
    }

    public List<TurnContext> GetSessionHistory(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || !_sessions.TryGetValue(sessionId, out var session))
        {
            return new List<TurnContext>();
        }

        lock (session.Turns)
        {
            return session.Turns.ToList();
        }
    }

    public void ClearSession(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            lock (session.Turns)
            {
                session.Turns.Clear();
            }
            session.LastAccessed = DateTime.UtcNow;
            _logger.LogInformation("Cleared session history for: {SessionId}", sessionId);
        }
    }

    private void CleanupExpired()
    {
        var cutoff = DateTime.UtcNow - _ttl;
        var expiredKeys = _sessions
            .Where(pair => pair.Value.LastAccessed < cutoff)
            .Select(pair => pair.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _sessions.TryRemove(key, out _);
        }
    }
}
