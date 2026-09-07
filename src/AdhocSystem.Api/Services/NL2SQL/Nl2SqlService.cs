using System.Text.Json;
using AdhocSystem.Api.Services.Database;
using AdhocSystem.Api.Services.Llm;
using AdhocSystem.Api.Services.Security;
using AdhocSystem.Api.Services.Session;

namespace AdhocSystem.Api.Services.NL2SQL;

public class Nl2SqlService : INl2SqlService
{
    private readonly ISchemaIntrospector _schemaIntrospector;
    private readonly IDatabaseService _databaseService;
    private readonly ISqlGuardService _sqlGuardService;
    private readonly ICircuitBreakerLlm _llm;
    private readonly ILogger<Nl2SqlService> _logger;

    public Nl2SqlService(
        ISchemaIntrospector schemaIntrospector,
        IDatabaseService databaseService,
        ISqlGuardService sqlGuardService,
        ICircuitBreakerLlm llm,
        ILogger<Nl2SqlService> logger)
    {
        _schemaIntrospector = schemaIntrospector;
        _databaseService = databaseService;
        _sqlGuardService = sqlGuardService;
        _llm = llm;
        _logger = logger;
    }

    public async Task<(string generatedSql, List<Dictionary<string, object?>> results)> GenerateAndExecuteAsync(
        string query,
        TurnContext? previousTurn = null,
        Dictionary<string, object?>? selectedRowContext = null,
        bool isFollowUp = false,
        CancellationToken cancellationToken = default)
    {
        var schemaContext = await _schemaIntrospector.GetDynamicSchemaContextAsync(cancellationToken);
        string prompt;

        if ((isFollowUp && previousTurn != null && !string.IsNullOrWhiteSpace(previousTurn.GeneratedSql)) || selectedRowContext != null)
        {
            var rowContextSection = "";
            var compactOptions = new JsonSerializerOptions { WriteIndented = false };

            if (selectedRowContext != null)
            {
                var pruned = PruneRowForContext(selectedRowContext);
                rowContextSection = $"Target Row Context: {JsonSerializer.Serialize(pruned, compactOptions)}";
            }
            else if (previousTurn?.SampleRows != null && previousTurn.SampleRows.Count > 0)
            {
                var prunedSamples = previousTurn.SampleRows.Take(2).Select(PruneRowForContext).ToList();
                rowContextSection = $"Sample Previous Rows: {JsonSerializer.Serialize(prunedSamples, compactOptions)}";
            }

            prompt = Prompts.ContextualPrompt
                .Replace("{schema_context}", schemaContext)
                .Replace("{previous_query}", previousTurn?.Query ?? "Targeted database entity row selection")
                .Replace("{previous_sql}", previousTurn?.GeneratedSql ?? "SELECT * FROM previous_table")
                .Replace("{row_context_section}", rowContextSection)
                .Replace("{query}", query);

            _logger.LogInformation("Generating contextual follow-up T-SQL for query: '{Query}'", query);
        }
        else
        {
            prompt = Prompts.UniversalPrompt
                .Replace("{schema_context}", schemaContext)
                .Replace("{query}", query);

            _logger.LogInformation("Generating independent universal T-SQL for query: '{Query}'", query);
        }

        var messages = new[] { ChatMessage.User(prompt) };
        var rawSql = await _llm.CompleteWithFallbackAsync(messages, temperature: 0.0, maxTokens: 3000, cancellationToken: cancellationToken);

        // Layer 1 Safety Validation via ScriptDom AST Guard
        var validatedSql = _sqlGuardService.ValidateAndSanitize(rawSql);

        // Layer 2 Read-Only Database Execution
        var results = await _databaseService.ExecuteReadOnlyQueryAsync(validatedSql, cancellationToken);

        return (validatedSql, results);
    }

    private static Dictionary<string, object?> PruneRowForContext(Dictionary<string, object?> row)
    {
        var pruned = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in row)
        {
            if (kv.Value == null) continue;
            var key = kv.Key;
            // Retain primary keys, identifiers, names, numbers, codes, and statuses
            if (key.EndsWith("ID", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("Name", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("Number", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("Code", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("Date", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("Total", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("Status", StringComparison.OrdinalIgnoreCase))
            {
                pruned[key] = kv.Value;
            }
        }
        return pruned.Count > 0 ? pruned : row;
    }
}
