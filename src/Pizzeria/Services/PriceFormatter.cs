using System.Globalization;

namespace Pizzeria.Services;

public static class PriceFormatter
{
    // GetCultureInfo returns a cached, read-only culture without the user's regional overrides,
    // so the output does not depend on the server or the current thread culture.
    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>Formats a price in Colombian pesos without decimals: 10500 becomes "$10.500".</summary>
    public static string ToCop(decimal price) => "$" + price.ToString("N0", Colombia);
}
