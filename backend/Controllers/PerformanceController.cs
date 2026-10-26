using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Requests.Data;
using Requests.Models;

namespace Requests.Controllers;

/// <summary>Dev-only measurement endpoints. Not for production.</summary>
[ApiController]
[Route("api/perf")]
public class PerformanceController : ControllerBase
{
    private readonly AppDb _db;
    private readonly ILogger<PerformanceController> _logger;

    public PerformanceController(AppDb db, ILogger<PerformanceController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet("timings")]
    public async Task<IActionResult> Timings(int runs = 5, CancellationToken ct = default)
    {
        if (runs is < 1 or > 50) return BadRequest("runs בין 1 ל-50");

        _logger.LogInformation("[PERF] START runs={Runs}", runs);
        var results = new List<object>();

        var listTimes = new List<double>();
        for (var i = 0; i < runs; i++)
        {
            var sw = Stopwatch.StartNew();
            await _db.Requests.AsNoTracking()
                .Where(x => x.StatusRaw == "New")
                .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .Skip(2000).Take(25)
                .Select(x => new { x.Id, x.Title, x.StatusRaw })
                .ToListAsync(ct);
            sw.Stop();
            listTimes.Add(sw.Elapsed.TotalMilliseconds);
            _logger.LogDebug("[PERF] list run {Run}/{Runs}: {Ms} ms", i + 1, runs, Math.Round(sw.Elapsed.TotalMilliseconds, 2));
        }
        results.Add(new { operation = "GET /api/requests?status=New&page=81", medianMs = Median(listTimes) });
        _logger.LogInformation("[PERF] list median={Ms} ms", Median(listTimes));

        var aggTimes = new List<double>();
        for (var i = 0; i < runs; i++)
        {
            var sw = Stopwatch.StartNew();
            var total = await _db.Requests.CountAsync(ct);
            var byStatus = await _db.Requests.GroupBy(x => x.StatusRaw).Select(g => new { g.Key, C = g.Count() }).ToListAsync(ct);
            var byPriority = await _db.Requests.GroupBy(x => x.PriorityRaw).Select(g => new { g.Key, C = g.Count() }).ToListAsync(ct);
            sw.Stop();
            aggTimes.Add(sw.Elapsed.TotalMilliseconds);
            _logger.LogDebug("[PERF] agg run {Run}/{Runs}: {Ms} ms (total={Total}, statuses={Statuses}, priorities={Priorities})",
                i + 1, runs, Math.Round(sw.Elapsed.TotalMilliseconds, 2), total, byStatus.Count, byPriority.Count);
        }
        results.Add(new { operation = "GET /api/requests/aggregations (cache MISS)", medianMs = Median(aggTimes) });
        _logger.LogInformation("[PERF] aggregations median={Ms} ms", Median(aggTimes));

        var searchTimes = new List<double>();
        for (var i = 0; i < runs; i++)
        {
            var sw = Stopwatch.StartNew();
            var hits = await _db.Requests.AsNoTracking()
                .Where(x => x.Title.Contains("פנייה 999"))
                .CountAsync(ct);
            sw.Stop();
            searchTimes.Add(sw.Elapsed.TotalMilliseconds);
            _logger.LogDebug("[PERF] search run {Run}/{Runs}: {Ms} ms (hits={Hits})",
                i + 1, runs, Math.Round(sw.Elapsed.TotalMilliseconds, 2), hits);
        }
        results.Add(new { operation = "GET /api/requests?search=... (LIKE '%..%', no index)", medianMs = Median(searchTimes) });
        _logger.LogInformation("[PERF] search median={Ms} ms", Median(searchTimes));

        var rowCount = await _db.Requests.CountAsync(ct);
        _logger.LogInformation("[PERF] END rowCount={RowCount}", rowCount);

        return Ok(new { rowCount, runs, results });
    }

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;
        return Math.Round(sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2
            : sorted[mid], 2);
    }
}