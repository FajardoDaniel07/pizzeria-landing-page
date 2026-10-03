using Microsoft.EntityFrameworkCore;
using Pizzeria.Models;

namespace Pizzeria.Data;

/// <summary>
/// Sample menu for local development. The caller decides when to run it
/// (Program.cs runs it only in Development); sample data must never reach production.
/// </summary>
public static class DbSeeder
{
    /// <summary>
    /// Inserts the sample menu when the MenuItems table is empty (idempotent).
    /// If the database cannot be used, logs a warning and returns so the app can start anyway.
    /// </summary>
    public static async Task SeedAsync(AppDbContext context, ILogger logger)
    {
        try
        {
            if (await context.MenuItems.AnyAsync())
            {
                return;
            }

            context.MenuItems.AddRange(
                new MenuItem
                {
                    Name = "Carnes",
                    Description = "Carnes variadas sobre queso mozzarella y salsa de tomate.", // EJEMPLO
                    Price = 10500m,
                    Category = MenuCategory.Pizza,
                    IsFeatured = true,
                },
                new MenuItem
                {
                    Name = "Champiñón con pollo",
                    Description = "Champiñones y pollo sobre queso mozzarella.", // EJEMPLO
                    Price = 10500m,
                    Category = MenuCategory.Pizza,
                },
                new MenuItem
                {
                    Name = "Mexicana",
                    Description = "Carne molida, jalapeños y maíz sobre queso mozzarella.", // EJEMPLO
                    Price = 10500m,
                    Category = MenuCategory.Pizza,
                    IsFeatured = true,
                });

            await context.SaveChangesAsync();
            logger.LogInformation("Sample menu seeded.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Only the exception type is logged: provider messages may carry server details.
            logger.LogWarning(
                "Sample menu was not seeded because the database is not available ({ExceptionType}).",
                ex.GetType().Name);
        }
    }
}
