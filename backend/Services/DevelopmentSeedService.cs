using Microsoft.EntityFrameworkCore;
using Requests.Data;
using Requests.Models;

namespace Requests.Services;

public interface IDevelopmentSeedService
{
    Task<int> SeedAsync(int count = 100000, bool reset = false, CancellationToken ct = default);
    Task<int> SeedIfEmptyAsync(CancellationToken ct = default);
}

public class DevelopmentSeedService : IDevelopmentSeedService
{
    private const int BatchSize = 5000;

    private readonly AppDb _db;
    private readonly IAggregationsCache _cache;
    private readonly ILogger<DevelopmentSeedService> _logger;

    public DevelopmentSeedService(AppDb db, IAggregationsCache cache, ILogger<DevelopmentSeedService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<int> SeedIfEmptyAsync(CancellationToken ct = default)
    {
        var hasRows = await _db.Requests.AnyAsync(ct);
        if (hasRows)
        {
            _logger.LogInformation("[SEED] skipping startup seeding because the database is not empty");
            return 0;
        }

        return await SeedAsync(100000, reset: false, ct);
    }

    public async Task<int> SeedAsync(int count = 100000, bool reset = false, CancellationToken ct = default)
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
        var statuses = Enum.GetNames<Status>();
        var priorities = Enum.GetNames<Priority>();
        var rnd = new Random(42);

        var totalInserted = 0;
        var batch = new List<ServiceRequest>();

        for (var i = 0; i < count; i++)
        {
            var created = DateTime.UtcNow.AddDays(-rnd.Next(0, 365));
            batch.Add(new ServiceRequest
            {
                Title = $"פנייה {i + 1}",
                OrganizationName = orgs[rnd.Next(orgs.Length)],
                StatusRaw = statuses[rnd.Next(statuses.Length)],
                PriorityRaw = priorities[rnd.Next(priorities.Length)],
                AssignedTo = "מטפל " + (char)('א' + rnd.Next(6)),
                CreatedAt = created,
                UpdatedAt = created
            });

            if (batch.Count == BatchSize)
            {
                _db.Requests.AddRange(batch);
                await _db.SaveChangesAsync(ct);
                _db.ChangeTracker.Clear();
                totalInserted += batch.Count;
                _logger.LogInformation("[SEED] batch saved: {Done}/{Total}", totalInserted, count);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            _db.Requests.AddRange(batch);
            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();
            totalInserted += batch.Count;
            _logger.LogInformation("[SEED] batch saved: {Done}/{Total}", totalInserted, count);
        }

        _cache.Invalidate();
        _logger.LogInformation("[SEED] END count={Count} cache=invalidated", totalInserted);
        return totalInserted;
    }
}
