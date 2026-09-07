using AdhocSystem.Api.Models.Common;
using AdhocSystem.Api.Services.Database;
using AdhocSystem.Api.Services.Semantic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AdhocSystem.Tests;

public class MockDatabaseService : IDatabaseService
{
    private readonly List<Dictionary<string, object?>> _mockData;

    public MockDatabaseService(List<Dictionary<string, object?>> mockData)
    {
        _mockData = mockData;
    }

    public Task<List<Dictionary<string, object?>>> ExecuteReadOnlyQueryAsync(string sqlQuery, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_mockData);
    }

    public Task<bool> CheckConnectionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}

public class SqliteSemanticStoreTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly LocalEmbeddingGenerator _embeddingGenerator;
    private readonly NullLogger<SqliteSemanticStore> _logger;

    public SqliteSemanticStoreTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_semantic_{Guid.NewGuid():N}.db");
        _embeddingGenerator = new LocalEmbeddingGenerator();
        _logger = NullLogger<SqliteSemanticStore>.Instance;
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempDbPath))
            {
                File.Delete(_tempDbPath);
            }
        }
        catch { }
    }

    [Fact]
    public async Task HybridSearch_ReturnsRelevantMatches()
    {
        var mockCatalog = new List<Dictionary<string, object?>>
        {
            new()
            {
                ["ProductID"] = 101,
                ["ProductName"] = "Mountain-100 Silver, 38",
                ["ProductNumber"] = "BK-M18S-38",
                ["Color"] = "Silver",
                ["ListPrice"] = 3399.99,
                ["CategoryName"] = "Bikes",
                ["SubcategoryName"] = "Mountain Bikes",
                ["ModelName"] = "Mountain-100",
                ["Description"] = "Top-of-the-line competition mountain bike. High-grade alloy frame with responsive hydraulic disc brakes."
            },
            new()
            {
                ["ProductID"] = 102,
                ["ProductName"] = "Water Bottle - 30 oz.",
                ["ProductNumber"] = "WB-H898",
                ["Color"] = "Blue",
                ["ListPrice"] = 4.99,
                ["CategoryName"] = "Accessories",
                ["SubcategoryName"] = "Bottles and Cages",
                ["ModelName"] = "Water Bottle",
                ["Description"] = "Lightweight high-density polyethylene water bottle with easy-pour nozzle."
            },
            new()
            {
                ["ProductID"] = 103,
                ["ProductName"] = "All-Weather Rain Jacket",
                ["ProductNumber"] = "JC-9032",
                ["Color"] = "Yellow",
                ["ListPrice"] = 119.99,
                ["CategoryName"] = "Clothing",
                ["SubcategoryName"] = "Jackets",
                ["ModelName"] = "All-Weather Jacket",
                ["Description"] = "Water-resistant, breathable cycling jacket for rainy, wet and cold conditions."
            }
        };

        var options = Options.Create(new VectorStoreOptions
        {
            SqliteDbPath = _tempDbPath,
            AutoSyncOnStartup = true,
            FtsWeight = 0.5f,
            VectorWeight = 0.5f,
            VectorDimension = 384
        });

        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddScoped<IDatabaseService>(_ => new MockDatabaseService(mockCatalog));
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();

        var store = new SqliteSemanticStore(scopeFactory, _embeddingGenerator, options, _logger);

        await store.InitializeAsync();

        var status = await store.GetStatusAsync();
        Assert.True(status.IsInitialized);
        Assert.Equal(3, status.TotalProducts);
        Assert.Equal(3, status.TotalVectors);

        // Search for rain jacket
        var results = await store.HybridSearchAsync("waterproof rain jacket for wet weather", topK: 2);
        Assert.NotEmpty(results);
        Assert.Equal(103, Convert.ToInt32(results[0]["product_id"]));
        Assert.Contains("Rain Jacket", results[0]["product_name"]?.ToString());

        // Search for mountain bike
        var bikeResults = await store.HybridSearchAsync("competition alloy mountain bike", topK: 1);
        Assert.NotEmpty(bikeResults);
        Assert.Equal(101, Convert.ToInt32(bikeResults[0]["product_id"]));
    }
}
