using Pizzeria.Models;
using Pizzeria.Services;
using Pizzeria.Tests.TestSupport;

namespace Pizzeria.Tests;

/// <summary>Menu reads over SQLite in memory (RF-24, RF-10, RF-07).</summary>
public class MenuServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private static MenuItem Item(
        string name,
        decimal price = 10500m,
        MenuCategory category = MenuCategory.Pizza,
        bool isFeatured = false,
        string? description = null) =>
        new()
        {
            Name = name,
            Description = description,
            Price = price,
            Category = category,
            IsFeatured = isFeatured,
        };

    private async Task ArrangeAsync(params MenuItem[] items)
    {
        await using var context = _database.CreateContext();
        context.MenuItems.AddRange(items);
        await context.SaveChangesAsync();
    }

    private Task ArrangeSeedLikeMenuAsync() => ArrangeAsync(
        Item("Mexicana", isFeatured: true, description: "Example description"),
        Item("Champiñón con pollo"),
        Item("Carnes", isFeatured: true));

    // ---------- Grouped menu ----------

    // RF-24 / RF-10: one group per category, items ordered by name inside it.
    [Fact]
    public async Task GetMenuByCategoryAsync_OrdersItemsByNameInsideCategory_RF24()
    {
        await ArrangeSeedLikeMenuAsync();
        await using var context = _database.CreateContext();
        var service = new MenuService(context);

        IReadOnlyList<MenuCategoryGroup> groups = await service.GetMenuByCategoryAsync();

        var group = Assert.Single(groups);
        Assert.Equal(MenuCategory.Pizza, group.Category);
        Assert.Equal(["Carnes", "Champiñón con pollo", "Mexicana"], group.Items.Select(i => i.Name));
    }

    // RF-24 / RF-10: categories without products are not returned.
    [Fact]
    public async Task GetMenuByCategoryAsync_OmitsEmptyCategories_RF24()
    {
        await ArrangeAsync(
            Item("Carnes"),
            Item("Limonada", 4000m, MenuCategory.Drink));
        await using var context = _database.CreateContext();
        var service = new MenuService(context);

        var groups = await service.GetMenuByCategoryAsync();

        Assert.Equal([MenuCategory.Pizza, MenuCategory.Drink], groups.Select(g => g.Category));
        Assert.DoesNotContain(groups, g => g.Category is MenuCategory.Starter or MenuCategory.Dessert);
        Assert.All(groups, g => Assert.NotEmpty(g.Items));
    }

    // RF-10 / S-3: categories follow the enum order, not the alphabetical order of the stored text.
    [Fact]
    public async Task GetMenuByCategoryAsync_OrdersCategoriesByEnumOrder_RF10()
    {
        await ArrangeAsync(
            Item("Tiramisu", 8000m, MenuCategory.Dessert),
            Item("Limonada", 4000m, MenuCategory.Drink),
            Item("Palitos de ajo", 6000m, MenuCategory.Starter),
            Item("Carnes", 10500m, MenuCategory.Pizza));
        await using var context = _database.CreateContext();
        var service = new MenuService(context);

        var groups = await service.GetMenuByCategoryAsync();

        Assert.Equal(
            [MenuCategory.Pizza, MenuCategory.Starter, MenuCategory.Drink, MenuCategory.Dessert],
            groups.Select(g => g.Category));
    }

    // RF-24: each item lands in its own category group.
    [Fact]
    public async Task GetMenuByCategoryAsync_PutsEachItemInItsCategory_RF24()
    {
        await ArrangeAsync(
            Item("Mexicana"),
            Item("Limonada", 4000m, MenuCategory.Drink),
            Item("Carnes"),
            Item("Agua", 3000m, MenuCategory.Drink));
        await using var context = _database.CreateContext();
        var service = new MenuService(context);

        var groups = await service.GetMenuByCategoryAsync();

        Assert.Equal(["Carnes", "Mexicana"], groups.Single(g => g.Category == MenuCategory.Pizza).Items.Select(i => i.Name));
        Assert.Equal(["Agua", "Limonada"], groups.Single(g => g.Category == MenuCategory.Drink).Items.Select(i => i.Name));
    }

    // RF-10: each row needs name, price, description and the featured flag.
    [Fact]
    public async Task GetMenuByCategoryAsync_ReturnsItemData_RF10()
    {
        await ArrangeSeedLikeMenuAsync();
        await using var context = _database.CreateContext();
        var service = new MenuService(context);

        var items = (await service.GetMenuByCategoryAsync()).Single().Items;

        var mexicana = items.Single(i => i.Name == "Mexicana");
        Assert.Equal(10500m, mexicana.Price);
        Assert.Equal("Example description", mexicana.Description);
        Assert.True(mexicana.IsFeatured);
        Assert.Null(mexicana.ImagePath);

        var chicken = items.Single(i => i.Name == "Champiñón con pollo");
        Assert.Null(chicken.Description);
        Assert.False(chicken.IsFeatured);
    }

    // RF-24 (negative): an empty menu gives an empty list, not an error.
    [Fact]
    public async Task GetMenuByCategoryAsync_EmptyMenu_ReturnsNoGroups_RF24()
    {
        await using var context = _database.CreateContext();
        var service = new MenuService(context);

        var groups = await service.GetMenuByCategoryAsync();

        Assert.Empty(groups);
    }

    // ---------- Featured ----------

    // RF-24 / RF-09: only items with IsFeatured = true, ordered by name.
    [Fact]
    public async Task GetFeaturedAsync_ReturnsOnlyFeaturedItemsOrderedByName_RF24()
    {
        await ArrangeSeedLikeMenuAsync();
        await using var context = _database.CreateContext();
        var service = new MenuService(context);

        IReadOnlyList<MenuItem> featured = await service.GetFeaturedAsync();

        Assert.Equal(["Carnes", "Mexicana"], featured.Select(i => i.Name));
        Assert.All(featured, i => Assert.True(i.IsFeatured));
    }

    // RF-24 / S-8 (negative): no featured products gives an empty list.
    [Fact]
    public async Task GetFeaturedAsync_NoFeaturedItems_ReturnsEmpty_RF24()
    {
        await ArrangeAsync(Item("Carnes"), Item("Mexicana"));
        await using var context = _database.CreateContext();
        var service = new MenuService(context);

        var featured = await service.GetFeaturedAsync();

        Assert.Empty(featured);
    }

    // ---------- Minimum price ----------

    // RF-07 / RF-24: the lowest price of the whole menu (12000 and 9500 gives 9500).
    [Fact]
    public async Task GetMinPriceAsync_ReturnsLowestPrice_RF07()
    {
        await ArrangeAsync(Item("Carnes", 12000m), Item("Mexicana", 9500m));
        await using var context = _database.CreateContext();
        var service = new MenuService(context);

        decimal? minPrice = await service.GetMinPriceAsync();

        Assert.Equal(9500m, minPrice);
    }

    // RF-07: the minimum considers every category, not only pizzas.
    [Fact]
    public async Task GetMinPriceAsync_ConsidersAllCategories_RF07()
    {
        await ArrangeAsync(
            Item("Carnes", 10500m),
            Item("Tiramisu", 8000m, MenuCategory.Dessert),
            Item("Agua", 3000m, MenuCategory.Drink));
        await using var context = _database.CreateContext();
        var service = new MenuService(context);

        Assert.Equal(3000m, await service.GetMinPriceAsync());
    }

    // RF-07: with the seeded menu the sticker value is 10500.
    [Fact]
    public async Task GetMinPriceAsync_SeedLikeMenu_Returns10500_RF07()
    {
        await ArrangeSeedLikeMenuAsync();
        await using var context = _database.CreateContext();
        var service = new MenuService(context);

        Assert.Equal(10500m, await service.GetMinPriceAsync());
    }

    // RF-07 / RF-24 (negative): no products gives no value.
    [Fact]
    public async Task GetMinPriceAsync_EmptyMenu_ReturnsNull_RF24()
    {
        await using var context = _database.CreateContext();
        var service = new MenuService(context);

        Assert.Null(await service.GetMinPriceAsync());
    }

    // ---------- Read-only queries ----------

    // RF-24 / RNF-20: queries use AsNoTracking, so nothing ends up in the change tracker.
    [Fact]
    public async Task Queries_DoNotTrackEntities_RF24()
    {
        await ArrangeSeedLikeMenuAsync();
        await using var context = _database.CreateContext();
        var service = new MenuService(context);

        await service.GetMenuByCategoryAsync();
        await service.GetFeaturedAsync();
        await service.GetMinPriceAsync();

        Assert.Empty(context.ChangeTracker.Entries());
    }
}
