using Requests.Extensions;
using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// One line per layer.
builder.Services
    .AddRequestsData(builder.Configuration.GetConnectionString("DefaultConnection")!)
    .AddRequestsCaching(builder.Configuration)
    .AddRequestsServices()
    .AddRequestsCors();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<Requests.Data.AppDb>().Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();
app.Run();

// Exposes the entry point to WebApplicationFactory in the test project.
public partial class Program { }

namespace Requests.Extensions
{
    /// <summary>
    /// All DI registration in one place. One line per layer.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddRequestsData(
            this IServiceCollection services, string connectionString)
        {
            // Scoped: one DbContext per HTTP request.
            services.AddDbContext<Requests.Data.AppDb>(options => options.UseSqlServer(connectionString));
            return services;
        }

        public static IServiceCollection AddRequestsCaching(
            this IServiceCollection services, IConfiguration configuration)
        {
            services.AddMemoryCache();
            services.Configure<Requests.Services.CacheSettings>(configuration.GetSection("Cache"));

            // Singleton: the cache entry and the rebuild gate outlive a request.
            services.AddSingleton<Requests.Services.IAggregationsCache, Requests.Services.MemoryAggregationsCache>();
            return services;
        }

        public static IServiceCollection AddRequestsServices(this IServiceCollection services)
        {
            // Scoped: depends on AppDb, which is Scoped.
            services.AddScoped<Requests.Services.IRequestService, Requests.Services.RequestService>();
            return services;
        }

        public static IServiceCollection AddRequestsCors(this IServiceCollection services)
        {
            services.AddCors(o => o.AddDefaultPolicy(p => p
                .WithOrigins("http://localhost:4200", "https://localhost:4200")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .WithExposedHeaders("X-Cache")));
            return services;
        }
    }
}