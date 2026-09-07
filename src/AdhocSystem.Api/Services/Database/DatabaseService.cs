using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using AdhocSystem.Api.Models.Common;

namespace AdhocSystem.Api.Services.Database;

public class DatabaseService : IDatabaseService
{
    private readonly DatabaseOptions _options;
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(IOptions<DatabaseOptions> options, ILogger<DatabaseService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<List<Dictionary<string, object?>>> ExecuteReadOnlyQueryAsync(string sqlQuery, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing read-only SQL: {Query}", sqlQuery);
        var connectionString = !string.IsNullOrWhiteSpace(_options.ReadOnlyConnectionString)
            ? _options.ReadOnlyConnectionString
            : _options.ConnectionString;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var cmdDefinition = new CommandDefinition(
            commandText: sqlQuery,
            commandType: CommandType.Text,
            cancellationToken: cancellationToken,
            commandTimeout: 90
        );

        var rows = await connection.QueryAsync(cmdDefinition);
        var results = new List<Dictionary<string, object?>>();

        foreach (var row in rows)
        {
            if (row is IDictionary<string, object> dict)
            {
                var rowDict = new Dictionary<string, object?>();
                foreach (var kv in dict)
                {
                    rowDict[kv.Key] = kv.Value;
                }
                results.Add(rowDict);
            }
            else
            {
                var rowDict = new Dictionary<string, object?>();
                foreach (var prop in ((object)row).GetType().GetProperties())
                {
                    rowDict[prop.Name] = prop.GetValue(row);
                }
                results.Add(rowDict);
            }
        }

        _logger.LogInformation("SQL execution returned {Count} rows", results.Count);
        return results;
    }

    public async Task<bool> CheckConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionString = !string.IsNullOrWhiteSpace(_options.ReadOnlyConnectionString)
                ? _options.ReadOnlyConnectionString
                : _options.ConnectionString;

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            var res = await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT 1", cancellationToken: cancellationToken));
            return res == 1;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Database connection check failed");
            return false;
        }
    }
}
