using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AdhocSystem.Api.Models.Common;
using AdhocSystem.Api.Models.Requests;
using AdhocSystem.Api.Models.Responses;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AdhocSystem.Tests;

public class EndToEndApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public EndToEndApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
        _client.Timeout = TimeSpan.FromMinutes(5);
    }

    [Fact]
    public async Task GetHealth_ReturnsOkAndConfiguredStatus()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var health = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(health);
        Assert.Equal("ok", health.Status);
        Assert.Contains("AdventureWorks2022", health.Database);
    }

    [Fact]
    public async Task SessionLifecycle_CreateAndQueryHistory()
    {
        var createRes = await _client.PostAsync("/session/new", null);
        Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);

        var content = await createRes.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var sessionId = doc.RootElement.GetProperty("session_id").GetString();
        Assert.NotNull(sessionId);

        var historyRes = await _client.GetAsync($"/session/{sessionId}/history");
        Assert.Equal(HttpStatusCode.OK, historyRes.StatusCode);
    }

    [Fact]
    public async Task PostQuery_Structured_ReturnsTsqlAndResults()
    {
        var request = new QueryRequest
        {
            Query = "What is the total sales revenue in 2013?"
        };

        var response = await _client.PostAsJsonAsync("/query", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var queryResponse = await response.Content.ReadFromJsonAsync<QueryResponse>();
        Assert.NotNull(queryResponse);
        Assert.Equal(IntentType.Structured, queryResponse.Intent);
        Assert.NotNull(queryResponse.GeneratedSql);
        Assert.StartsWith("SELECT", queryResponse.GeneratedSql.Trim(), StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(queryResponse.Results);
        Assert.True(queryResponse.TotalRecords > 0);
        Assert.False(queryResponse.IsTruncated);
    }

    [Fact]
    public async Task PostQuery_DetailedSalesReport2013_FetchesOver50kRecordsWithoutTruncation()
    {
        var request = new QueryRequest
        {
            Query = "Detailed sales report for 2013"
        };

        var response = await _client.PostAsJsonAsync("/query", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var queryResponse = await response.Content.ReadFromJsonAsync<QueryResponse>();
        Assert.NotNull(queryResponse);
        Assert.Equal(IntentType.Structured, queryResponse.Intent);
        Assert.NotNull(queryResponse.GeneratedSql);
        // AdventureWorks2022 has > 50,000 line items in 2013 (~53,248)
        Assert.True(queryResponse.TotalRecords > 50000, $"Expected >50k records, got {queryResponse.TotalRecords}");
        Assert.Equal(queryResponse.TotalRecords, queryResponse.Results.Count);
        Assert.False(queryResponse.IsTruncated);
    }
}
