using Pizzeria.Models;

namespace Pizzeria.Services;

/// <summary>Visible (Spanish) names of the menu categories.</summary>
public static class MenuCategoryNames
{
    /// <summary>Returns the heading shown for a category, for example "Pizzas" for <see cref="MenuCategory.Pizza"/>.</summary>
    /// <param name="category">Category to name.</param>
    /// <returns>The plural Spanish name of the category.</returns>
    public static string ToDisplayName(this MenuCategory category) => category switch
    {
        MenuCategory.Pizza => "Pizzas",
        MenuCategory.Starter => "Entradas",
        MenuCategory.Drink => "Bebidas",
        MenuCategory.Dessert => "Postres",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown menu category."),
    };
}
