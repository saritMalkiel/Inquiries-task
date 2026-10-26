using Microsoft.EntityFrameworkCore;
using Requests.Data;
using Requests.Models;

namespace Requests.Services;

/// <summary>
/// All request logic lives here: query composition, cache coordination,
/// concurrency handling, history writing. The controller only binds HTTP,
/// validates the contract, and maps outcomes to status codes.
/// </summary>
public class RequestService : IRequestService
{
    private const int MaxBulkIds = 500;

    private readonly AppDb _db;
    private readonly IAggregationsCache _cache;

    public RequestService(AppDb db, IAggregationsCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<PagedResultDto> GetPageAsync(RequestQuery query, CancellationToken ct = default)
    {
        var q = _db.Requests.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(x => x.Title.Contains(term) || x.OrganizationName.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(query.OrganizationName))
        {
            var org = query.OrganizationName.Trim();
            q = query.OrganizationExact == true
                ? q.Where(x => x.OrganizationName == org)
                : q.Where(x => x.OrganizationName.StartsWith(org));
        }

        if (!string.IsNullOrWhiteSpace(query.AssignedTo))
        {
            var who = query.AssignedTo.Trim();
            q = q.Where(x => x.AssignedTo != null && x.AssignedTo.StartsWith(who));
        }

        // The column may hold "New" or legacy "0"; match BOTH so filtering works
        // regardless of what was seeded into the nvarchar column.
        var statusNames = ParseEnumNames<Status>(query.Status);
        if (statusNames.Count > 0)
        {
            var variants = statusNames
                .SelectMany(n => new[] { n, ((int)Enum.Parse<Status>(n, true)).ToString() })
                .ToList();
            q = q.Where(x => variants.Contains(x.StatusRaw));
        }

        var priorityNames = ParseEnumNames<Priority>(query.Priority);
        if (priorityNames.Count > 0)
        {
            var variants = priorityNames
                .SelectMany(n => new[] { n, ((int)Enum.Parse<Priority>(n, true)).ToString() })
                .ToList();
            q = q.Where(x => variants.Contains(x.PriorityRaw));
        }

        if (query.FromDate is not null) q = q.Where(x => x.CreatedAt >= query.FromDate.Value);
        if (query.ToDate is not null)
        {
            var end = query.ToDate.Value.Date.AddDays(1);
            q = q.Where(x => x.CreatedAt < end);
        }

        q = ApplySort(q, query.SortBy, query.SortDir);

        // Count and page are separate server-side statements: Skip/Take becomes
        // OFFSET ... FETCH NEXT, and the projection runs AFTER Take, so only
        // pageSize rows ever cross the wire.
        var total = await q.CountAsync(ct);
        var totalPages = (int)Math.Ceiling(total / (double)query.PageSize);

        var items = await q
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ToDto)
            .ToListAsync(ct);

        items = items.Select(d => d with
        {
            Status = NormalizeName<Status>(d.Status, Status.New),
            Priority = NormalizeName<Priority>(d.Priority, Priority.Low)
        }).ToList();

        return new PagedResultDto(query.Page, query.PageSize, total, totalPages, items);
    }

    public async Task<ServiceRequestDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var dto = await _db.Requests.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(ToDto)
            .FirstOrDefaultAsync(ct);

        return dto is null ? null : dto with
        {
            Status = NormalizeName<Status>(dto.Status, Status.New),
            Priority = NormalizeName<Priority>(dto.Priority, Priority.Low)
        };
    }

    /// <returns>null when the request does not exist.</returns>
    public async Task<List<StatusHistoryDto>?> GetHistoryAsync(int id, CancellationToken ct = default)
    {
        var exists = await _db.Requests.AnyAsync(x => x.Id == id, ct);
        if (!exists) return null;

        var history = await _db.StatusChanges.AsNoTracking()
            .Where(x => x.RequestId == id)
            .OrderByDescending(x => x.ChangedAt)
            .Select(x => new StatusHistoryDto(
                x.PreviousStatusRaw,
                x.NewStatusRaw,
                x.ChangedAt,
                x.ChangedBy))
            .ToListAsync(ct);

        return history.Select(h => h with
        {
            PreviousStatus = NormalizeName<Status>(h.PreviousStatus, Status.New),
            NewStatus = NormalizeName<Status>(h.NewStatus, Status.New)
        }).ToList();
    }

    public async Task<AggregationsDto> GetAggregationsAsync(CancellationToken ct = default)
    {
        if (_cache.TryGet(out var cached) && cached is not null) return cached;

        using (await _cache.AcquireRebuildLockAsync(ct))
        {
            // Double-check: someone may have rebuilt while we waited.
            if (_cache.TryGet(out cached) && cached is not null) return cached;

            var total = await _db.Requests.CountAsync(ct);

            var byStatusRaw = await _db.Requests
                .GroupBy(x => x.StatusRaw)
                .Select(g => new { Key = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var byPriorityRaw = await _db.Requests
                .GroupBy(x => x.PriorityRaw)
                .Select(g => new { Key = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var dto = new AggregationsDto(
                total,
                FillAllStatuses(byStatusRaw.ToDictionary(x => x.Key, x => x.Count)),
                FillAllPriorities(byPriorityRaw.ToDictionary(x => x.Key, x => x.Count)),
                DateTime.UtcNow);

            _cache.Set(dto);
            return dto;
        }
    }

    public async Task<StatusUpdateResult> UpdateStatusAsync(
        int id, UpdateStatusPayload body, string user, CancellationToken ct = default)
    {
        if (!Enum.TryParse<Status>(body.Status, true, out var next))
            return new StatusUpdateResult(StatusUpdateOutcome.InvalidStatus, Message: "סטטוס לא חוקי");

        var entity = await _db.Requests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return new StatusUpdateResult(StatusUpdateOutcome.NotFound);

        if (entity.Status == next)
            return new StatusUpdateResult(StatusUpdateOutcome.SameStatus, Message: "הסטטוס זהה לסטטוס הנוכחי");

        if (!string.IsNullOrEmpty(body.RowVersion))
        {
            try
            {
                _db.Entry(entity).Property(x => x.RowVersion).OriginalValue =
                    Convert.FromBase64String(body.RowVersion);
            }
            catch (FormatException)
            {
                return new StatusUpdateResult(StatusUpdateOutcome.BadRowVersion, Message: "RowVersion לא תקין");
            }
        }

        var prev = entity.Status;
        entity.Status = next;
        entity.UpdatedAt = DateTime.UtcNow;

        _db.StatusChanges.Add(new StatusChange
        {
            RequestId = entity.Id,
            PreviousStatus = prev,
            NewStatus = next,
            ChangedAt = entity.UpdatedAt,
            ChangedBy = user
        });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new StatusUpdateResult(
                StatusUpdateOutcome.Conflict,
                Message: "הרשומה עודכנה בינתיים ע\"י משתמש אחר");
        }

        // Only after a successful commit - invalidating earlier would let a reader
        // rebuild from the old rows and cache them for a full TTL.
        _cache.Invalidate();

        return new StatusUpdateResult(
            StatusUpdateOutcome.Success,
            new UpdateStatusResult(
                entity.Id,
                entity.Status.ToString(),
                entity.RowVersion is null ? string.Empty : Convert.ToBase64String(entity.RowVersion),
                entity.UpdatedAt));
    }

    public async Task<BulkOutcome> BulkUpdateStatusAsync(
        BulkStatusPayload body, string user, CancellationToken ct = default)
    {
        if (!Enum.TryParse<Status>(body.Status, true, out var next))
            return new BulkOutcome(BulkOutcomeKind.InvalidStatus, Message: "סטטוס לא חוקי");

        if (body.Ids is null || body.Ids.Count == 0)
            return new BulkOutcome(BulkOutcomeKind.EmptyIds, Message: "לא נשלחו רשומות");

        if (body.Ids.Count > MaxBulkIds)
            return new BulkOutcome(BulkOutcomeKind.TooManyIds, Message: $"עד {MaxBulkIds} רשומות בעדכון מרוכז");

        var result = new BulkStatusResult();
        var entities = await _db.Requests
            .Where(x => body.Ids.Contains(x.Id))
            .ToListAsync(ct);

        var found = entities.Select(x => x.Id).ToHashSet();
        foreach (var missing in body.Ids.Where(i => !found.Contains(i)))
            result.Failed.Add(new BulkFailure(missing, "הרשומה לא נמצאה"));

        var now = DateTime.UtcNow;
        foreach (var entity in entities)
        {
            if (entity.Status == next)
            {
                result.Failed.Add(new BulkFailure(entity.Id, "הסטטוס זהה לנוכחי"));
                continue;
            }

            var prev = entity.Status;
            entity.Status = next;
            entity.UpdatedAt = now;

            _db.StatusChanges.Add(new StatusChange
            {
                RequestId = entity.Id,
                PreviousStatus = prev,
                NewStatus = next,
                ChangedAt = now,
                ChangedBy = user
            });

            result.Updated++;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new BulkOutcome(
                BulkOutcomeKind.Conflict,
                Message: "אחת הרשומות עודכנה בינתיים — נסה שוב");
        }

        if (result.Updated > 0) _cache.Invalidate();

        return new BulkOutcome(BulkOutcomeKind.Success, result);
    }

    // === internals ===

    private static readonly System.Linq.Expressions.Expression<Func<ServiceRequest, ServiceRequestDto>> ToDto =
        x => new ServiceRequestDto(
            x.Id, x.Title, x.Description, x.OrganizationName,
            x.StatusRaw, x.PriorityRaw, x.AssignedTo,
            x.CreatedAt, x.UpdatedAt,
            x.RowVersion == null ? null : Convert.ToBase64String(x.RowVersion));

    private static List<string> ParseEnumNames<TEnum>(string? csv) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(csv)) return new List<string>();

        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .Select(s => Enum.TryParse<TEnum>(s, true, out var v) ? v.ToString() : null)
                  .Where(s => s is not null)
                  .Select(s => s!)
                  .Distinct()
                  .ToList();
    }

    private static string NormalizeName<TEnum>(string? raw, TEnum fallback)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(raw)) return fallback.ToString();

        var trimmed = raw.Trim();

        if (Enum.TryParse<TEnum>(trimmed, true, out var byName)) return byName.ToString();

        if (int.TryParse(trimmed, out var ordinal) && Enum.IsDefined(typeof(TEnum), ordinal))
            return ((TEnum)Enum.ToObject(typeof(TEnum), ordinal)).ToString();

        return fallback.ToString();
    }

    private static IQueryable<ServiceRequest> ApplySort(
        IQueryable<ServiceRequest> q, string? sortBy, string? sortDir)
    {
        var desc = !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);

        return (sortBy?.ToLowerInvariant()) switch
        {
            "title" => desc
                ? q.OrderByDescending(x => x.Title).ThenBy(x => x.Id)
                : q.OrderBy(x => x.Title).ThenBy(x => x.Id),

            "status" => desc
                ? q.OrderByDescending(x => x.StatusRaw == "New" ? 4 :
                    x.StatusRaw == "InProgress" ? 3 : x.StatusRaw == "Waiting" ? 2 : 1)
                   .ThenByDescending(x => x.Id)
                : q.OrderBy(x => x.StatusRaw == "New" ? 4 :
                    x.StatusRaw == "InProgress" ? 3 : x.StatusRaw == "Waiting" ? 2 : 1)
                   .ThenBy(x => x.Id),

            "priority" => desc
                ? q.OrderByDescending(x => x.PriorityRaw == "High" ? 3 :
                    x.PriorityRaw == "Medium" ? 2 : 1)
                   .ThenByDescending(x => x.Id)
                : q.OrderBy(x => x.PriorityRaw == "High" ? 3 :
                    x.PriorityRaw == "Medium" ? 2 : 1)
                   .ThenBy(x => x.Id),

            "updatedat" => desc
                ? q.OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id)
                : q.OrderBy(x => x.UpdatedAt).ThenBy(x => x.Id),

            _ => desc
                ? q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                : q.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
        };
    }

    private static Dictionary<string, int> FillAllStatuses(Dictionary<string, int> counts)
    {
        var result = Enum.GetNames<Status>().ToDictionary(n => n, _ => 0);
        foreach (var (k, v) in counts)
        {
            var name = NormalizeName<Status>(k, Status.New);
            if (result.ContainsKey(name)) result[name] += v;
        }
        return result;
    }

    private static Dictionary<string, int> FillAllPriorities(Dictionary<string, int> counts)
    {
        var result = Enum.GetNames<Priority>().ToDictionary(n => n, _ => 0);
        foreach (var (k, v) in counts)
        {
            var name = NormalizeName<Priority>(k, Priority.Low);
            if (result.ContainsKey(name)) result[name] += v;
        }
        return result;
    }
}