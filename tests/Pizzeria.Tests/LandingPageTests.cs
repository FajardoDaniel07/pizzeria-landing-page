using System.Net;
using System.Text.RegularExpressions;
using Pizzeria.Tests.TestSupport;

namespace Pizzeria.Tests;

/// <summary>
/// Integration tests for blocks 4b and 5: layout, business configuration and static sections.
/// Razor HTML-encodes configured values (the apostrophe, accented letters), so assertions on
/// text run against the decoded HTML.
/// </summary>
public class LandingPageTests
{
    private const string WhatsAppPrefix = "https://wa.me/573113706576?text=";

    private static async Task<string> GetHtmlAsync(PizzeriaWebFactory factory, string path = "/")
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static string Decode(string html) => WebUtility.HtmlDecode(html);

    private static List<string> Hrefs(string html) =>
        Regex.Matches(html, "href=\"([^\"]*)\"")
            .Select(match => WebUtility.HtmlDecode(match.Groups[1].Value))
            .ToList();

    [Fact]
    public async Task Get_Root_Returns200_InSpanish_WithBusinessName()
    {
        using var factory = new PizzeriaWebFactory();

        var html = Decode(await GetHtmlAsync(factory));

        Assert.Contains("<html lang=\"es\">", html);
        Assert.Matches(@"<title>[^<]*Harry's Pizza[^<]*</title>", html);
        Assert.Matches(@"<header[\s\S]*Harry's Pizza[\s\S]*</header>", html);
        Assert.Matches(@"<h1[^>]*>Harry's Pizza</h1>", html);
    }

    [Fact]
    public async Task Get_Root_HasNoTemplateLeftovers_NorInlineStylesOrScripts()
    {
        using var factory = new PizzeriaWebFactory();

        var html = await GetHtmlAsync(factory);

        Assert.DoesNotContain("bootstrap", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("jquery", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("importmap", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Pizzeria.styles.css", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<style", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" style=", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<iframe", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Welcome", html);
        Assert.DoesNotContain("Home", html);
        Assert.DoesNotContain("Privacy", html);
        // Static asset URLs carry a content fingerprint (favicon.<hash>.svg).
        Assert.Matches(@"<link rel=""icon"" href=""/favicon(\.[a-z0-9]+)?\.svg""", html);
        Assert.Contains("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">", html);
    }

    [Fact]
    public async Task Get_Root_LoadsNoThirdPartyResources()
    {
        using var factory = new PizzeriaWebFactory();

        var html = await GetHtmlAsync(factory);

        // Resources (link, img, script) must be same-origin: only navigation links may be external.
        var resourceUrls = Regex.Matches(html, "<(?:link|img|script)\\b[^>]*?\\b(?:href|src)=\"([^\"]*)\"")
            .Select(match => match.Groups[1].Value)
            .ToList();

        Assert.NotEmpty(resourceUrls);
        Assert.All(resourceUrls, url => Assert.StartsWith("/", url));
        Assert.All(resourceUrls, url => Assert.False(url.StartsWith("//", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Get_Root_PreloadsOnlyTheDisplayFont_AtTheSameUrlTheStylesheetUses()
    {
        using var factory = new PizzeriaWebFactory();
        using var client = factory.CreateClient();

        var html = await GetHtmlAsync(factory);
        var css = await client.GetStringAsync("/css/site.css");
        var preloads = Regex.Matches(html, @"<link rel=""preload"" href=""([^""]*)""[^>]*>").ToList();

        // A fingerprinted preload URL would not match the stylesheet's url() and the font would load twice.
        var preload = Assert.Single(preloads);
        Assert.Equal("/fonts/bowlby-one-latin-400-normal.woff2", preload.Groups[1].Value);
        Assert.Contains("as=\"font\" type=\"font/woff2\" crossorigin", preload.Value);
        Assert.Contains("url(\"../fonts/bowlby-one-latin-400-normal.woff2\")", css);
        Assert.DoesNotContain("http", css, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_Root_Header_HasAnchorsAndWhatsAppButton()
    {
        using var factory = new PizzeriaWebFactory();

        var html = Decode(await GetHtmlAsync(factory));
        var header = Regex.Match(html, @"<header[\s\S]*?</header>").Value;

        Assert.Matches(@"href=""/#menu""[^>]*>Menú</a>", header);
        Assert.Matches(@"href=""/#horarios""[^>]*>Horarios</a>", header);
        Assert.Matches(@"href=""/#contacto""[^>]*>Contacto</a>", header);
        Assert.Contains(WhatsAppPrefix, header);
        Assert.Contains("id=\"horarios\"", html);
    }

    [Fact]
    public async Task Get_Root_WhatsAppLinks_PointToBusinessNumber_WithEncodedMessage()
    {
        using var factory = new PizzeriaWebFactory();

        var html = await GetHtmlAsync(factory);
        var whatsAppLinks = Hrefs(html).Where(href => href.Contains("wa.me")).ToList();

        // Header button, hero button, footer link and the fixed mobile bar.
        Assert.Equal(4, whatsAppLinks.Count);
        Assert.All(whatsAppLinks, href =>
        {
            Assert.Contains("wa.me/573113706576", href);
            Assert.StartsWith(WhatsAppPrefix, href);

            var text = href[WhatsAppPrefix.Length..];
            Assert.DoesNotContain(" ", text);
            Assert.DoesNotContain(",", text);
            Assert.Equal("Hola, quiero hacer un pedido", Uri.UnescapeDataString(text));
        });
    }

    [Fact]
    public async Task Get_Root_Hero_HasBothCallsToAction_AndDecorativePizza()
    {
        using var factory = new PizzeriaWebFactory();

        var html = Decode(await GetHtmlAsync(factory));
        var hero = Regex.Match(html, @"<section class=""hero""[\s\S]*?</section>").Value;

        Assert.Matches(@"href=""https://wa\.me/573113706576\?text=[^""]+""[^>]*>Pedir por WhatsApp</a>", hero);
        Assert.Matches(@"href=""#contacto""[^>]*>Reservar</a>", hero);
        Assert.Matches(@"<img[^>]*src=""/img/pizza-placeholder(\.[a-z0-9]+)?\.svg""[^>]*alt=""""", hero);
        // The price sticker belongs to block 6.
        Assert.DoesNotContain("Desde", hero);
    }

    [Fact]
    public async Task Get_Root_MobileBar_LinksToWhatsApp()
    {
        using var factory = new PizzeriaWebFactory();

        var html = Decode(await GetHtmlAsync(factory));

        Assert.Matches(
            @"<div class=""wa-bar"">\s*<a[^>]*href=""https://wa\.me/573113706576\?text=[^""]+""[^>]*>Pedir por WhatsApp</a>",
            html);
    }

    [Fact]
    public async Task Get_Root_ShowsAboutSection()
    {
        using var factory = new PizzeriaWebFactory();

        var html = Decode(await GetHtmlAsync(factory));

        Assert.Matches(@"<h2[^>]*>Sobre nosotros</h2>", html);
    }

    [Fact]
    public async Task Get_Root_ShowsHoursFromConfiguration()
    {
        using var factory = new PizzeriaWebFactory();

        var html = Decode(await GetHtmlAsync(factory));

        Assert.Matches(@"<th[^>]*>Lunes a jueves</th>\s*<td[^>]*>2:00 p\. m\. a 10:30 p\. m\.</td>", html);
        Assert.Matches(@"<th[^>]*>Viernes a domingo</th>\s*<td[^>]*>12:00 m\. a 10:30 p\. m\.</td>", html);
        Assert.Equal(2, Regex.Matches(html, @"<tr\b").Count);
    }

    [Fact]
    public async Task Get_Root_HoursComeFromConfiguration_NotFromTheView()
    {
        using var factory = new PizzeriaWebFactory()
            .WithSetting("Business:Hours:0:Days", "Todos los días")
            .WithSetting("Business:Hours:0:Time", "1:00 p. m. a 9:00 p. m.");

        var html = Decode(await GetHtmlAsync(factory));

        Assert.Contains("Todos los días", html);
        Assert.Contains("1:00 p. m. a 9:00 p. m.", html);
        Assert.DoesNotContain("Lunes a jueves", html);
    }

    [Fact]
    public async Task Get_Root_WithoutAddress_ShowsCityOnly_AndNoDirectionsLink()
    {
        using var factory = new PizzeriaWebFactory();

        var html = Decode(await GetHtmlAsync(factory));

        Assert.Contains("Medellín, Antioquia", html);
        Assert.DoesNotContain("Cómo llegar", html);
        Assert.DoesNotContain("google.com/maps", html);
        Assert.DoesNotContain("<iframe", html);
    }

    [Fact]
    public async Task Get_Root_WithAddress_ShowsAddress_AndDirectionsLinkWithoutIframe()
    {
        using var factory = new PizzeriaWebFactory()
            .WithSetting("Business:Address", "Calle 10 # 20-30");

        var html = await GetHtmlAsync(factory);
        var decoded = Decode(html);

        Assert.Contains("Medellín, Antioquia", decoded);
        Assert.Contains("Calle 10 # 20-30", decoded);
        Assert.Matches(@"href=""https://www\.google\.com/maps/search/\?api=1&query=[^"" ]+""[^>]*>Cómo llegar</a>", decoded);
        Assert.DoesNotContain("<iframe", html);
    }

    [Fact]
    public async Task Get_Root_WithDifferentBusinessName_ShowsIt()
    {
        using var factory = new PizzeriaWebFactory()
            .WithSetting("Business:Name", "Pizzas de Prueba");

        var html = Decode(await GetHtmlAsync(factory));

        Assert.Matches(@"<title>[^<]*Pizzas de Prueba[^<]*</title>", html);
        Assert.Matches(@"<h1[^>]*>Pizzas de Prueba</h1>", html);
        Assert.DoesNotContain("Harry's Pizza", html);
    }

    [Fact]
    public async Task Get_Root_Footer_ShowsNamePhoneWhatsAppAndPrivacyLink_WithoutSocial()
    {
        using var factory = new PizzeriaWebFactory();

        var html = Decode(await GetHtmlAsync(factory));
        var footer = Regex.Match(html, @"<footer[\s\S]*?</footer>").Value;

        Assert.Contains("Harry's Pizza", footer);
        Assert.Contains("311 370 6576", footer);
        Assert.Contains("href=\"tel:+573113706576\"", footer);
        Assert.Contains(WhatsAppPrefix, footer);
        Assert.Contains("href=\"/privacidad\"", footer);
        Assert.DoesNotContain("site-footer__link--social", footer);
        Assert.DoesNotContain("Redes", footer);
    }

    [Fact]
    public async Task Get_Root_Footer_WithSocialConfigured_ShowsTheLink()
    {
        using var factory = new PizzeriaWebFactory()
            .WithSetting("Business:Social:0:Name", "Instagram")
            .WithSetting("Business:Social:0:Url", "https://example.com/harrys");

        var html = Decode(await GetHtmlAsync(factory));
        var footer = Regex.Match(html, @"<footer[\s\S]*?</footer>").Value;

        Assert.Matches(@"href=""https://example\.com/harrys""[^>]*>Instagram</a>", footer);
    }

    [Fact]
    public async Task Get_Root_EveryImage_HasAltWidthAndHeight()
    {
        using var factory = new PizzeriaWebFactory();

        var html = await GetHtmlAsync(factory);
        var images = Regex.Matches(html, @"<img\b[^>]*>").Select(match => match.Value).ToList();

        Assert.NotEmpty(images);
        Assert.All(images, image =>
        {
            Assert.Contains(" alt=\"", image);
            Assert.Matches(@" width=""\d+""", image);
            Assert.Matches(@" height=""\d+""", image);
        });
    }

    [Fact]
    public async Task Get_Root_ChequeredStrip_AppearsExactlyTwice()
    {
        using var factory = new PizzeriaWebFactory();

        var html = await GetHtmlAsync(factory);

        Assert.Equal(2, Regex.Matches(html, @"class=""checker""").Count);
    }

    [Theory]
    [InlineData("/css/site.css")]
    [InlineData("/favicon.svg")]
    [InlineData("/img/pizza-placeholder.svg")]
    [InlineData("/fonts/bowlby-one-latin-400-normal.woff2")]
    [InlineData("/fonts/archivo-latin-wdth-normal.woff2")]
    public async Task Get_StaticAsset_Returns200(string path)
    {
        using var factory = new PizzeriaWebFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_Error_ShowsGenericSpanishText_WithoutTechnicalDetails()
    {
        using var factory = new PizzeriaWebFactory();

        var html = Decode(await GetHtmlAsync(factory, "/Error"));

        Assert.Contains("<html lang=\"es\">", html);
        Assert.Contains("Algo falló al cargar la página", html);
        Assert.DoesNotContain("Development", html);
        Assert.DoesNotContain("Request ID", html);
        Assert.DoesNotContain("An error occurred", html);
    }

    [Fact]
    public async Task Get_Privacidad_Returns200_WithCommonLayout()
    {
        using var factory = new PizzeriaWebFactory();

        var html = Decode(await GetHtmlAsync(factory, "/privacidad"));

        Assert.Contains("<html lang=\"es\">", html);
        Assert.Matches(@"<footer[\s\S]*Harry's Pizza[\s\S]*</footer>", html);
    }

    [Fact]
    public async Task SeedMenuAsync_FillsTheDatabaseUsedByTheApp()
    {
        using var factory = new PizzeriaWebFactory();

        await factory.SeedMenuAsync();
        await GetHtmlAsync(factory);

        await using var context = factory.CreateDbContext();
        Assert.Equal(3, context.MenuItems.Count());
    }
}
