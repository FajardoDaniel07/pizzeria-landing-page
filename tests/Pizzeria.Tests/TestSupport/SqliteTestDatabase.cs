using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pizzeria.Data;

namespace Pizzeria.Tests.TestSupport;

/// <summary>
/// SQLite in-memory database shared by every context created from the same instance.
/// The database lives as long as the connection stays open (RNF-28: no SQL Server, no EF InMemory).
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    /// <param name="createSchema">
    /// False leaves the database without tables, which simulates an unusable database.
    /// </param>
    public SqliteTestDatabase(bool createSchema = true)
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        if (createSchema)
        {
            using var context = CreateContext();
            context.Database.EnsureCreated();
        }
    }

    /// <summary>Creates a fresh context (empty change tracker) over the shared connection.</summary>
    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new AppDbContext(options);
    }

    public void Dispose() => _connection.Dispose();
}
