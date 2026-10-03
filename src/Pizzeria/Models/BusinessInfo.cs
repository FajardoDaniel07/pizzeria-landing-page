namespace Pizzeria.Models;

/// <summary>
/// Public business data shown on the site, bound from the <c>Business</c> configuration section.
/// None of these values are secrets.
/// </summary>
public sealed class BusinessInfo
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "Business";

    /// <summary>Business name as shown in the header, hero, footer and page title.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Legal name of the party responsible for personal data, shown in the privacy notice.
    /// Empty until the owner provides it; the notice then names only <see cref="Name"/>.
    /// </summary>
    public string LegalName { get; set; } = string.Empty;

    /// <summary>True when a legal name is configured.</summary>
    public bool HasLegalName => !string.IsNullOrWhiteSpace(LegalName);

    /// <summary>Local phone number, digits only (for example <c>3113706576</c>).</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>WhatsApp number with country code and no plus sign (for example <c>573113706576</c>).</summary>
    public string WhatsApp { get; set; } = string.Empty;

    /// <summary>Text prefilled in the WhatsApp chat.</summary>
    public string WhatsAppMessage { get; set; } = string.Empty;

    /// <summary>City and region, always shown.</summary>
    public string City { get; set; } = string.Empty;

    /// <summary>Street address. Empty hides the address line and the directions link.</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>Opening hours, one entry per row of the hours table.</summary>
    public List<BusinessHours> Hours { get; set; } = [];

    /// <summary>Social network links. Empty hides them in the footer.</summary>
    public List<SocialLink> Social { get; set; } = [];

    /// <summary>True when a street address is configured.</summary>
    public bool HasAddress => !string.IsNullOrWhiteSpace(Address);

    /// <summary>WhatsApp chat link with the prefilled message URL-encoded.</summary>
    public string WhatsAppUrl =>
        $"https://wa.me/{Uri.EscapeDataString(WhatsApp)}?text={Uri.EscapeDataString(WhatsAppMessage)}";

    /// <summary>Phone link for the <c>tel:</c> scheme, in international format.</summary>
    public string PhoneUrl => $"tel:+{Uri.EscapeDataString(WhatsApp)}";

    /// <summary>Phone grouped for reading (<c>311 370 6576</c>) when it has ten digits; otherwise unchanged.</summary>
    public string PhoneDisplay =>
        Phone.Length == 10 && Phone.All(char.IsAsciiDigit)
            ? $"{Phone[..3]} {Phone[3..6]} {Phone[6..]}"
            : Phone;

    /// <summary>Google Maps search link for the address (plain link, never an embedded map).</summary>
    public string MapsUrl =>
        $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString($"{Address}, {City}")}";
}

/// <summary>One row of the opening hours table.</summary>
public sealed class BusinessHours
{
    /// <summary>Days covered by the row (for example "Lunes a jueves").</summary>
    public string Days { get; set; } = string.Empty;

    /// <summary>Opening time range (for example "2:00 p. m. a 10:30 p. m.").</summary>
    public string Time { get; set; } = string.Empty;
}

/// <summary>A social network link shown in the footer.</summary>
public sealed class SocialLink
{
    /// <summary>Visible name of the network.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Absolute URL of the profile.</summary>
    public string Url { get; set; } = string.Empty;
}
