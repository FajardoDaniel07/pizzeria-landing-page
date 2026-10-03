using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Pizzeria.Data;

namespace Pizzeria.Tests.TestSupport;

/// <summary>
/// Hosts the real app for integration tests. <see cref="AppDbContext"/> runs on a SQLite
/// in-memory database owned by this factory (no SQL Server, no user-secrets, no network) and
/// the connection string is a fake value supplied through test configuration.
/// The environment is "Testing", so the startup seeder does not run: call
/// <see cref="SeedMenuAsync"/> when a test needs the sample menu.
/// Each instance is isolated (own database, own host), so per-instance state such as rate
/// limiting does not leak between tests.
/// </summary>
public sealed class PizzeriaWebFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly Dictionary<string, string?> _settings = new()
    {
        // Fake value: never used to connect, it only satisfies the startup check.
        ["ConnectionStrings:Default"] = "Server=test.invalid;Database=PizzeriaTests",
    };
    private readonly List<Action<IServiceCollection>> _serviceOverrides = [];

    public PizzeriaWebFactory()
    {
        _connection.Open();

        using var context = CreateDbContext();
        context.Database.EnsureCreated();
    }

    /// <summary>Overrides a configuration value. Call it before the first client is created.</summary>
    public PizzeriaWebFactory WithSetting(string key, string? value)
    {
        _settings[key] = value;
        return this;
    }

    /// <summary>
    /// Replaces or adds services (for example a failing MenuService). Call it before the first client is created.
    /// </summary>
    public PizzeriaWebFactory WithServices(Action<IServiceCollection> configure)
    {
        _serviceOverrides.Add(configure);
        return this;
    }

    /// <summary>Creates a context over the shared database, to arrange data or assert on what was saved.</summary>
    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new AppDbContext(options);
    }

    /// <summary>Inserts the sample menu with the app's own seeder (three pizzas at 10500).</summary>
    public async Task SeedMenuAsync()
    {
        await using var context = CreateDbContext();
        await DbSeeder.SeedAsync(context, NullLogger.Instance);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(_settings));

        builder.ConfigureServices(services =>
        {
            // Drop the SQL Server registration, including the options configuration that
            // AddDbContext registers separately, so only the SQLite provider remains.
            var sqlServerRegistrations = services
                .Where(descriptor =>
                    descriptor.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    descriptor.ServiceType == typeof(IDbContextOptionsConfiguration<AppDbContext>))
                .ToList();

            foreach (var descriptor in sqlServerRegistrations)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            foreach (var configure in _serviceOverrides)
            {
                configure(services);
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
