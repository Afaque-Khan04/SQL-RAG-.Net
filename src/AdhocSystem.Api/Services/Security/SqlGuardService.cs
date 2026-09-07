using System.Text.RegularExpressions;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace AdhocSystem.Api.Services.Security;

public class SqlGuardService : ISqlGuardService
{
    private readonly ILogger<SqlGuardService> _logger;

    private static readonly HashSet<string> ForbiddenTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "DROP", "DELETE", "INSERT", "UPDATE", "ALTER", "TRUNCATE", "CREATE",
        "GRANT", "REVOKE", "EXEC", "EXECUTE", "MERGE", "XP_CMDSHELL", "SHUTDOWN"
    };

    public SqlGuardService(ILogger<SqlGuardService> logger)
    {
        _logger = logger;
    }

    public string ValidateAndSanitize(string sqlQuery)
    {
        if (string.IsNullOrWhiteSpace(sqlQuery))
        {
            throw new SqlGuardSecurityException("Empty SQL query provided.");
        }

        var cleaned = sqlQuery.Trim();

        // 1. Extract from markdown code fences if present anywhere
        var fenceMatch = Regex.Match(cleaned, @"```(?:sql)?\s*([\s\S]*?)\s*```", RegexOptions.IgnoreCase);
        if (fenceMatch.Success)
        {
            cleaned = fenceMatch.Groups[1].Value.Trim();
        }
        else
        {
            if (cleaned.StartsWith("```sql", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[6..].Trim();
            }
            else if (cleaned.StartsWith("```", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[3..].Trim();
            }

            if (cleaned.EndsWith("```", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[..^3].Trim();
            }
        }

        // 2. Remove comments safely
        cleaned = Regex.Replace(cleaned, @"/\*.*?\*/", "", RegexOptions.Singleline);
        cleaned = Regex.Replace(cleaned, @"--[^\r\n]*", "");

        // 3. Strip conversational preamble before first SELECT or WITH keyword
        var startMatch = Regex.Match(cleaned, @"\b(?:SELECT|WITH)\b", RegexOptions.IgnoreCase);
        if (startMatch.Success && startMatch.Index > 0)
        {
            cleaned = cleaned[startMatch.Index..].Trim();
        }

        // 4. Strip trailing conversational notes / questions that models occasionally append
        cleaned = Regex.Replace(cleaned, @"(?im)^\s*(?:note|explanation|please note|warning|tips?|disclaimer|query|would you)\b.*$", "");
        cleaned = cleaned.Trim().TrimEnd(';');

        // 5. Microsoft ScriptDom AST Validation
        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        using var reader = new StringReader(cleaned);
        var fragment = parser.Parse(reader, out var errors);

        if (errors != null && errors.Count > 0)
        {
            var firstErr = errors[0];
            throw new SqlGuardSecurityException($"SQL syntax parsing error at line {firstErr.Line}, col {firstErr.Column}: {firstErr.Message}");
        }

        if (fragment is not TSqlScript script)
        {
            throw new SqlGuardSecurityException("Query safety violation: Invalid TSql script fragment.");
        }

        if (script.Batches.Count != 1)
        {
            throw new SqlGuardSecurityException("Query safety violation: Exactly one SQL batch is permitted.");
        }

        var batch = script.Batches[0];
        if (batch.Statements.Count != 1)
        {
            throw new SqlGuardSecurityException("Query safety violation: Exactly one SQL statement is permitted per query.");
        }

        var statement = batch.Statements[0];
        if (statement is not SelectStatement selectStatement)
        {
            throw new SqlGuardSecurityException($"Query safety violation: Statement type '{statement.GetType().Name}' is forbidden. Only SELECT statements are permitted.");
        }

        // Ensure INTO clause is not used to create tables (SelectStatement.Into)
        if (selectStatement.Into != null)
        {
            throw new SqlGuardSecurityException("Query safety violation: SELECT INTO is forbidden.");
        }

        // 4. Token-level visitor to block any execution or mutation tokens
        var visitor = new AstSafetyVisitor();
        selectStatement.Accept(visitor);

        if (visitor.Violations.Count > 0)
        {
            throw new SqlGuardSecurityException($"Query safety violation: Forbidden operations detected: {string.Join(", ", visitor.Violations)}");
        }

        _logger.LogInformation("SQL Guard ScriptDom AST validation passed successfully.");
        return cleaned;
    }

    private class AstSafetyVisitor : TSqlFragmentVisitor
    {
        public List<string> Violations { get; } = new();

        public override void ExplicitVisit(ExecuteStatement node)
        {
            Violations.Add("EXECUTE statement");
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(DataModificationStatement node)
        {
            Violations.Add($"Data modification ({node.GetType().Name})");
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(AlterTableStatement node)
        {
            Violations.Add("ALTER TABLE statement");
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(DropTableStatement node)
        {
            Violations.Add("DROP TABLE statement");
            base.ExplicitVisit(node);
        }
    }
}
