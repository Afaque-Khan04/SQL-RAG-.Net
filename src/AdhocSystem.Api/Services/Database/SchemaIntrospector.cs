using System.Text;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using AdhocSystem.Api.Models.Common;

namespace AdhocSystem.Api.Services.Database;

public class SchemaIntrospector : ISchemaIntrospector
{
    private readonly DatabaseOptions _options;
    private readonly ILogger<SchemaIntrospector> _logger;
    private static string? _schemaCache;
    private static readonly SemaphoreSlim _cacheLock = new(1, 1);

    private static readonly HashSet<string> SkipTables = new(StringComparer.OrdinalIgnoreCase)
    {
        // Production
        "Culture", "Document", "Illustration", "ProductDocument",
        "ProductModelIllustration", "ProductModelProductDescriptionCulture",
        "ProductPhoto", "ProductProductPhoto", "ProductReview",
        // Sales
        "CountryRegionCurrency", "CreditCard", "Currency", "CurrencyRate",
        "PersonCreditCard", "SalesOrderHeaderSalesReason", "SalesPersonQuotaHistory",
        "SalesTaxRate", "SalesTerritoryHistory", "ShoppingCartItem",
        // Purchasing
        "ShipMethod"
    };

    public SchemaIntrospector(IOptions<DatabaseOptions> options, ILogger<SchemaIntrospector> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public void InvalidateCache()
    {
        _schemaCache = null;
    }

    public async Task<string> GetDynamicSchemaContextAsync(CancellationToken cancellationToken = default)
    {
        if (_schemaCache != null)
        {
            return _schemaCache;
        }

        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_schemaCache != null)
            {
                return _schemaCache;
            }

            _logger.LogInformation("Introspecting database schema for schemas: {Schemas}...", string.Join(", ", _options.AllowedSchemas));

            var connectionString = !string.IsNullOrWhiteSpace(_options.ReadOnlyConnectionString)
                ? _options.ReadOnlyConnectionString
                : _options.ConnectionString;

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var sb = new StringBuilder();
            sb.AppendLine($"Database: AdventureWorks2022 (schemas: {string.Join(", ", _options.AllowedSchemas)})");

            // 1. Query tables and views
            var tableSql = @"
                SELECT s.name AS SchemaName, t.name AS TableName, 'TABLE' AS ObjectType
                FROM sys.tables t
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                WHERE s.name IN @Schemas
                UNION ALL
                SELECT s.name AS SchemaName, v.name AS TableName, 'VIEW' AS ObjectType
                FROM sys.views v
                INNER JOIN sys.schemas s ON v.schema_id = s.schema_id
                WHERE (s.name = 'Sales' AND v.name IN ('vSalesPerson', 'vIndividualCustomer'))
                ORDER BY SchemaName, TableName";

            var objects = (await connection.QueryAsync<(string SchemaName, string TableName, string ObjectType)>(
                tableSql, new { Schemas = _options.AllowedSchemas })).ToList();

            // 2. Query all columns for allowed schemas
            var columnSql = @"
                SELECT 
                    s.name AS SchemaName,
                    o.name AS TableName,
                    c.name AS ColumnName,
                    tp.name AS DataType,
                    c.max_length AS MaxLength,
                    c.precision AS Precision,
                    c.scale AS Scale,
                    CASE WHEN pk.column_id IS NOT NULL THEN 1 ELSE 0 END AS IsPrimaryKey
                FROM sys.columns c
                INNER JOIN sys.objects o ON c.object_id = o.object_id
                INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
                INNER JOIN sys.types tp ON c.user_type_id = tp.user_type_id
                LEFT JOIN (
                    SELECT ic.object_id, ic.column_id
                    FROM sys.index_columns ic
                    INNER JOIN sys.indexes i ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                    WHERE i.is_primary_key = 1
                ) pk ON c.object_id = pk.object_id AND c.column_id = pk.column_id
                WHERE s.name IN @Schemas AND c.name NOT IN ('rowguid', 'ModifiedDate')
                ORDER BY s.name, o.name, c.column_id";

            var allColumns = (await connection.QueryAsync<(
                string SchemaName, string TableName, string ColumnName, string DataType, 
                short MaxLength, byte Precision, byte Scale, int IsPrimaryKey
            )>(columnSql, new { Schemas = _options.AllowedSchemas })).ToList();

            var columnsByTable = allColumns
                .GroupBy(c => $"{c.SchemaName}.{c.TableName}", StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            // 3. Query all foreign keys
            var fkSql = @"
                SELECT 
                    s_parent.name AS ParentSchema,
                    o_parent.name AS ParentTable,
                    c_parent.name AS ParentColumn,
                    s_ref.name AS RefSchema,
                    o_ref.name AS RefTable,
                    c_ref.name AS RefColumn
                FROM sys.foreign_key_columns fkc
                INNER JOIN sys.objects o_parent ON fkc.parent_object_id = o_parent.object_id
                INNER JOIN sys.schemas s_parent ON o_parent.schema_id = s_parent.schema_id
                INNER JOIN sys.columns c_parent ON fkc.parent_object_id = c_parent.object_id AND fkc.parent_column_id = c_parent.column_id
                INNER JOIN sys.objects o_ref ON fkc.referenced_object_id = o_ref.object_id
                INNER JOIN sys.schemas s_ref ON o_ref.schema_id = s_ref.schema_id
                INNER JOIN sys.columns c_ref ON fkc.referenced_object_id = c_ref.object_id AND fkc.referenced_column_id = c_ref.column_id
                WHERE s_parent.name IN @Schemas
                ORDER BY ParentSchema, ParentTable, fkc.constraint_column_id";

            var allFks = (await connection.QueryAsync<(
                string ParentSchema, string ParentTable, string ParentColumn,
                string RefSchema, string RefTable, string RefColumn
            )>(fkSql, new { Schemas = _options.AllowedSchemas })).ToList();

            var fksByTable = allFks
                .GroupBy(f => $"{f.ParentSchema}.{f.ParentTable}", StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            int totalIncludedTables = 0;

            foreach (var obj in objects)
            {
                if (obj.ObjectType == "TABLE" && SkipTables.Contains(obj.TableName))
                {
                    continue;
                }

                var qualified = $"{obj.SchemaName}.{obj.TableName}";
                if (!columnsByTable.TryGetValue(qualified, out var cols) || cols.Count == 0)
                {
                    continue;
                }

                totalIncludedTables++;
                var colStrings = cols.Select(c =>
                {
                    var pk = c.IsPrimaryKey == 1 ? " PK" : "";
                    return $"{c.ColumnName}({c.DataType}){pk}";
                });

                sb.AppendLine();
                sb.AppendLine($"{qualified}: {string.Join(", ", colStrings)}");

                if (fksByTable.TryGetValue(qualified, out var fks) && fks.Count > 0)
                {
                    var fkStrings = fks.Select(f => $"{f.ParentColumn}->{f.RefSchema}.{f.RefTable}.{f.RefColumn}");
                    sb.AppendLine($"  FKs: {string.Join(", ", fkStrings)}");
                }
            }

            _schemaCache = sb.ToString();
            _logger.LogInformation("Schema introspection complete. {Count} tables/views introspected, {Length} characters.", totalIncludedTables, _schemaCache.Length);
            return _schemaCache;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to introspect database schema");
            return $"Database: AdventureWorks2022. Schemas: {string.Join(", ", _options.AllowedSchemas)}. Error introspecting schema: {ex.Message}";
        }
        finally
        {
            _cacheLock.Release();
        }
    }
}
