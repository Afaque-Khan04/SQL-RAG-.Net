using Microsoft.AspNetCore.Mvc;
using AdhocSystem.Api.Services.Session;

namespace AdhocSystem.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class SessionController : ControllerBase
{
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<SessionController> _logger;

    public SessionController(ISessionManager sessionManager, ILogger<SessionController> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    [HttpPost("new")]
    public ActionResult<object> CreateNewSession()
    {
        var sessionId = _sessionManager.CreateSession();
        return Ok(new { session_id = sessionId, status = "created" });
    }

    [HttpGet("{sessionId}/history")]
    public ActionResult<object> GetSessionHistory(string sessionId)
    {
        var history = _sessionManager.GetSessionHistory(sessionId);
        return Ok(new { session_id = sessionId, turns = history });
    }

    [HttpDelete("{sessionId}")]
    public ActionResult<object> ClearSession(string sessionId)
    {
        _sessionManager.ClearSession(sessionId);
        return Ok(new { session_id = sessionId, status = "cleared" });
    }
}
