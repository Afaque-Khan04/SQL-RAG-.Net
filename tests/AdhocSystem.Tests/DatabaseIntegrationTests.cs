using AdhocSystem.Api.Models.Common;
using AdhocSystem.Api.Services.Database;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AdhocSystem.Tests;

public class DatabaseIntegrationTests
{
    private readonly DatabaseOptions _options = new()
    {
        ConnectionString = "Server=.\\SQLEXPRESS;Database=AdventureWorks2022;Trusted_Connection=True;TrustServerCertificate=True;",
        ReadOnlyConnectionString = "Server=.\\SQLEXPRESS;Database=AdventureWorks2022;Trusted_Connection=True;TrustServerCertificate=True;",
        AllowedSchemas = new List<string> { "Sales", "Purchasing", "Production" }
    };

    [Fact]
    public async Task CanConnectToDatabase()
    {
        await using var conn = new Microsoft.Data.SqlClient.SqlConnection(_options.ConnectionString);
        await conn.OpenAsync();
        Assert.Equal(System.Data.ConnectionState.Open, conn.State);
    }

    [Fact]
    public async Task SchemaIntrospector_LoadsAllowedSchemas()
    {
        var introspector = new SchemaIntrospector(Options.Create(_options), NullLogger<SchemaIntrospector>.Instance);
        var context = await introspector.GetDynamicSchemaContextAsync();

        Assert.NotNull(context);
        Assert.Contains("Database: AdventureWorks2022", context);
        Assert.Contains("Sales.SalesOrderHeader", context);
        Assert.Contains("Production.Product", context);
        Assert.Contains("Purchasing.PurchaseOrderHeader", context);
        Assert.Contains("Sales.vSalesPerson", context);
    }
}
