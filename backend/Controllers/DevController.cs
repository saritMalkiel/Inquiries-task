using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Requests.Data;
using Requests.Models;
using Requests.Services;

namespace Requests.Controllers;

[ApiController]
[Route("api/dev")]
public class DevController : ControllerBase
{
    private readonly AppDb _db;
    private readonly IAggregationsCache _cache;
    private readonly ILogger<DevController> _logger;

    public DevController(AppDb db, IAggregationsCache cache, ILogger<DevController> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    [HttpPost("seed")]
    public async Task<IActionResult> Seed(int count = 100000, bool reset = false, CancellationToken ct = default)
    {
        _logger.LogInformation("[SEED] START count={Count} reset={Reset}", count, reset);

        if (reset)
        {
            _logger.LogInformation("[SEED] reset: deleting StatusChanges + Requests");
            var deletedHistory = await _db.StatusChanges.ExecuteDeleteAsync(ct);
            var deletedRequests = await _db.Requests.ExecuteDeleteAsync(ct);
            _logger.LogInformation("[SEED] reset done: history={History} requests={Requests}", deletedHistory, deletedRequests);
        }

        var orgs = new[] { "עיריית בני ברק", "עיריית ירושלים", "מוסדות החינוך", "קופת חולים" };

        // Seed the enum NAMES: the columns are nvarchar.
        var statuses = Enum.GetNames<Status>();
        var priorities = Enum.GetNames<Priority>();
        var rnd = new Random(42);

        for (int i = 0; i < count; i += 5000)
        {
            var size = Math.Min(5000, count - i);
            for (int j = 0; j < size; j++)
            {
                var created = DateTime.UtcNow.AddDays(-rnd.Next(0, 365));
                _db.Requests.Add(new ServiceRequest
                {
                    Title = $"פנייה {i + j + 1}",
                    OrganizationName = orgs[rnd.Next(orgs.Length)],
                    StatusRaw = statuses[rnd.Next(statuses.Length)],
                    PriorityRaw = priorities[rnd.Next(priorities.Length)],
                    AssignedTo = "מטפל " + (char)('א' + rnd.Next(6)),
                    CreatedAt = created,
                    UpdatedAt = created
                });
            }
            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();

            _logger.LogInformation("[SEED] batch saved: {Done}/{Total}", i + size, count);
        }

        // Seed bypasses the API's write paths, so the cache must be cleared.
        _cache.Invalidate();
        _logger.LogInformation("[SEED] END count={Count} cache=invalidated", count);

        return Ok(new { created = count });
    }

    [HttpPost("reset-cache")]
    public IActionResult ResetCache()
    {
        _cache.Invalidate();
        _logger.LogInformation("[CACHE] invalidated manually");
        return Ok(new { cleared = true });
    }
}