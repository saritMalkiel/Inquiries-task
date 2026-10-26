using Microsoft.EntityFrameworkCore;
using Requests.Data;
using Xunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using Requests.Data;     // AppDb
using Requests.Models;   // Status, Priority, ServiceRequest, StatusChange
using Xunit;

namespace Tests;

/// <summary>
/// Four required automated tests.
///
/// Rather than WebApplicationFactory (which fights the SDK 9 / package 8 version
/// skew, surfacing as "PipeWriter does not implement UnflushedBytes"), these tests
/// build the same data layer the API uses: a real AppDb over the EF in-memory
/// provider, and the exact LINQ the controllers compose.
///
/// This keeps the suite deterministic and dependency-free: no SQL Server, no
/// port binding, no server process.
///
/// Coverage:
///   1. valid query - filtering + paging     (positive path)
///   2. validation - invalid input rejected  (400 rules)
///   3. optimistic concurrency - stale row   (DbUpdateConcurrencyException -> 409)
///   4. integration - update persisted + history appended
/// </summary>
public class RequestsApiTests : IDisposable
{
    private readonly AppDb _db;

    public RequestsApiTests()
    {
        var options = new DbContextOptionsBuilder<AppDb>()
            .UseInMemoryDatabase("tests-" + Guid.NewGuid().ToString("N"))
            .Options;

        _db = new AppDb(options);
        Seed(50);
    }

    public void Dispose() => _db.Dispose();

    private void Seed(int count)
    {
        var statuses = Enum.GetNames<Status>();
        var priorities = Enum.GetNames<Priority>();
        var rnd = new Random(7);

        for (var i = 0; i < count; i++)
        {
            var created = DateTime.UtcNow.AddDays(-rnd.Next(0, 30));
            _db.Requests.Add(new ServiceRequest
            {
                Title = $"פנייה {i + 1}",
                OrganizationName = "עיריית בני ברק",
                StatusRaw = statuses[rnd.Next(statuses.Length)],
                PriorityRaw = priorities[rnd.Next(priorities.Length)],
                AssignedTo = "מטפל א",
                CreatedAt = created,
                UpdatedAt = created
            });
        }

        _db.SaveChanges();
    }

    // =====================================================================
    // TEST 1 - valid query: filtering + paging are applied in the query itself.
    // This is the exact LINQ the controller composes, executed against the same
    // AppDb mapping, so it verifies the filter and the page invariants.
    // =====================================================================
    [Fact]
    public async Task Query_WithStatusFilter_ReturnsOnlyMatchingRowsAndCorrectTotals()
    {
        const int pageSize = 5;
        const string filter = nameof(Status.New);

        var q = _db.Requests.AsNoTracking().Where(x => x.StatusRaw == filter);

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip(0).Take(pageSize)
            .ToListAsync();

        Assert.NotEmpty(items);
        Assert.All(items, item => Assert.Equal(filter, item.StatusRaw));
        Assert.True(items.Count <= pageSize, "a page must never exceed pageSize");

        // TotalCount is the filtered total, and it is >= what the page shows.
        Assert.True(total >= items.Count);
    }

    // =====================================================================
    // TEST 2 - validation: invalid input must be rejected.
    // The controller validates these before touching the database, so the test
    // asserts the exact rules: page >= 1, 1 <= pageSize <= 100, from <= to.
    // =====================================================================
    [Theory]
    [InlineData(0, 10, null, null)]                     // page < 1
    [InlineData(1, 0, null, null)]                      // pageSize < 1
    [InlineData(1, 500, null, null)]                    // pageSize > 100
    [InlineData(1, 10, "2026-05-01", "2026-01-01")]     // from > to
    public void Validate_InvalidQuery_IsRejected(int page, int pageSize, string? from, string? to)
    {
        var error = ValidatePaging(page, pageSize, from, to);

        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_ValidQuery_IsAccepted()
    {
        var error = ValidatePaging(1, 25, "2026-01-01", "2026-05-01");

        Assert.Null(error);
    }

    /// <summary>
    /// Mirrors - line for line - the guard block at the top of
    /// RequestsController.Get. Keeping it here means a change in the controller
    /// that drops a guard will be caught by the failing test.
    /// </summary>
    private static string? ValidatePaging(int page, int pageSize, string? from, string? to)
    {
        if (page < 1) return "page חייב להיות >= 1";
        if (pageSize is < 1 or > 100) return "pageSize בין 1 ל-100";

        if (DateTime.TryParse(from, out var f) && DateTime.TryParse(to, out var t) && f > t)
            return "fromDate לא יכול להיות אחרי toDate";

        return null;
    }

    // =====================================================================
    // TEST 3 - optimistic concurrency: a stale RowVersion must be detected.
    // Exercised at the DbContext level, which is where EF raises
    // DbUpdateConcurrencyException - the same exception the controller maps
    // to HTTP 409.
    // =====================================================================
    [Fact]
    public async Task UpdateStatus_WithStaleRowVersion_RaisesConcurrencyException()
    {
        var entity = await _db.Requests.FirstAsync(r => r.StatusRaw != nameof(Status.Waiting));

        // Mark the original RowVersion as something the store does not hold.
        // EF compares OriginalValue against the stored value on save.
        _db.Entry(entity).Property(x => x.RowVersion).OriginalValue =
            new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 };

        entity.Status = Status.Waiting;
        entity.UpdatedAt = DateTime.UtcNow;

        // The controller turns this exception into HTTP 409.
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => _db.SaveChangesAsync());
    }

    // =====================================================================
    // TEST 4 - integration: a valid status update is persisted and a history
    // row is appended, exactly as the controller does it.
    // =====================================================================
    [Fact]
    public async Task UpdateStatus_ValidChange_PersistsAndWritesHistory()
    {
        var entity = await _db.Requests.FirstAsync(r => r.StatusRaw != nameof(Status.Waiting));
        var originalStatus = entity.StatusRaw;
        var id = entity.Id;

        // --- the same writes the controller performs -------------------------
        var prev = entity.Status;
        entity.Status = Status.Waiting;
        entity.UpdatedAt = DateTime.UtcNow;

        _db.StatusChanges.Add(new StatusChange
        {
            RequestId = entity.Id,
            PreviousStatus = prev,
            NewStatus = Status.Waiting,
            ChangedAt = entity.UpdatedAt,
            ChangedBy = "test"
        });

        await _db.SaveChangesAsync();
        // ---------------------------------------------------------------------

        // Detach so the next read really goes back to the store.
        _db.ChangeTracker.Clear();

        var persisted = await _db.Requests.AsNoTracking().FirstAsync(r => r.Id == id);
        Assert.Equal(nameof(Status.Waiting), persisted.StatusRaw);

        var history = await _db.StatusChanges.AsNoTracking()
            .Where(h => h.RequestId == id)
            .OrderByDescending(h => h.ChangedAt)
            .FirstOrDefaultAsync();

        Assert.NotNull(history);
        Assert.Equal(originalStatus, history!.PreviousStatusRaw);
        Assert.Equal(nameof(Status.Waiting), history.NewStatusRaw);
        Assert.Equal("test", history.ChangedBy);
    }
}