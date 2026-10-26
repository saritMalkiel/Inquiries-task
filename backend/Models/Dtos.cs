namespace Requests.Models;

public record ServiceRequestDto(int Id, string Title, string? Description, string OrganizationName, string Status, string Priority, string? AssignedTo, DateTime CreatedAt, DateTime UpdatedAt, string? RowVersion);
public record PagedResultDto(int Page, int PageSize, int TotalCount, int TotalPages, List<ServiceRequestDto> Items);
public record StatusHistoryDto(string PreviousStatus, string NewStatus, DateTime ChangedAt, string ChangedBy);
public record AggregationsDto(int TotalRequests, Dictionary<string, int> ByStatus, Dictionary<string, int> ByPriority, DateTime GeneratedAt);
public record UpdateStatusPayload(string Status, string? RowVersion);
public record UpdateStatusResult(int Id, string Status, string RowVersion, DateTime UpdatedAt);
public record BulkStatusPayload(List<int> Ids, string Status);
public class BulkStatusResult { public int Updated { get; set; } public List<BulkFailure> Failed { get; set; } = new(); }
public record BulkFailure(int Id, string Reason);