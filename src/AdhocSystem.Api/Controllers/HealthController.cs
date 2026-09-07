using Microsoft.AspNetCore.Mvc;
using AdhocSystem.Api.Models.Responses;
using AdhocSystem.Api.Services.Database;

namespace AdhocSystem.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    private readonly IDatabaseService _databaseService;

    public HealthController(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    [HttpGet]
    public async Task<ActionResult<HealthResponse>> HealthCheck(CancellationToken cancellationToken)
    {
        var dbOk = await _databaseService.CheckConnectionAsync(cancellationToken);

        return Ok(new HealthResponse
        {
            Status = "ok",
            Database = dbOk ? "AdventureWorks2022 (SQL Server connected)" : "SQL Server unreachable",
            VectorStore = "configured (Chroma REST sidecar)"
        });
    }
}
