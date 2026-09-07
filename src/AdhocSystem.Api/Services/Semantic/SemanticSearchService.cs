using AdhocSystem.Api.Models.Common;
using Microsoft.Extensions.Options;

namespace AdhocSystem.Api.Services.Semantic;

public class SemanticSearchService : ISemanticSearchService
{
    private readonly ISqliteSemanticStore _sqliteStore;
    private readonly QueryDecomposer _decomposer;
    private readonly ILogger<SemanticSearchService> _logger;

    public SemanticSearchService(
        ISqliteSemanticStore sqliteStore,
        QueryDecomposer decomposer,
        ILogger<SemanticSearchService> logger)
    {
        _sqliteStore = sqliteStore;
        _decomposer = decomposer;
        _logger = logger;
    }

    public async Task<List<Dictionary<string, object?>>> SearchAsync(string query, int topK = 5, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Performing hybrid semantic search for: '{Query}' (requested top_k={TopK})", query, topK);

        var decomp = _decomposer.DecomposeDetailed(query);
        var subQueries = decomp.SubQueries;

        if (subQueries.Count <= 1)
        {
            return await _sqliteStore.HybridSearchAsync(subQueries.Count > 0 ? subQueries[0] : query, topK, cancellationToken);
        }

        _logger.LogInformation("Executing composite multi-query retrieval across {Count} sub-queries (IsComparison: {IsComparison}): {Queries}",
            subQueries.Count, decomp.IsComparison, string.Join(" | ", subQueries));

        // For targeted comparison queries (e.g. "compare A and B"), default to 1 item per entity unless user explicitly specified a count
        int kPerSubquery;
        if (decomp.IsComparison && topK <= 25)
        {
            kPerSubquery = 1; // Exactly 1 item per compared subject
        }
        else
        {
            kPerSubquery = Math.Max(1, (topK + subQueries.Count - 1) / subQueries.Count);
        }

        var subResults = new List<List<Dictionary<string, object?>>>();

        foreach (var sq in subQueries)
        {
            var res = await _sqliteStore.HybridSearchAsync(sq, kPerSubquery, cancellationToken);
            subResults.Add(res);
        }

        // Balanced round-robin interleaving with deduplication
        var interleaved = new List<Dictionary<string, object?>>();
        var seenKeys = new HashSet<string>();
        var maxLen = subResults.Any() ? subResults.Max(r => r.Count) : 0;

        for (var i = 0; i < maxLen; i++)
        {
            foreach (var list in subResults)
            {
                if (i < list.Count)
                {
                    var item = list[i];
                    string uniqueKey = "";
                    if (item.TryGetValue("product_id", out var pid) && pid != null)
                    {
                        uniqueKey = pid.ToString()!;
                    }
                    else if (item.TryGetValue("document", out var doc) && doc != null)
                    {
                        uniqueKey = doc.ToString()!;
                    }
                    else
                    {
                        uniqueKey = Guid.NewGuid().ToString();
                    }

                    if (!string.IsNullOrEmpty(uniqueKey) && seenKeys.Add(uniqueKey))
                    {
                        interleaved.Add(item);
                    }
                }
            }
        }

        var totalLimit = decomp.IsComparison && topK <= 25 
            ? subQueries.Count 
            : Math.Max(topK, subQueries.Count * 2);

        var finalResults = interleaved.Take(totalLimit).ToList();

        _logger.LogInformation("Sub-query decomposition merged {Count} balanced records across {NumSub} categories.",
            finalResults.Count, subQueries.Count);

        return finalResults;
    }
}
