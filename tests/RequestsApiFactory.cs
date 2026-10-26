using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Requests;          // Program
using Requests.Data;     // AppDb
using Requests.Models;   // Status, Priority, ServiceRequest

namespace Tests;

/// <summary>
/// Boots the real application (real controllers, real routing, real DI) but
/// replaces the SQL Server provider with the EF Core in-memory provider, so the
/// integration tests run without a database server and without touching the
/// developer's RequestsDb.
/// </summary>
public class RequestsApiFactory : WebApplicationFactory<Program>
{
    // Unique per factory instance so parallel test classes never share state.
    private readonly string _dbName = "tests-" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove the SQL Server registration added by Program.cs.
            services.RemoveAll<DbContextOptions<AppDb>>();
            services.RemoveAll<AppDb>();

            services.AddDbContext<AppDb>(options =>
                options.UseInMemoryDatabase(_dbName));
        });
    }

    /// <summary>Seeds a small, deterministic data set through the same DbContext.</summary>
    public void Seed(int count = 50)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();

        var statuses = Enum.GetNames<Status>();
        var priorities = Enum.GetNames<Priority>();
        var rnd = new Random(7);

        for (var i = 0; i < count; i++)
        {
            var created = DateTime.UtcNow.AddDays(-rnd.Next(0, 30));
            db.Requests.Add(new ServiceRequest
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

        db.SaveChanges();
    }
}