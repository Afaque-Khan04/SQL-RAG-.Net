using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics.Tensors;
using System.Text.RegularExpressions;
using AdhocSystem.Api.Models.Common;
using AdhocSystem.Api.Services.Database;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace AdhocSystem.Api.Services.Semantic;

public record ProductRecord(
    int ProductId,
    string ProductName,
    string ProductNumber,
    string Color,
    double ListPrice,
    string CategoryName,
    string SubcategoryName,
    string ModelName,
    string Description,
    string DocumentText
);

public class SqliteSemanticStore : ISqliteSemanticStore
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILocalEmbeddingGenerator _embeddingGenerator;
    private readonly VectorStoreOptions _options;
    private readonly ILogger<SqliteSemanticStore> _logger;
    private readonly string _dbPath;
    private readonly string _connectionString;

    private readonly ConcurrentDictionary<int, ProductRecord> _productCache = new();
    private readonly ConcurrentDictionary<int, float[]> _vectorCache = new();
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private bool _isInitialized;
    private DateTime? _lastSyncedAt;

    public SqliteSemanticStore(
        IServiceScopeFactory scopeFactory,
        ILocalEmbeddingGenerator embeddingGenerator,
        IOptions<VectorStoreOptions> options,
        ILogger<SqliteSemanticStore> logger)
    {
        _scopeFactory = scopeFactory;
        _embeddingGenerator = embeddingGenerator;
        _options = options.Value;
        _logger = logger;

        var relativeOrAbsPath = string.IsNullOrWhiteSpace(_options.SqliteDbPath)
            ? "data/semantic_store.db"
            : _options.SqliteDbPath;

        _dbPath = Path.IsPathRooted(relativeOrAbsPath)
            ? relativeOrAbsPath
            : Path.Combine(AppContext.BaseDirectory, relativeOrAbsPath);

        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _connectionString = $"Data Source={_dbPath}";
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized) return;

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized) return;

            _logger.LogInformation("Initializing SQLite Semantic Store at: {DbPath}", _dbPath);

            using (var conn = new SqliteConnection(_connectionString))
            {
                await conn.OpenAsync(cancellationToken);

                // Create main Products table
                var createProductsSql = @"
                    CREATE TABLE IF NOT EXISTS Products (
                        ProductID INTEGER PRIMARY KEY,
                        ProductName TEXT NOT NULL,
                        ProductNumber TEXT,
                        Color TEXT,
                        ListPrice REAL,
                        CategoryName TEXT,
                        SubcategoryName TEXT,
                        ModelName TEXT,
                        Description TEXT NOT NULL,
                        DocumentText TEXT NOT NULL
                    );";

                using (var cmd = new SqliteCommand(createProductsSql, conn))
                {
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                // Create FTS5 Virtual Table for BM25 text search
                var createFtsSql = @"
                    CREATE VIRTUAL TABLE IF NOT EXISTS ProductsFts USING fts5(
                        ProductID UNINDEXED,
                        ProductName,
                        CategoryName,
                        SubcategoryName,
                        ModelName,
                        Description,
                        tokenize='porter unicode61'
                    );";

                using (var cmd = new SqliteCommand(createFtsSql, conn))
                {
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                // Create ProductVectors Table
                var createVectorsSql = @"
                    CREATE TABLE IF NOT EXISTS ProductVectors (
                        ProductID INTEGER PRIMARY KEY,
                        Embedding BLOB NOT NULL,
                        VectorDimension INTEGER NOT NULL
                    );";

                using (var cmd = new SqliteCommand(createVectorsSql, conn))
                {
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                // Create StoreMetadata Table
                var createMetaSql = @"
                    CREATE TABLE IF NOT EXISTS StoreMetadata (
                        Key TEXT PRIMARY KEY,
                        Value TEXT NOT NULL
                    );";

                using (var cmd = new SqliteCommand(createMetaSql, conn))
                {
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            // Load into in-memory caches
            await LoadCachesFromSqliteAsync(cancellationToken);

            // Check if catalog needs initial sync
            if (_productCache.IsEmpty && _options.AutoSyncOnStartup)
            {
                _logger.LogInformation("SQLite Semantic Store is empty. Triggering initial sync from AdventureWorks SQL Server...");
                await SyncInternalAsync(cancellationToken);
            }

            _isInitialized = true;
            _logger.LogInformation("SQLite Semantic Store ready with {ProductCount} products and {VectorCount} vectors.",
                _productCache.Count, _vectorCache.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SQLite Semantic Store at {DbPath}", _dbPath);
            throw;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<int> SyncFromAdventureWorksAsync(CancellationToken cancellationToken = default)
    {
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            return await SyncInternalAsync(cancellationToken);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task<int> SyncInternalAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Beginning catalog synchronization from AdventureWorks2022 to SQLite...");
        var sw = Stopwatch.StartNew();

        const string sql = @"
            SELECT DISTINCT
                p.ProductID,
                p.Name AS ProductName,
                p.ProductNumber,
                ISNULL(p.Color, 'N/A') AS Color,
                CAST(p.ListPrice AS FLOAT) AS ListPrice,
                ISNULL(pc.Name, 'General') AS CategoryName,
                ISNULL(sc.Name, 'General') AS SubcategoryName,
                ISNULL(pm.Name, p.Name) AS ModelName,
                pd.Description
            FROM Production.Product AS p
            JOIN Production.ProductModel AS pm 
                ON p.ProductModelID = pm.ProductModelID
            JOIN Production.ProductModelProductDescriptionCulture AS pmpdc 
                ON pm.ProductModelID = pmpdc.ProductModelID AND pmpdc.CultureID = 'en'
            JOIN Production.ProductDescription AS pd 
                ON pmpdc.ProductDescriptionID = pd.ProductDescriptionID
            LEFT JOIN Production.ProductSubcategory AS sc 
                ON p.ProductSubcategoryID = sc.ProductSubcategoryID
            LEFT JOIN Production.ProductCategory AS pc 
                ON sc.ProductCategoryID = pc.ProductCategoryID
            WHERE pd.Description IS NOT NULL AND LEN(pd.Description) > 5;";

        List<Dictionary<string, object?>> rows;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbService = scope.ServiceProvider.GetRequiredService<IDatabaseService>();
            rows = await dbService.ExecuteReadOnlyQueryAsync(sql, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch catalog from AdventureWorks SQL Server for semantic sync. Will rely on existing SQLite records.");
            return _productCache.Count;
        }

        if (rows.Count == 0)
        {
            _logger.LogWarning("No product records returned from AdventureWorks for semantic sync.");
            return 0;
        }

        var dim = _options.VectorDimension > 0 ? _options.VectorDimension : 384;
        var products = new List<ProductRecord>();
        var vectors = new List<(int ProductId, byte[] Blob, float[] RawVector)>();

        foreach (var r in rows)
        {
            var pid = Convert.ToInt32(r["ProductID"]);
            var name = r["ProductName"]?.ToString() ?? "Product";
            var pnum = r["ProductNumber"]?.ToString() ?? "";
            var color = r["Color"]?.ToString() ?? "N/A";
            var price = r["ListPrice"] != null ? Convert.ToDouble(r["ListPrice"]) : 0.0;
            var cat = r["CategoryName"]?.ToString() ?? "General";
            var subcat = r["SubcategoryName"]?.ToString() ?? "General";
            var model = r["ModelName"]?.ToString() ?? name;
            var desc = r["Description"]?.ToString() ?? "";

            var docText = $"Product: {name} | Model: {model} | Category: {cat} > {subcat} | Color: {color} | Price: ${price:F2} | Description: {desc}";

            var record = new ProductRecord(pid, name, pnum, color, price, cat, subcat, model, desc, docText);
            products.Add(record);

            var vector = _embeddingGenerator.GenerateEmbedding(docText, dim);
            var blob = _embeddingGenerator.SerializeVector(vector);
            vectors.Add((pid, blob, vector));
        }

        // Write batch into SQLite transaction
        using (var conn = new SqliteConnection(_connectionString))
        {
            await conn.OpenAsync(cancellationToken);
            using var tx = conn.BeginTransaction();

            using (var clearCmd = new SqliteCommand("DELETE FROM Products; DELETE FROM ProductsFts; DELETE FROM ProductVectors;", conn, tx))
            {
                await clearCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Insert Products & FTS
            var insertProductSql = @"
                INSERT INTO Products (ProductID, ProductName, ProductNumber, Color, ListPrice, CategoryName, SubcategoryName, ModelName, Description, DocumentText)
                VALUES (@pid, @name, @pnum, @color, @price, @cat, @subcat, @model, @desc, @docText);
                INSERT INTO ProductsFts (ProductID, ProductName, CategoryName, SubcategoryName, ModelName, Description)
                VALUES (@pid, @name, @cat, @subcat, @model, @desc);";

            foreach (var p in products)
            {
                using var cmd = new SqliteCommand(insertProductSql, conn, tx);
                cmd.Parameters.AddWithValue("@pid", p.ProductId);
                cmd.Parameters.AddWithValue("@name", p.ProductName);
                cmd.Parameters.AddWithValue("@pnum", p.ProductNumber);
                cmd.Parameters.AddWithValue("@color", p.Color);
                cmd.Parameters.AddWithValue("@price", p.ListPrice);
                cmd.Parameters.AddWithValue("@cat", p.CategoryName);
                cmd.Parameters.AddWithValue("@subcat", p.SubcategoryName);
                cmd.Parameters.AddWithValue("@model", p.ModelName);
                cmd.Parameters.AddWithValue("@desc", p.Description);
                cmd.Parameters.AddWithValue("@docText", p.DocumentText);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Insert Vectors
            var insertVectorSql = "INSERT INTO ProductVectors (ProductID, Embedding, VectorDimension) VALUES (@pid, @emb, @dim);";
            foreach (var v in vectors)
            {
                using var cmd = new SqliteCommand(insertVectorSql, conn, tx);
                cmd.Parameters.AddWithValue("@pid", v.ProductId);
                cmd.Parameters.AddWithValue("@emb", v.Blob);
                cmd.Parameters.AddWithValue("@dim", dim);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Update Metadata
            var updateMetaSql = @"
                INSERT OR REPLACE INTO StoreMetadata (Key, Value) VALUES ('LastSyncedAt', @syncTime);
                INSERT OR REPLACE INTO StoreMetadata (Key, Value) VALUES ('TotalCount', @count);";
            using (var metaCmd = new SqliteCommand(updateMetaSql, conn, tx))
            {
                metaCmd.Parameters.AddWithValue("@syncTime", DateTime.UtcNow.ToString("O"));
                metaCmd.Parameters.AddWithValue("@count", products.Count.ToString());
                await metaCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
        }

        // Update in-memory caches
        _productCache.Clear();
        foreach (var p in products)
        {
            _productCache[p.ProductId] = p;
        }

        _vectorCache.Clear();
        foreach (var v in vectors)
        {
            _vectorCache[v.ProductId] = v.RawVector;
        }

        _lastSyncedAt = DateTime.UtcNow;
        sw.Stop();
        _logger.LogInformation("Successfully indexed {Count} product records into SQLite in {ElapsedMs}ms.", products.Count, sw.ElapsedMilliseconds);

        return products.Count;
    }

    private async Task LoadCachesFromSqliteAsync(CancellationToken cancellationToken)
    {
        _productCache.Clear();
        _vectorCache.Clear();

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        // Load Products
        using (var cmd = new SqliteCommand("SELECT ProductID, ProductName, ProductNumber, Color, ListPrice, CategoryName, SubcategoryName, ModelName, Description, DocumentText FROM Products;", conn))
        using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var p = new ProductRecord(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? "" : reader.GetString(2),
                    reader.IsDBNull(3) ? "N/A" : reader.GetString(3),
                    reader.IsDBNull(4) ? 0.0 : reader.GetDouble(4),
                    reader.IsDBNull(5) ? "General" : reader.GetString(5),
                    reader.IsDBNull(6) ? "General" : reader.GetString(6),
                    reader.IsDBNull(7) ? "" : reader.GetString(7),
                    reader.GetString(8),
                    reader.GetString(9)
                );
                _productCache[p.ProductId] = p;
            }
        }

        // Load Vectors
        using (var cmd = new SqliteCommand("SELECT ProductID, Embedding FROM ProductVectors;", conn))
        using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var pid = reader.GetInt32(0);
                var blob = (byte[])reader[1];
                var vec = _embeddingGenerator.DeserializeVector(blob);
                _vectorCache[pid] = vec;
            }
        }

        // Load Metadata
        using (var cmd = new SqliteCommand("SELECT Key, Value FROM StoreMetadata WHERE Key = 'LastSyncedAt';", conn))
        using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken) && DateTime.TryParse(reader.GetString(1), out var dt))
            {
                _lastSyncedAt = dt;
            }
        }
    }

    public async Task<List<Dictionary<string, object?>>> HybridSearchAsync(string query, int topK = 5, CancellationToken cancellationToken = default)
    {
        if (!_isInitialized)
        {
            await InitializeAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(query) || _productCache.IsEmpty)
        {
            return new List<Dictionary<string, object?>>();
        }

        var sw = Stopwatch.StartNew();
        var dim = _options.VectorDimension > 0 ? _options.VectorDimension : 384;
        var queryVector = _embeddingGenerator.GenerateEmbedding(query, dim);

        // 1. Compute Vector Cosine Similarity scores
        var vectorScores = new Dictionary<int, float>();
        foreach (var (pid, docVec) in _vectorCache)
        {
            var sim = TensorPrimitives.CosineSimilarity(queryVector, docVec);
            vectorScores[pid] = Math.Max(0.0f, sim);
        }

        // 2. Perform SQLite FTS5 Full-Text Match
        var ftsScores = new Dictionary<int, float>();
        var sanitizedFts = SanitizeFtsQuery(query);

        if (!string.IsNullOrWhiteSpace(sanitizedFts))
        {
            try
            {
                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                // FTS5 rank is a negative number where lower (more negative) is better BM25 match
                var ftsSql = @"
                    SELECT ProductID, rank
                    FROM ProductsFts
                    WHERE ProductsFts MATCH @query
                    ORDER BY rank
                    LIMIT @limit;";

                using var cmd = new SqliteCommand(ftsSql, conn);
                cmd.Parameters.AddWithValue("@query", sanitizedFts);
                cmd.Parameters.AddWithValue("@limit", Math.Max(topK * 3, 200));

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                var ftsRanks = new List<(int ProductId, double Rank)>();
                while (await reader.ReadAsync(cancellationToken))
                {
                    ftsRanks.Add((reader.GetInt32(0), reader.GetDouble(1)));
                }

                if (ftsRanks.Count > 0)
                {
                    // Normalize FTS ranks to [0..1] range
                    var minRank = ftsRanks.Min(r => r.Rank);
                    var maxRank = ftsRanks.Max(r => r.Rank);
                    var rankRange = Math.Max(0.0001, maxRank - minRank);

                    foreach (var (pid, rank) in ftsRanks)
                    {
                        // Inverted normalized rank so best match gets ~1.0
                        var normScore = (float)(1.0 - ((rank - minRank) / rankRange));
                        ftsScores[pid] = normScore;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "FTS5 match query produced error for input '{Query}'. Falling back to vector search only.", sanitizedFts);
            }
        }

        // 3. Score Fusion: Hybrid weighted combination
        var ftsWeight = _options.FtsWeight;
        var vecWeight = _options.VectorWeight;
        var totalWeight = ftsWeight + vecWeight;
        if (totalWeight <= 0) totalWeight = 1.0f;

        var scoredItems = new List<(int ProductId, float CombinedScore, float CosineSim, float FtsScore)>();

        foreach (var (pid, record) in _productCache)
        {
            vectorScores.TryGetValue(pid, out var cosine);
            ftsScores.TryGetValue(pid, out var fts);

            var hybridScore = ((cosine * vecWeight) + (fts * ftsWeight)) / totalWeight;

            // Give a boost if direct exact substring matches occur in product name
            if (record.ProductName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                record.ModelName.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                hybridScore = Math.Min(1.0f, hybridScore + 0.3f);
            }

            if (hybridScore > 0.01f)
            {
                scoredItems.Add((pid, hybridScore, cosine, fts));
            }
        }

        // Dynamic adaptive cutoff: prevent unrelated noisy items from padding the result list
        if (scoredItems.Count > 0)
        {
            var maxScore = scoredItems.Max(x => x.CombinedScore);
            var relativeCutoff = maxScore >= 0.30f ? Math.Max(0.18f, maxScore * 0.45f) : 0.05f;
            scoredItems = scoredItems.Where(x => x.CombinedScore >= relativeCutoff).ToList();
        }

        // Sort descending by combined score
        var topResults = scoredItems
            .OrderByDescending(x => x.CombinedScore)
            .Take(topK)
            .ToList();

        // 4. Format into response dictionaries
        var results = new List<Dictionary<string, object?>>();
        foreach (var item in topResults)
        {
            if (!_productCache.TryGetValue(item.ProductId, out var p)) continue;

            var metadata = new Dictionary<string, object?>
            {
                ["product_id"] = p.ProductId,
                ["product_name"] = p.ProductName,
                ["product_number"] = p.ProductNumber,
                ["color"] = p.Color,
                ["category"] = p.CategoryName,
                ["subcategory"] = p.SubcategoryName,
                ["model_name"] = p.ModelName,
                ["price"] = p.ListPrice,
                ["description"] = p.Description
            };

            var dict = new Dictionary<string, object?>
            {
                ["document"] = p.DocumentText,
                ["metadata"] = metadata,
                ["distance"] = Math.Round(1.0 - item.CombinedScore, 4),
                ["score"] = Math.Round(item.CombinedScore, 4),
                ["product_id"] = p.ProductId,
                ["product_name"] = p.ProductName,
                ["category"] = p.CategoryName,
                ["subcategory"] = p.SubcategoryName,
                ["price"] = p.ListPrice,
                ["description"] = p.Description
            };

            results.Add(dict);
        }

        sw.Stop();
        _logger.LogInformation("Hybrid search for '{Query}' returned {Count} results in {ElapsedMs}ms.",
            query, results.Count, sw.ElapsedMilliseconds);

        return results;
    }

    public async Task<SemanticStoreStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!_isInitialized)
        {
            await InitializeAsync(cancellationToken);
        }

        long dbSize = 0;
        try
        {
            if (File.Exists(_dbPath))
            {
                dbSize = new FileInfo(_dbPath).Length;
            }
        }
        catch { }

        return new SemanticStoreStatus(
            IsInitialized: _isInitialized,
            TotalProducts: _productCache.Count,
            TotalVectors: _vectorCache.Count,
            VectorDimension: _options.VectorDimension,
            DatabasePath: _dbPath,
            DatabaseSizeBytes: dbSize,
            LastSyncedAt: _lastSyncedAt
        );
    }

    private static string SanitizeFtsQuery(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // Extract alphanumeric tokens
        var tokens = Regex.Matches(input, @"[\w\d]+", RegexOptions.Compiled)
            .Select(m => m.Value.Trim())
            .Where(w => w.Length >= 2)
            .ToList();

        if (tokens.Count == 0) return string.Empty;

        // Build prefix match query: (token1* OR token2* ...)
        var parts = tokens.Select(t => $"\"{t}\"*");
        return string.Join(" OR ", parts);
    }
}
