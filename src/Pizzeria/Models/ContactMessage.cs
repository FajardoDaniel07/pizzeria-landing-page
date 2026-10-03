namespace Pizzeria.Models;

/// <summary>
/// Message sent from the contact form. Holds the minimum personal data:
/// no IP address and no user agent are stored.
/// </summary>
public class ContactMessage
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public required string Message { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
