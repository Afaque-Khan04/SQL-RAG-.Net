using AdhocSystem.Api.Services.Semantic;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AdhocSystem.Tests;

public class MockSqliteStore : ISqliteSemanticStore
{
    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<int> SyncFromAdventureWorksAsync(CancellationToken cancellationToken = default) => Task.FromResult(2);
    public Task<SemanticStoreStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new SemanticStoreStatus(true, 2, 2, 384, "data/test.db", 1024, DateTime.UtcNow));

    public Task<List<Dictionary<string, object?>>> HybridSearchAsync(string query, int topK = 5, CancellationToken cancellationToken = default)
    {
        var list = new List<Dictionary<string, object?>>();
        if (query.Contains("frame", StringComparison.OrdinalIgnoreCase) || query.Contains("bike", StringComparison.OrdinalIgnoreCase))
        {
            list.Add(new Dictionary<string, object?>
            {
                ["product_id"] = 1,
                ["product_name"] = "HL Road Frame",
                ["category"] = "Frames",
                ["score"] = 0.95
            });
        }
        if (query.Contains("pedal", StringComparison.OrdinalIgnoreCase) || query.Contains("helmet", StringComparison.OrdinalIgnoreCase))
        {
            list.Add(new Dictionary<string, object?>
            {
                ["product_id"] = 2,
                ["product_name"] = "Sport-100 Helmet",
                ["category"] = "Helmets",
                ["score"] = 0.88
            });
        }
        return Task.FromResult(list);
    }
}

public class SemanticSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_DecomposesAndInterleavesResults()
    {
        var mockStore = new MockSqliteStore();
        var decomposer = new QueryDecomposer();
        var service = new SemanticSearchService(mockStore, decomposer, NullLogger<SemanticSearchService>.Instance);

        var results = await service.SearchAsync("best road frame and helmet", topK: 5);

        Assert.NotEmpty(results);
        Assert.Contains(results, r => Convert.ToInt32(r["product_id"]) == 1);
        Assert.Contains(results, r => Convert.ToInt32(r["product_id"]) == 2);
    }

    [Fact]
    public async Task SearchAsync_CompareQuery_ReturnsExactlyOnePerItem()
    {
        var mockStore = new MockSqliteStore();
        var decomposer = new QueryDecomposer();
        var service = new SemanticSearchService(mockStore, decomposer, NullLogger<SemanticSearchService>.Instance);

        var results = await service.SearchAsync("compare road frame and helmet", topK: 25);

        Assert.Equal(2, results.Count);
        Assert.Equal(1, Convert.ToInt32(results[0]["product_id"]));
        Assert.Equal(2, Convert.ToInt32(results[1]["product_id"]));
    }
}
