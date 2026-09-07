using System.Text.RegularExpressions;
using AdhocSystem.Api.Models.Common;
using AdhocSystem.Api.Services.Llm;

namespace AdhocSystem.Api.Services.Routing;

public class IntentRouter : IIntentRouter
{
    private readonly ICircuitBreakerLlm _llm;
    private readonly ILogger<IntentRouter> _logger;

    private static readonly Regex YearRegex = new(@"\b(?:19|20)\d{2}\b", RegexOptions.Compiled);

    private static readonly string[] StrictStructuredMarkers = new[]
    {
        "sales report", "report for", "report of", "sales in", "sales for", "revenue in", "revenue for",
        "total sales", "total revenue", "highest sales", "lowest sales", "orders in", "orders for",
        "top ", "top-", "highest", "lowest", "sum(", "count(", "avg(", "order by", "group by",
        "most expensive", "cheapest", "how many", "how much", "quantity sold", "orders placed",
        "list price", "total spent", "drill down", "drilldown", "drill-down", "drill into",
        "breakdown", "customer", "customers", "vendor", "vendors", "employee", "employees",
        "inventory", "stock level", "salesperson", "sales person", "purchase order", "sales order",
        "list all", "show all", "get all", "find all records", "all products in", "revenue", "sales"
    };

    private static readonly string[] ExplicitSemanticMarkers = new[]
    {
        "compare ", "difference between", " vs ", " vs. ", " versus ",
        "describe features", "features of", "tell me about",
        "recommend ", "good for ", "suitable for ", "specs of ", "specifications of ",
        "looking for items like", "items similar to"
    };

    private const string RouterPromptTemplate = @"Classify the business query into exactly one category:
- 'structured': SQL data retrieval, transactional reports, sales, revenue, orders, inventory, vendors, customers, counts, metrics, ranking.
- 'semantic': conceptual product discovery, item comparisons, recommendations, qualitative feature specs.

Query: ""{query}""
Answer with ONE word: 'structured' or 'semantic'.";

    public IntentRouter(ICircuitBreakerLlm llm, ILogger<IntentRouter> logger)
    {
        _llm = llm;
        _logger = logger;
    }

    public async Task<IntentType> RouteAsync(string query, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Routing intent for query: '{Query}'", query);

        var qLower = query.ToLowerInvariant();

        // 1. Check strict structured markers first (sales report, revenue, dates, aggregations)
        if (StrictStructuredMarkers.Any(marker => qLower.Contains(marker)) ||
            (YearRegex.IsMatch(query) && (qLower.Contains("sales") || qLower.Contains("report") || qLower.Contains("order") || qLower.Contains("revenue") || qLower.Contains("for") || qLower.Contains("in"))))
        {
            _logger.LogInformation("Classified intent via fast rule: STRUCTURED");
            return IntentType.Structured;
        }

        // 2. Check explicit semantic markers (compare X and Y, describe features of X)
        if (ExplicitSemanticMarkers.Any(marker => qLower.Contains(marker)))
        {
            _logger.LogInformation("Classified intent via fast rule: SEMANTIC");
            return IntentType.Semantic;
        }

        // 3. Fast compact LLM router fallback with maxTokens: 10
        var prompt = RouterPromptTemplate.Replace("{query}", query);
        var messages = new[] { ChatMessage.User(prompt) };

        try
        {
            var response = await _llm.CompleteWithFallbackAsync(messages, temperature: 0.0, maxTokens: 100, cancellationToken: cancellationToken);
            var content = response.Trim().ToLowerInvariant();

            if (content.Contains("structured"))
            {
                _logger.LogInformation("LLM classified intent: STRUCTURED");
                return IntentType.Structured;
            }
            if (content.Contains("semantic"))
            {
                _logger.LogInformation("LLM classified intent: SEMANTIC");
                return IntentType.Semantic;
            }

            _logger.LogWarning("Unclear router output '{Output}', using rule-based fallback", content);
            return FallbackRoute(query);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM intent routing unavailable. Using rule-based fallback router.");
            return FallbackRoute(query);
        }
    }

    private IntentType FallbackRoute(string query)
    {
        var q = query.ToLowerInvariant();
        var structuredKeywords = new[]
        {
            "report", "highest", "lowest", "total", "revenue", "sales", "average", "sum",
            "count", "top", "bottom", "max", "min", "region", "category", "order by",
            "quantity", "price", "drill down", "drill-down", "drilldown", "drill into",
            "customer", "customers", "order", "orders", "inventory", "stock", "vendor"
        };

        if (structuredKeywords.Any(kw => q.Contains(kw)) || YearRegex.IsMatch(query))
        {
            _logger.LogInformation("Fallback classified intent: STRUCTURED");
            return IntentType.Structured;
        }

        _logger.LogInformation("Fallback classified intent: SEMANTIC");
        return IntentType.Semantic;
    }
}
