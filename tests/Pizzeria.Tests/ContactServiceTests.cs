using Microsoft.EntityFrameworkCore;
using Pizzeria.Models;
using Pizzeria.Services;
using Pizzeria.Tests.TestSupport;

namespace Pizzeria.Tests;

/// <summary>Saving contact messages over SQLite in memory (RF-25).</summary>
public class ContactServiceTests : IDisposable
{
    private static readonly DateTimeOffset FixedNow = new(2026, 10, 3, 15, 0, 0, TimeSpan.Zero);

    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private static ContactInput Build(
        string name = "Ana",
        string? phone = "311 370 6576",
        string? email = null,
        string message = "Quiero reservar",
        string? website = null) =>
        new()
        {
            Name = name,
            Phone = phone,
            Email = email,
            Message = message,
            Website = website,
        };

    private async Task SaveAsync(ContactInput input, TimeProvider? timeProvider = null)
    {
        await using var context = _database.CreateContext();
        var service = new ContactService(context, timeProvider ?? new FixedTimeProvider(FixedNow));
        await service.SaveAsync(input);
    }

    private async Task<List<ContactMessage>> ReadAllAsync()
    {
        await using var context = _database.CreateContext();
        return await context.ContactMessages.AsNoTracking().OrderBy(m => m.Id).ToListAsync();
    }

    // RF-25: a valid ContactInput becomes exactly one ContactMessage row.
    [Fact]
    public async Task SaveAsync_ValidInput_StoresOneRowWithMappedValues_RF25()
    {
        await SaveAsync(Build(name: "Ana", phone: "311 370 6576", email: "ana@example.com", message: "Quiero reservar"));

        var saved = Assert.Single(await ReadAllAsync());
        Assert.True(saved.Id > 0);
        Assert.Equal("Ana", saved.Name);
        Assert.Equal("311 370 6576", saved.Phone);
        Assert.Equal("ana@example.com", saved.Email);
        Assert.Equal("Quiero reservar", saved.Message);
    }

    // RF-25: each call stores its own row.
    [Fact]
    public async Task SaveAsync_CalledTwice_StoresTwoRows_RF25()
    {
        await SaveAsync(Build(name: "Ana"));
        await SaveAsync(Build(name: "Luis"));

        var saved = await ReadAllAsync();
        Assert.Equal(["Ana", "Luis"], saved.Select(m => m.Name));
    }

    // RF-25: CreatedAtUtc comes from the injected TimeProvider (2026-10-03T15:00:00Z).
    [Fact]
    public async Task SaveAsync_TakesCreatedAtUtcFromTimeProvider_RF25()
    {
        await SaveAsync(Build(), new FixedTimeProvider(FixedNow));

        var saved = Assert.Single(await ReadAllAsync());
        Assert.Equal(new DateTime(2026, 10, 3, 15, 0, 0), saved.CreatedAtUtc);
    }

    // RF-25: the value assigned to the entity has Kind = Utc.
    // Checked on the tracked entity: the Kind is not preserved when reading back from the database.
    [Fact]
    public async Task SaveAsync_AssignsCreatedAtUtcWithUtcKind_RF25()
    {
        await using var context = _database.CreateContext();
        var service = new ContactService(context, new FixedTimeProvider(FixedNow));

        await service.SaveAsync(Build());

        var entity = Assert.Single(context.ChangeTracker.Entries<ContactMessage>()).Entity;
        Assert.Equal(DateTimeKind.Utc, entity.CreatedAtUtc.Kind);
        Assert.Equal(FixedNow.UtcDateTime, entity.CreatedAtUtc);
    }

    // RF-25 (negative): the clock is the injected one, not the system clock.
    [Fact]
    public async Task SaveAsync_DoesNotUseSystemClock_RF25()
    {
        var past = new DateTimeOffset(2001, 2, 3, 4, 5, 6, TimeSpan.Zero);

        await SaveAsync(Build(), new FixedTimeProvider(past));

        var saved = Assert.Single(await ReadAllAsync());
        Assert.Equal(past.UtcDateTime, saved.CreatedAtUtc);
    }

    // RF-25: a clock with a non-UTC offset is still stored as the UTC instant.
    [Fact]
    public async Task SaveAsync_ConvertsClockOffsetToUtc_RF25()
    {
        // 10:00 at UTC-5 (Medellin) is 15:00 UTC.
        var medellin = new DateTimeOffset(2026, 10, 3, 10, 0, 0, TimeSpan.FromHours(-5));

        await SaveAsync(Build(), new FixedTimeProvider(medellin));

        var saved = Assert.Single(await ReadAllAsync());
        Assert.Equal(new DateTime(2026, 10, 3, 15, 0, 0), saved.CreatedAtUtc);
    }

    // RF-25 / S-4: values are trimmed when saving.
    [Fact]
    public async Task SaveAsync_TrimsAllValues_RF25()
    {
        await SaveAsync(Build(
            name: "  Ana  ",
            phone: "  311 370 6576 ",
            email: " ana@example.com  ",
            message: "\t Quiero reservar \n"));

        var saved = Assert.Single(await ReadAllAsync());
        Assert.Equal("Ana", saved.Name);
        Assert.Equal("311 370 6576", saved.Phone);
        Assert.Equal("ana@example.com", saved.Email);
        Assert.Equal("Quiero reservar", saved.Message);
    }

    // RF-25: inner whitespace is content, only the ends are trimmed.
    [Fact]
    public async Task SaveAsync_KeepsInnerWhitespace_RF25()
    {
        await SaveAsync(Build(name: " Ana María ", message: " Mesa para dos,\nsábado 8 p. m. "));

        var saved = Assert.Single(await ReadAllAsync());
        Assert.Equal("Ana María", saved.Name);
        Assert.Equal("Mesa para dos,\nsábado 8 p. m.", saved.Message);
    }

    // RF-25: empty or whitespace-only Phone is stored as null.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SaveAsync_EmptyPhone_IsStoredAsNull_RF25(string? phone)
    {
        await SaveAsync(Build(phone: phone, email: "ana@example.com"));

        var saved = Assert.Single(await ReadAllAsync());
        Assert.Null(saved.Phone);
        Assert.Equal("ana@example.com", saved.Email);
    }

    // RF-25: empty or whitespace-only Email is stored as null.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SaveAsync_EmptyEmail_IsStoredAsNull_RF25(string? email)
    {
        await SaveAsync(Build(phone: "3113706576", email: email));

        var saved = Assert.Single(await ReadAllAsync());
        Assert.Null(saved.Email);
        Assert.Equal("3113706576", saved.Phone);
    }

    // RF-25 / RNF-04: the honeypot value is not stored anywhere in the row.
    [Fact]
    public async Task SaveAsync_DoesNotStoreWebsite_RF25()
    {
        const string honeypot = "https://spam.example";

        await SaveAsync(Build(email: "ana@example.com", website: honeypot));

        var saved = Assert.Single(await ReadAllAsync());
        Assert.DoesNotContain(honeypot, saved.Name);
        Assert.DoesNotContain(honeypot, saved.Message);
        Assert.NotEqual(honeypot, saved.Phone);
        Assert.NotEqual(honeypot, saved.Email);
    }

    // RF-25 / RF-21: the entity has no place to store the honeypot.
    [Fact]
    public void ContactMessage_HasNoWebsiteProperty_RF25()
    {
        Assert.Null(typeof(ContactMessage).GetProperty("Website"));
    }
}
