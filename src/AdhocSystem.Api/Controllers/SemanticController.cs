using AdhocSystem.Api.Services.Semantic;
using Microsoft.AspNetCore.Mvc;

namespace AdhocSystem.Api.Controllers;

public record SemanticSearchTestRequest(string Query, int? TopK);

[ApiController]
[Route("api/[controller]")]
public class SemanticController : ControllerBase
{
    private readonly ISqliteSemanticStore _sqliteStore;
    private readonly ISemanticSearchService _semanticSearchService;
    private readonly ILogger<SemanticController> _logger;

    public SemanticController(
        ISqliteSemanticStore sqliteStore,
        ISemanticSearchService semanticSearchService,
        ILogger<SemanticController> logger)
    {
        _sqliteStore = sqliteStore;
        _semanticSearchService = semanticSearchService;
        _logger = logger;
    }

    [HttpGet("status")]
    public async Task<ActionResult<SemanticStoreStatus>> GetStatus(CancellationToken cancellationToken)
    {
        var status = await _sqliteStore.GetStatusAsync(cancellationToken);
        return Ok(status);
    }

    [HttpPost("sync")]
    public async Task<IActionResult> SyncCatalog(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Manual catalog sync requested via API.");
        var count = await _sqliteStore.SyncFromAdventureWorksAsync(cancellationToken);
        return Ok(new
        {
            success = true,
            indexedProducts = count,
            timestamp = DateTime.UtcNow
        });
    }

    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] SemanticSearchTestRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { detail = "Query cannot be empty." });
        }

        var topK = request.TopK.HasValue && request.TopK.Value > 0 ? request.TopK.Value : 5;
        var results = await _semanticSearchService.SearchAsync(request.Query, topK, cancellationToken);

        return Ok(new
        {
            query = request.Query,
            topK,
            count = results.Count,
            results
        });
    }
}
