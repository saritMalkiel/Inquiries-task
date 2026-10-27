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
    private readonly IDevelopmentSeedService _seedService;
    private readonly IAggregationsCache _cache;
    private readonly ILogger<DevController> _logger;

    public DevController(IDevelopmentSeedService seedService, IAggregationsCache cache, ILogger<DevController> logger)
    {
        _seedService = seedService;
        _cache = cache;
        _logger = logger;
    }

    [HttpPost("seed")]
    public async Task<IActionResult> Seed(int count = 100000, bool reset = false, CancellationToken ct = default)
    {
        var created = await _seedService.SeedAsync(count, reset, ct);
        _logger.LogInformation("[SEED] endpoint completed created={Count}", created);
        return Ok(new { created });
    }

    [HttpPost("reset-cache")]
    public IActionResult ResetCache()
    {
        _cache.Invalidate();
        _logger.LogInformation("[CACHE] invalidated manually");
        return Ok(new { cleared = true });
    }
}