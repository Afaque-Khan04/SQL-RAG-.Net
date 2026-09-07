using AdhocSystem.Api.Services.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AdhocSystem.Tests;

public class SqlGuardTests
{
    private readonly SqlGuardService _guard = new(NullLogger<SqlGuardService>.Instance);

    [Theory]
    [InlineData("SELECT * FROM Sales.SalesOrderHeader")]
    [InlineData("SELECT TOP 5 ProductID, Name FROM Production.Product WHERE ListPrice > 100 ORDER BY ListPrice DESC")]
    [InlineData("SELECT * FROM (SELECT TOP 5 * FROM Sales.SalesOrderDetail ORDER BY LineTotal DESC) AS a")]
    [InlineData("WITH Sales_CTE AS (SELECT SalesOrderID FROM Sales.SalesOrderHeader) SELECT * FROM Sales_CTE")]
    public void ValidQueries_ShouldPass(string sql)
    {
        var cleaned = _guard.ValidateAndSanitize(sql);
        Assert.NotNull(cleaned);
    }

    [Theory]
    [InlineData("DROP TABLE Sales.SalesOrderHeader")]
    [InlineData("DELETE FROM Production.Product WHERE ProductID = 1")]
    [InlineData("INSERT INTO Production.Product (Name) VALUES ('Test')")]
    [InlineData("UPDATE Sales.SalesOrderDetail SET UnitPrice = 0")]
    [InlineData("ALTER TABLE Sales.Customer ADD InjectedCol int")]
    [InlineData("SELECT * FROM Production.Product; DROP TABLE Sales.SalesOrderHeader")]
    [InlineData("EXEC sp_who2")]
    [InlineData("SELECT * INTO NewTable FROM Production.Product")]
    public void DestructiveOrDangerousQueries_ShouldThrow(string sql)
    {
        Assert.Throws<SqlGuardSecurityException>(() => _guard.ValidateAndSanitize(sql));
    }
}
