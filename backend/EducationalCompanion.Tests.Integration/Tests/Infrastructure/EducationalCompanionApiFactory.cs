using System.Linq;
using EducationalCompanion.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EducationalCompanion.Tests.Integration;

// Creates an in-memory API instance with SQLite (instead of PostgreSQL).
public sealed class EducationalCompanionApiFactory : WebApplicationFactory<Program>, IDisposable
{
    private readonly SqliteConnection _connection;

    public EducationalCompanionApiFactory()
    {
        // Keep the connection open for the lifetime of the factory.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting(WebHostDefaults.DetailedErrorsKey, "true");

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Debug);
        });

        builder.ConfigureServices(services =>
        {
            // Replace ApplicationDbContext registration.
            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(ApplicationDbContext) ||
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>) &&
                     d.ServiceType.GenericTypeArguments[0] == typeof(ApplicationDbContext)) ||
                    // EF may also register IDbContextOptions<TContext> depending on version
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.GetGenericTypeDefinition().Name.StartsWith("IDbContextOptions`1") &&
                     d.ServiceType.GenericTypeArguments[0] == typeof(ApplicationDbContext)))
                .ToList();
            foreach (var d in toRemove)
                services.Remove(d);

            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));

            // Ensure schema exists (no migrations/seed in Testing).
            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();
        });
    }

    public void ResetDatabase()
    {
        using var sp = Services.CreateScope();
        var db = sp.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}

