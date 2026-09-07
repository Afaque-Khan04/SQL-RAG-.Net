namespace AdhocSystem.Api.Services.Database;

public interface IDatabaseService
{
    Task<List<Dictionary<string, object?>>> ExecuteReadOnlyQueryAsync(string sqlQuery, CancellationToken cancellationToken = default);
    Task<bool> CheckConnectionAsync(CancellationToken cancellationToken = default);
}
