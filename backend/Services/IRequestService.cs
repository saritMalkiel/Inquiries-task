using Requests.Models;

namespace Requests.Services;


/// <summary>
/// Application-service seam between HTTP and persistence. Controllers know only
/// this interface: no DbContext, no LINQ, no cache.
/// </summary>
public interface IRequestService
{
    Task<PagedResultDto> GetPageAsync(RequestQuery query, CancellationToken ct = default);
    Task<ServiceRequestDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<StatusHistoryDto>?> GetHistoryAsync(int id, CancellationToken ct = default);
    Task<AggregationsDto> GetAggregationsAsync(CancellationToken ct = default);
    Task<StatusUpdateResult> UpdateStatusAsync(int id, UpdateStatusPayload body, string user, CancellationToken ct = default);
    Task<BulkOutcome> BulkUpdateStatusAsync(BulkStatusPayload body, string user, CancellationToken ct = default);
}

public record RequestQuery(
    int Page = 1,
    int PageSize = 25,
    string? Search = null,
    string? Status = null,
    string? Priority = null,
    string? OrganizationName = null,
    bool? OrganizationExact = null,
    string? AssignedTo = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? SortBy = null,
    string? SortDir = null);

public enum StatusUpdateOutcome
{
    Success,
    NotFound,
    InvalidStatus,
    SameStatus,
    Conflict,
    BadRowVersion
}

public record StatusUpdateResult(
    StatusUpdateOutcome Outcome,
    UpdateStatusResult? Result = null,
    string? Message = null);

public enum BulkOutcomeKind { Success, InvalidStatus, EmptyIds, TooManyIds, Conflict }

public record BulkOutcome(
    BulkOutcomeKind Kind,
    BulkStatusResult? Result = null,
    string? Message = null);