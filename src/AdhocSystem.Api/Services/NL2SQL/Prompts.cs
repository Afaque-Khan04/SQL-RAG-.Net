namespace AdhocSystem.Api.Services.NL2SQL;

public static class Prompts
{
    public const string UniversalPrompt = @"Expert T-SQL generator for Microsoft SQL Server.
Generate a single valid, read-only SELECT statement answering the user query.
Return ONLY raw SQL. No markdown (no ```sql), no explanation, no comments.

{schema_context}

Rules:
1. Syntax: T-SQL (`TOP N`, `ISNULL()`). For year filters, use sargable ranges (e.g. `OrderDate >= '2013-01-01' AND OrderDate < '2014-01-01'`) for index optimization.
2. Row Limits: Use `TOP N` ONLY when user specifies an explicit count or rank (e.g. 'top 5'). Only join tables needed for the requested report.
3. Sorting/Filters: Obey 'ASC'/'DESC' when explicitly asked. Use `LIKE '%term%'` for case-insensitive string filters. Do NOT add arbitrary ORDER BY clauses to large ledger/detail queries unless explicitly requested by the user.
4. Sales Joins:
   - Order header to line items: `Sales.SalesOrderHeader h JOIN Sales.SalesOrderDetail d ON h.SalesOrderID = d.SalesOrderID`.
   - Customer: Join `Sales.Customer c ON h.CustomerID = c.CustomerID` (join `Person.Person p ON c.PersonID = p.BusinessEntityID` only if customer names are requested).
   - Salesperson: When salesperson info is requested, ALWAYS `LEFT JOIN Sales.SalesPerson sp ON h.SalesPersonID = sp.BusinessEntityID` (never `INNER JOIN`, online orders have NULL `SalesPersonID`). For salesperson name, `LEFT JOIN Person.Person sp_p ON sp.BusinessEntityID = sp_p.BusinessEntityID` (SalesPerson primary key is `BusinessEntityID`; do NOT use `PersonID` or `SalesPersonID`).
5. Products & Categories: `Production.Product p LEFT JOIN Production.ProductSubcategory subcat ON p.ProductSubcategoryID = subcat.ProductSubcategoryID LEFT JOIN Production.ProductCategory cat ON subcat.ProductCategoryID = cat.ProductCategoryID`.
   - Categories: 'Accessories' (`LIKE '%Access%'`), 'Bikes', 'Clothing' (`LIKE '%Cloth%'`), 'Components'.
   - Ranking: Order by `SUM(d.OrderQty) DESC` or `SUM(d.LineTotal) DESC`.
6. Purchasing: `Purchasing.PurchaseOrderHeader poh JOIN Purchasing.PurchaseOrderDetail pod ON poh.PurchaseOrderID = pod.PurchaseOrderID LEFT JOIN Purchasing.Vendor v ON poh.VendorID = v.BusinessEntityID`.
7. Unions: Wrap branches with `TOP`/`ORDER BY` in derived tables `SELECT * FROM (SELECT TOP N ... ORDER BY ...) AS sub` before `UNION [ALL]`.

Query: ""{query}""
T-SQL:";

    public const string ContextualPrompt = @"Expert T-SQL generator for Microsoft SQL Server multi-turn follow-up queries.
Generate a single valid, read-only SELECT statement. Return ONLY raw SQL.

{schema_context}

=== CONTEXT ===
Previous Query: ""{previous_query}""
Previous SQL:
{previous_sql}
{row_context_section}
===============

Rules:
1. Schema-qualified names: `Sales.*`, `Purchasing.*`, `Production.*`.
2. Entity Drill-down: Query detail/inventory/order tables for specific entity using `WHERE <EntityID> = <Value>` (e.g. `WHERE ProductID = 862`).
3. Positional reference: Use target entity IDs from TARGET ROW CONTEXT.
4. Refine previous query: Wrap previous SQL in a derived table `FROM ({previous_sql}) AS prev` or append WHERE/GROUP BY/ORDER BY.
5. Limits: `TOP N` only when explicit count requested.
6. Case-insensitive `LIKE '%term%'` for text filters.

Follow-Up Query: ""{query}""
T-SQL:";
}
