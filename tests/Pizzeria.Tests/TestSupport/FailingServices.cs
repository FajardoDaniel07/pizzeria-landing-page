using Pizzeria.Data;
using Pizzeria.Models;
using Pizzeria.Services;

namespace Pizzeria.Tests.TestSupport;

/// <summary>Menu service that fails on every read, as if the database were down.</summary>
public sealed class FailingMenuService(AppDbContext context) : MenuService(context)
{
    /// <summary>Text carried by the exception; it must never reach the response.</summary>
    public const string FailureDetail = "Simulated failure: server sql-test-host is unreachable";

    public override Task<IReadOnlyList<MenuCategoryGroup>> GetMenuByCategoryAsync(
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(FailureDetail);

    public override Task<IReadOnlyList<MenuItem>> GetFeaturedAsync(CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(FailureDetail);

    public override Task<decimal?> GetMinPriceAsync(CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(FailureDetail);
}

/// <summary>Contact service that fails on save, as if the database were down.</summary>
public sealed class FailingContactService(AppDbContext context, TimeProvider timeProvider)
    : ContactService(context, timeProvider)
{
    /// <summary>Text carried by the exception; it must never reach the response.</summary>
    public const string FailureDetail = "Simulated failure: cannot insert into ContactMessages";

    public override Task SaveAsync(ContactInput input, CancellationToken cancellationToken = default) =>
        // The message repeats the submitted values on purpose: logging it would leak personal data.
        throw new InvalidOperationException($"{FailureDetail} ({input.Name}, {input.Phone}, {input.Email}, {input.Message})");
}
