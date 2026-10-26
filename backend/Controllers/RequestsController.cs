using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Requests;
using Requests.Services;
using Requests.Models;

namespace Requests.Controllers;

[ApiController]
[Route("api/requests")]
public class RequestsController : ControllerBase
{
    private readonly IRequestService _requests;
    private readonly ILogger<RequestsController> _logger;

    public RequestsController(IRequestService requests, ILogger<RequestsController> logger)
    {
        _requests = requests;
        _logger = logger;
    }

    // GET /api/requests?page=1&pageSize=25&search=..&status=New,Waiting...
    [HttpGet]
    public async Task<ActionResult<PagedResultDto>> Get(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? status = null,
        string? priority = null,
        string? organizationName = null,
        bool? organizationExact = null,
        string? assignedTo = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? sortBy = null,
        string? sortDir = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[REQ] GET page={Page} pageSize={PageSize} search={Search} status={Status} priority={Priority} org={Org} assignedTo={AssignedTo} from={From} to={To} sort={SortBy} {SortDir}",
            page, pageSize, search, status, priority, organizationName, assignedTo,
            fromDate?.ToString("s"), toDate?.ToString("s"), sortBy, sortDir);

        // HTTP contract validation only - business rules live in the service.
        if (page < 1) return BadRequest("page חייב להיות >= 1");
        if (pageSize is < 1 or > 100) return BadRequest("pageSize בין 1 ל-100");
        if (fromDate is not null && toDate is not null && fromDate > toDate)
            return BadRequest("fromDate לא יכול להיות אחרי toDate");

        var query = new RequestQuery(
            page, pageSize, search, status, priority,
            organizationName, organizationExact, assignedTo,
            fromDate, toDate, sortBy, sortDir);

        var sw = Stopwatch.StartNew();
        var result = await _requests.GetPageAsync(query, ct);
        sw.Stop();

        _logger.LogInformation("[REQ] GET done in {Ms} ms", Math.Round(sw.Elapsed.TotalMilliseconds, 2));
        return Ok(result);
    }

    // GET /api/requests/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ServiceRequestDto>> GetById(int id, CancellationToken ct)
    {
        _logger.LogInformation("[REQ] GET by id={Id}", id);

        var sw = Stopwatch.StartNew();
        var dto = await _requests.GetByIdAsync(id, ct);
        sw.Stop();

        _logger.LogInformation("[REQ] GET id={Id} found={Found} in {Ms} ms",
            id, dto is not null, Math.Round(sw.Elapsed.TotalMilliseconds, 2));

        return dto is null ? NotFound() : Ok(dto);
    }

    // GET /api/requests/{id}/history
    [HttpGet("{id:int}/history")]
    public async Task<ActionResult<List<StatusHistoryDto>>> GetHistory(int id, CancellationToken ct)
    {
        _logger.LogInformation("[REQ] GET history id={Id}", id);

        var history = await _requests.GetHistoryAsync(id, ct);

        _logger.LogInformation("[REQ] history id={Id} found={Found} rows={Rows}",
            id, history is not null, history?.Count ?? 0);

        return history is null ? NotFound() : Ok(history);
    }

    // GET /api/requests/aggregations
    //
    // Cached: one global summary entry, invalidated by every write path.
    [HttpGet("aggregations")]
    public async Task<ActionResult<AggregationsDto>> GetAggregations(CancellationToken ct)
    {
        _logger.LogInformation("[REQ] GET aggregations (cache hit/miss visible in service logs)");

        var sw = Stopwatch.StartNew();
        var dto = await _requests.GetAggregationsAsync(ct);
        sw.Stop();

        _logger.LogInformation("[REQ] aggregations done in {Ms} ms",
            Math.Round(sw.Elapsed.TotalMilliseconds, 2));

        return Ok(dto);
    }

    // PATCH /api/requests/{id}/status
    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<UpdateStatusResult>> UpdateStatus(
        int id, [FromBody] UpdateStatusPayload body, CancellationToken ct)
    {
        var user = User?.Identity?.Name ?? "system";
        _logger.LogInformation("[REQ] PATCH status id={Id} to={Status} by={User}", id, body.Status, user);

        var outcome = await _requests.UpdateStatusAsync(id, body, user, ct);

        _logger.LogInformation("[REQ] PATCH status id={Id} outcome={Outcome} message={Message}",
            id, outcome.Outcome, outcome.Message);

        // The single place where a business outcome becomes an HTTP status.
        return outcome.Outcome switch
        {
            StatusUpdateOutcome.Success => Ok(outcome.Result),
            StatusUpdateOutcome.NotFound => NotFound(),
            StatusUpdateOutcome.Conflict => Conflict(new { message = outcome.Message }),
            _ => BadRequest(outcome.Message)
        };
    }

    // PATCH /api/requests/bulk-status
    [HttpPatch("bulk/status")]
    [ProducesResponseType(typeof(BulkOutcome), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BulkOutcome>> BulkUpdateStatus([FromBody] BulkStatusPayload body, CancellationToken ct)
    {
        if (body?.Ids is null || body.Ids.Count == 0)                 // 1
            return BadRequest(new { message = "No ids supplied." });  // 2
        if (body.Ids.Count > RequestService.MaxBulkIds)               // 3
            return BadRequest(new { message = $"At most {RequestService.MaxBulkIds} ids per request." });

        var outcome = await _service.BulkUpdateStatusAsync(body, CurrentUser, ct);
        return outcome.Kind switch
        {
            BulkOutcomeKind.Success => Ok(outcome),
            BulkOutcomeKind.Conflict => Conflict(outcome),
            BulkOutcomeKind.EmptyIds => BadRequest(outcome),
            BulkOutcomeKind.TooManyIds => BadRequest(outcome),
            BulkOutcomeKind.InvalidStatus => UnprocessableEntity(outcome),
            _ => Ok(outcome)
        };
    }
}