namespace Pizzeria.Models;

public class MenuItem
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public MenuCategory Category { get; set; }

    /// <summary>Path relative to wwwroot; null means the placeholder image is used.</summary>
    public string? ImagePath { get; set; }

    public bool IsFeatured { get; set; }
}
