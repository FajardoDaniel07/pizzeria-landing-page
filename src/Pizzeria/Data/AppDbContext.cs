using Microsoft.EntityFrameworkCore;
using Pizzeria.Models;

namespace Pizzeria.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();

    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.Property(e => e.Name).IsRequired().HasMaxLength(80);
            entity.Property(e => e.Description).HasMaxLength(300);
            entity.Property(e => e.Price).HasPrecision(10, 2);
            // Stored as text so the column is readable in the database.
            entity.Property(e => e.Category).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.ImagePath).HasMaxLength(200);
            entity.Property(e => e.IsFeatured).HasDefaultValue(false);
            entity.HasIndex(e => e.Category);
        });

        modelBuilder.Entity<ContactMessage>(entity =>
        {
            entity.Property(e => e.Name).IsRequired().HasMaxLength(80);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Email).HasMaxLength(254);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(1000);
            // datetime2(0) on SQL Server: seconds are enough for a contact message.
            entity.Property(e => e.CreatedAtUtc).HasPrecision(0);
        });
    }
}
