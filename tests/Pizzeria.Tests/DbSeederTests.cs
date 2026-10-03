using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pizzeria.Data;
using Pizzeria.Models;
using Pizzeria.Tests.TestSupport;

namespace Pizzeria.Tests;

/// <summary>
/// Sample menu data (RF-23). The "only in Development" rule belongs to Program.cs and is
/// verified with the integration tests / manually; here the seeder itself is exercised.
/// </summary>
public class DbSeederTests
{
    private static async Task<List<MenuItem>> ReadMenuAsync(SqliteTestDatabase database)
    {
        await using var context = database.CreateContext();
        return await context.MenuItems.AsNoTracking().ToListAsync();
    }

    private static async Task SeedAsync(SqliteTestDatabase database, ListLogger? logger = null)
    {
        await using var context = database.CreateContext();
        await DbSeeder.SeedAsync(context, logger ?? new ListLogger());
    }

    // RF-23: empty table -> exactly the 3 pizzas of the brief.
    [Fact]
    public async Task SeedAsync_EmptyTable_InsertsTheThreePizzas_RF23()
    {
        using var database = new SqliteTestDatabase();

        await SeedAsync(database);

        var items = await ReadMenuAsync(database);
        Assert.Equal(
            ["Carnes", "Champiñón con pollo", "Mexicana"],
            items.Select(i => i.Name).Order(StringComparer.Ordinal));
    }

    // RF-23: every pizza costs 10500, is in the Pizza category and has no image.
    [Fact]
    public async Task SeedAsync_AllItems_HavePrice10500CategoryPizzaAndNoImage_RF23()
    {
        using var database = new SqliteTestDatabase();

        await SeedAsync(database);

        var items = await ReadMenuAsync(database);
        Assert.Equal(3, items.Count);
        Assert.All(items, item =>
        {
            Assert.Equal(10500m, item.Price);
            Assert.Equal(MenuCategory.Pizza, item.Category);
            Assert.Null(item.ImagePath);
        });
    }

    // RF-23 / RF-09: "Carnes" and "Mexicana" are featured; "Champiñón con pollo" is not.
    [Theory]
    [InlineData("Carnes", true)]
    [InlineData("Mexicana", true)]
    [InlineData("Champiñón con pollo", false)]
    public async Task SeedAsync_FeaturedFlag_RF23(string name, bool expectedFeatured)
    {
        using var database = new SqliteTestDatabase();

        await SeedAsync(database);

        var item = Assert.Single(await ReadMenuAsync(database), i => i.Name == name);
        Assert.Equal(expectedFeatured, item.IsFeatured);
    }

    // RF-23 / RF-20: each pizza has a one-sentence description that fits the column (300).
    [Fact]
    public async Task SeedAsync_EveryItem_HasShortDescription_RF23()
    {
        using var database = new SqliteTestDatabase();

        await SeedAsync(database);

        Assert.All(await ReadMenuAsync(database), item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Description));
            Assert.InRange(item.Description!.Length, 1, 300);
            Assert.InRange(item.Name.Length, 1, 80);
        });
    }

    // RF-23: idempotent, running it twice leaves 3 rows.
    [Fact]
    public async Task SeedAsync_RunTwice_LeavesThreeRows_RF23()
    {
        using var database = new SqliteTestDatabase();

        await SeedAsync(database);
        await SeedAsync(database);

        Assert.Equal(3, (await ReadMenuAsync(database)).Count);
    }

    // RF-23 (negative): with a non-empty table nothing is inserted.
    [Fact]
    public async Task SeedAsync_TableNotEmpty_InsertsNothing_RF23()
    {
        using var database = new SqliteTestDatabase();
        await using (var context = database.CreateContext())
        {
            context.MenuItems.Add(new MenuItem
            {
                Name = "Hawaiana",
                Price = 12000m,
                Category = MenuCategory.Pizza,
            });
            await context.SaveChangesAsync();
        }

        await SeedAsync(database);

        var item = Assert.Single(await ReadMenuAsync(database));
        Assert.Equal("Hawaiana", item.Name);
        Assert.Equal(12000m, item.Price);
    }

    // RF-23: the seeder never touches contact messages.
    [Fact]
    public async Task SeedAsync_DoesNotInsertContactMessages_RF23()
    {
        using var database = new SqliteTestDatabase();

        await SeedAsync(database);

        await using var context = database.CreateContext();
        Assert.Equal(0, await context.ContactMessages.CountAsync());
    }

    // RF-23: a normal run logs no warnings or errors.
    [Fact]
    public async Task SeedAsync_SuccessfulRun_LogsNoWarnings_RF23()
    {
        using var database = new SqliteTestDatabase();
        var logger = new ListLogger();

        await SeedAsync(database, logger);

        Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Warning);
    }

    // RF-23 (negative): database not usable -> no exception, one Warning in the log.
    [Fact]
    public async Task SeedAsync_DatabaseUnavailable_LogsWarningAndDoesNotThrow_RF23()
    {
        // No schema: every query fails, like a database that does not respond.
        using var database = new SqliteTestDatabase(createSchema: false);
        var logger = new ListLogger();

        var exception = await Record.ExceptionAsync(() => SeedAsync(database, logger));

        Assert.Null(exception);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.DoesNotContain(logger.Entries, e => e.Level > LogLevel.Warning);
    }
}
