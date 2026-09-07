using System.Text.RegularExpressions;

namespace AdhocSystem.Api.Services.Semantic;

public record QueryDecompositionResult(
    List<string> SubQueries,
    bool IsComparison
);

public class QueryDecomposer
{
    private static readonly Regex ConjunctionRegex = new(
        @"\s+(?:and also|as well as|along with|and\s+the|and\s+a|and\s+an|and|&|plus)\s+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AdjectiveRegex = new(
        @"\b(best|top|cheapest|most expensive|durable|lightweight|popular|ergonomic|affordable)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CompareRegex = new(
        @"^(?:compare|difference between)\s+(.+?)\s+(?:and|with|to|vs\.?|versus)\s+(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LeadingArticles = new(
        @"^(the|a|an)\s+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public QueryDecompositionResult DecomposeDetailed(string query)
    {
        var cleanQ = query.Trim();
        cleanQ = Regex.Replace(cleanQ, @"[?.!]+$", "").Trim();

        // 1. Compare / versus pattern (targeted comparison)
        var compareMatch = CompareRegex.Match(cleanQ);
        if (compareMatch.Success)
        {
            var s1 = LeadingArticles.Replace(compareMatch.Groups[1].Value.Trim(), "").Trim();
            var s2 = LeadingArticles.Replace(compareMatch.Groups[2].Value.Trim(), "").Trim();
            if (!string.IsNullOrEmpty(s1) && !string.IsNullOrEmpty(s2))
            {
                return new QueryDecompositionResult(new List<string> { s1, s2 }, IsComparison: true);
            }
        }

        // 2. Fast rule-based decomposition on explicit conjunctions
        var parts = ConjunctionRegex.Split(cleanQ);
        if (parts.Length > 1)
        {
            var adjMatch = AdjectiveRegex.Match(parts[0]);
            var adj = adjMatch.Success ? adjMatch.Value.Trim() : "";

            var subQueries = new List<string>();
            for (var i = 0; i < parts.Length; i++)
            {
                var cleaned = LeadingArticles.Replace(parts[i].Trim(), "").Trim();
                if (string.IsNullOrWhiteSpace(cleaned))
                {
                    continue;
                }

                if (i > 0 && !string.IsNullOrEmpty(adj) && !Regex.IsMatch(cleaned, @"\b" + Regex.Escape(adj) + @"\b", RegexOptions.IgnoreCase))
                {
                    subQueries.Add($"{adj} {cleaned}");
                }
                else
                {
                    subQueries.Add(cleaned);
                }
            }

            var filtered = subQueries.Where(sq => sq.Length >= 2).ToList();
            if (filtered.Count > 1)
            {
                return new QueryDecompositionResult(filtered, IsComparison: false);
            }
        }

        return new QueryDecompositionResult(new List<string> { cleanQ }, IsComparison: false);
    }

    public List<string> Decompose(string query) => DecomposeDetailed(query).SubQueries;
}
