using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using AdhocSystem.Api.Models.Common;
using AdhocSystem.Api.Models.Requests;
using AdhocSystem.Api.Models.Responses;
using AdhocSystem.Api.Services.Audio;
using AdhocSystem.Api.Services.NL2SQL;
using AdhocSystem.Api.Services.Routing;
using AdhocSystem.Api.Services.Semantic;
using AdhocSystem.Api.Services.Session;

namespace AdhocSystem.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class QueryController : ControllerBase
{
    private readonly IIntentRouter _intentRouter;
    private readonly INl2SqlService _nl2SqlService;
    private readonly ISemanticSearchService _semanticSearchService;
    private readonly ISpeechToTextService _sttService;
    private readonly ISessionManager _sessionManager;
    private readonly VectorStoreOptions _vectorOptions;
    private readonly DatabaseOptions _dbOptions;
    private readonly ILogger<QueryController> _logger;

    public QueryController(
        IIntentRouter intentRouter,
        INl2SqlService nl2SqlService,
        ISemanticSearchService semanticSearchService,
        ISpeechToTextService sttService,
        ISessionManager sessionManager,
        Microsoft.Extensions.Options.IOptions<VectorStoreOptions> vectorOptions,
        Microsoft.Extensions.Options.IOptions<DatabaseOptions> dbOptions,
        ILogger<QueryController> logger)
    {
        _intentRouter = intentRouter;
        _nl2SqlService = nl2SqlService;
        _semanticSearchService = semanticSearchService;
        _sttService = sttService;
        _sessionManager = sessionManager;
        _vectorOptions = vectorOptions.Value;
        _dbOptions = dbOptions.Value;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<QueryResponse>> ProcessQuery([FromBody] QueryRequest request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var queryStr = request.Query?.Trim();

        if (string.IsNullOrWhiteSpace(queryStr))
        {
            return BadRequest(new { detail = "Query string cannot be empty." });
        }

        try
        {
            // 1. Resolve active session & previous turn context
            var (sessionId, session) = _sessionManager.GetOrCreateSession(request.SessionId);
            var previousTurn = request.IsFollowUp ? _sessionManager.GetLastTurn(sessionId) : null;

            _logger.LogInformation("Incoming query (Session: {SessionId}, Follow-up: {IsFollowUp}, Turn #{Turn}): '{Query}'",
                sessionId, request.IsFollowUp, session.Turns.Count + 1, queryStr);

            // 2. Route intent
            IntentType intent;
            if (request.SelectedRowContext != null && request.SelectedRowContext.Count > 0)
            {
                intent = IntentType.Structured;
                _logger.LogInformation("Targeting active row entity context -> Intent locked to STRUCTURED");
            }
            else
            {
                intent = await _intentRouter.RouteAsync(queryStr, cancellationToken);
            }

            string? generatedSql = null;
            var results = new List<Dictionary<string, object?>>();

            // 3. Execute retrieval path
            if (intent == IntentType.Structured)
            {
                var (sql, data) = await _nl2SqlService.GenerateAndExecuteAsync(
                    queryStr,
                    previousTurn,
                    request.SelectedRowContext,
                    request.IsFollowUp,
                    cancellationToken);

                generatedSql = sql;
                results = data;
            }
            else
            {
                var defaultTopK = _vectorOptions.DefaultTopK > 0 ? _vectorOptions.DefaultTopK : 25;
                var maxTopK = _vectorOptions.MaxTopK > 0 ? _vectorOptions.MaxTopK : 1000;
                var topK = request.TopK.HasValue && request.TopK.Value > 0 ? request.TopK.Value : defaultTopK;

                var match = Regex.Match(queryStr, @"\b(?:top|limit|first|show)\s*(\d+)\b", RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var parsedK))
                {
                    topK = parsedK;
                }

                topK = Math.Clamp(topK, 1, maxTopK);

                results = await _semanticSearchService.SearchAsync(queryStr, topK, cancellationToken);
            }

            // 4. Record completed turn into session manager
            var turn = _sessionManager.RecordTurn(
                sessionId: sessionId,
                query: queryStr,
                intent: intent.ToString().ToLowerInvariant(),
                generatedSql: generatedSql,
                results: results,
                selectedRowContext: request.SelectedRowContext
            );

            // 5. Metadata and UI safety ceiling
            var totalRecords = results.Count;
            var maxLimit = _dbOptions.MaxResultLimit;
            var isTruncated = maxLimit > 0 && results.Count > maxLimit;
            var displayResults = isTruncated ? results.Take(maxLimit).ToList() : results;

            var answer = isTruncated
                ? $"Retrieved {displayResults.Count:N0} records (capped by MaxResultLimit configuration from {totalRecords:N0} total matching rows) via {intent.ToString().ToLowerInvariant()} query."
                : $"Successfully retrieved {totalRecords:N0} records from database via {intent.ToString().ToLowerInvariant()} query.";

            sw.Stop();

            return Ok(new QueryResponse
            {
                Query = queryStr,
                Intent = intent,
                GeneratedSql = generatedSql,
                Results = displayResults,
                TotalRecords = totalRecords,
                IsTruncated = isTruncated,
                Answer = answer,
                ExecutionTimeMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2),
                SessionId = sessionId,
                TurnIndex = turn.TurnIndex,
                ParentQuery = previousTurn?.Query
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Failed to execute query '{Query}': {Message}", queryStr, ex.Message);
            return StatusCode(500, new
            {
                detail = ex.Message,
                error = ex.Message,
                isError = true,
                query = queryStr,
                executionTimeMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2)
            });
        }
    }

    [HttpPost("transcribe")]
    public async Task<ActionResult<TranscribeResponse>> Transcribe(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { detail = "Empty audio file provided." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var (text, lang, duration) = await _sttService.TranscribeAudioAsync(stream, file.FileName, cancellationToken);

            return Ok(new TranscribeResponse
            {
                Text = text,
                Language = lang,
                Duration = Math.Round(duration, 2)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to transcribe audio: {Message}", ex.Message);
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpPost("query_audio")]
    public async Task<ActionResult<AudioQueryResponse>> ProcessAudioQuery(
        [FromForm] IFormFile? file,
        [FromForm] string? sessionId,
        [FromForm] bool isFollowUp = false,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { detail = "Empty audio file provided." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var (transcribedText, lang, duration) = await _sttService.TranscribeAudioAsync(stream, file.FileName, cancellationToken);

            if (string.IsNullOrWhiteSpace(transcribedText))
            {
                return BadRequest(new { detail = "No speech detected in audio file." });
            }

            var queryReq = new QueryRequest
            {
                Query = transcribedText,
                SessionId = sessionId,
                IsFollowUp = isFollowUp
            };

            var queryResultAction = await ProcessQuery(queryReq, cancellationToken);
            if (queryResultAction.Result is not OkObjectResult okResult || okResult.Value is not QueryResponse queryResp)
            {
                return StatusCode(500, new { detail = "Internal processing error on query execution." });
            }

            sw.Stop();

            return Ok(new AudioQueryResponse
            {
                TranscribedText = transcribedText,
                Language = lang,
                AudioDurationSeconds = Math.Round(duration, 2),
                Intent = queryResp.Intent,
                GeneratedSql = queryResp.GeneratedSql,
                Results = queryResp.Results,
                TotalRecords = queryResp.TotalRecords,
                IsTruncated = queryResp.IsTruncated,
                Answer = queryResp.Answer,
                ExecutionTimeMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2),
                SessionId = queryResp.SessionId,
                TurnIndex = queryResp.TurnIndex
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Failed to process audio query: {Message}", ex.Message);
            return StatusCode(500, new
            {
                detail = ex.Message,
                error = ex.Message,
                isError = true,
                executionTimeMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2)
            });
        }
    }
}
