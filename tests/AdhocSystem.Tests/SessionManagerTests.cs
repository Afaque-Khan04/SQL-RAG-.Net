using AdhocSystem.Api.Services.Session;
using Xunit;

namespace AdhocSystem.Tests;

public class SessionManagerTests
{
    private readonly SessionManager _sessionManager = new(Microsoft.Extensions.Logging.Abstractions.NullLogger<SessionManager>.Instance);

    [Fact]
    public void CreateSession_GeneratesUniqueSession()
    {
        var s1 = _sessionManager.CreateSession();
        var s2 = _sessionManager.CreateSession();

        Assert.NotEmpty(s1);
        Assert.NotEmpty(s2);
        Assert.NotEqual(s1, s2);
    }

    [Fact]
    public void RecordTurn_RetainsTurnsAndHistory()
    {
        var sId = _sessionManager.CreateSession();
        var turn1 = _sessionManager.RecordTurn(sId, "Query 1", "structured", "SELECT 1", new List<Dictionary<string, object?>>());
        var turn2 = _sessionManager.RecordTurn(sId, "Query 2", "structured", "SELECT 2", new List<Dictionary<string, object?>>());

        Assert.Equal(1, turn1.TurnIndex);
        Assert.Equal(2, turn2.TurnIndex);

        var last = _sessionManager.GetLastTurn(sId);
        Assert.NotNull(last);
        Assert.Equal("Query 2", last.Query);

        var history = _sessionManager.GetSessionHistory(sId);
        Assert.Equal(2, history.Count);
    }
}
