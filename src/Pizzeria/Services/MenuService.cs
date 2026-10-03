using Microsoft.EntityFrameworkCore;
using Pizzeria.Data;
using Pizzeria.Models;

namespace Pizzeria.Services;

/// <summary>Products of one category, ordered by name.</summary>
public sealed record MenuCategoryGroup(MenuCategory Category, IReadOnlyList<MenuItem> Items);

/// <summary>Read-only access to the menu.</summary>
public class MenuService(AppDbContext context)
{
    /// <summary>
    /// Menu grouped by category. Categories follow the enum order and those without products are omitted.
    /// </summary>
    public virtual async Task<IReadOnlyList<MenuCategoryGroup>> GetMenuByCategoryAsync(
        CancellationToken cancellationToken = default)
    {
        var items = await context.MenuItems
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        // Category is stored as text, so the enum order is applied in memory (S-3).
        return items
            .GroupBy(item => item.Category)
            .OrderBy(group => group.Key)
            .Select(group => new MenuCategoryGroup(group.Key, group.ToList()))
            .ToList();
    }

    public virtual async Task<IReadOnlyList<MenuItem>> GetFeaturedAsync(
        CancellationToken cancellationToken = default)
    {
        return await context.MenuItems
            .AsNoTracking()
            .Where(item => item.IsFeatured)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Lowest price of the whole menu, or null when there are no products.</summary>
    public virtual async Task<decimal?> GetMinPriceAsync(CancellationToken cancellationToken = default)
    {
        // The minimum is computed in memory: SQLite (used by the tests) cannot aggregate decimal.
        var prices = await context.MenuItems
            .AsNoTracking()
            .Select(item => item.Price)
            .ToListAsync(cancellationToken);

        return prices.Count == 0 ? null : prices.Min();
    }
}
