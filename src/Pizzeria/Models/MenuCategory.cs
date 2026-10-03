namespace Pizzeria.Models;

/// <summary>
/// Menu categories. The declaration order is the display order (S-3).
/// Stored as text, so every name must fit in nvarchar(20).
/// </summary>
public enum MenuCategory
{
    Pizza,
    Starter,
    Drink,
    Dessert,
}
