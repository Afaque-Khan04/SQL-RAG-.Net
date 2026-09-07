namespace AdhocSystem.Api.Services.Semantic;

public record SemanticStoreStatus(
    bool IsInitialized,
    int TotalProducts,
    int TotalVectors,
    int VectorDimension,
    string DatabasePath,
    long DatabaseSizeBytes,
    DateTime? LastSyncedAt
);

public interface ISqliteSemanticStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<int> SyncFromAdventureWorksAsync(CancellationToken cancellationToken = default);
    Task<List<Dictionary<string, object?>>> HybridSearchAsync(string query, int topK = 5, CancellationToken cancellationToken = default);
    Task<SemanticStoreStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}
