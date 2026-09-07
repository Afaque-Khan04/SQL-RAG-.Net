using AdhocSystem.Api.Services.Session;

namespace AdhocSystem.Api.Services.NL2SQL;

public interface INl2SqlService
{
    Task<(string generatedSql, List<Dictionary<string, object?>> results)> GenerateAndExecuteAsync(
        string query,
        TurnContext? previousTurn = null,
        Dictionary<string, object?>? selectedRowContext = null,
        bool isFollowUp = false,
        CancellationToken cancellationToken = default);
}
