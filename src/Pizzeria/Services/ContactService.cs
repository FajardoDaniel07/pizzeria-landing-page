using Pizzeria.Data;
using Pizzeria.Models;

namespace Pizzeria.Services;

/// <summary>Stores contact form messages.</summary>
public class ContactService(AppDbContext context, TimeProvider timeProvider)
{
    /// <summary>
    /// Saves an already validated input. Values are trimmed, empty optional values become null
    /// and the honeypot field is ignored.
    /// </summary>
    public virtual async Task SaveAsync(ContactInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var message = new ContactMessage
        {
            Name = input.Name.Trim(),
            Phone = TrimToNull(input.Phone),
            Email = TrimToNull(input.Email),
            Message = input.Message.Trim(),
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
        };

        context.ContactMessages.Add(message);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
