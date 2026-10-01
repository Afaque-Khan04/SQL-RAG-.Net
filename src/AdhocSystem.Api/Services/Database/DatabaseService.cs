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

        await using var command = new SqlCommand(sqlQuery, connection)
        {
            CommandType = CommandType.Text,
            CommandTimeout = 90
        };

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleResult, cancellationToken);
        var colCount = reader.FieldCount;
        var colNames = new string[colCount];
        for (int i = 0; i < colCount; i++)
        {
            colNames[i] = reader.GetName(i);
        }

        var results = new List<Dictionary<string, object?>>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var rowDict = new Dictionary<string, object?>(colCount, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < colCount; i++)
            {
                var val = reader.GetValue(i);
                rowDict[colNames[i]] = val is DBNull ? null : val;
            }
            results.Add(rowDict);
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
