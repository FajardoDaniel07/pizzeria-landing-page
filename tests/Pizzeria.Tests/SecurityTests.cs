using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Pizzeria.Tests.TestSupport;

namespace Pizzeria.Tests;

/// <summary>
/// Block 8: security headers and CSP, POST rate limiting and the privacy notice
/// (RNF-05, RNF-06, RNF-17, RF-19).
/// </summary>
public class SecurityTests
{
    private const string ExpectedCsp =
        "default-src 'self'; img-src 'self' data:; style-src 'self'; script-src 'self'; " +
        "frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

    private static string Header(HttpResponseMessage response, string name) =>
        Assert.Single(response.Headers.GetValues(name));

    private static void AssertSecurityHeaders(HttpResponseMessage response)
    {
        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("strict-origin-when-cross-origin", Header(response, "Referrer-Policy"));
        Assert.Equal("DENY", Header(response, "X-Frame-Options"));
        Assert.Equal(ExpectedCsp, Header(response, "Content-Security-Policy"));
    }

    private static async Task<int> SavedCountAsync(PizzeriaWebFactory factory)
    {
        await using var context = factory.CreateDbContext();
        return await context.ContactMessages.CountAsync();
    }

    // ---------- Headers and CSP (RNF-06) ----------

    [Theory]
    [InlineData("/")]
    [InlineData("/privacidad")]
    [InlineData("/Error")]
    [InlineData("/css/site.css")]
    public async Task Get_HasSecurityHeaders_AndTheStrictCsp_RNF06(string path)
    {
        using var factory = new PizzeriaWebFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertSecurityHeaders(response);
    }

    [Fact]
    public async Task Post_RejectedResponses_AlsoCarrySecurityHeaders_RNF06()
    {
        using var factory = new PizzeriaWebFactory();
        using var form = new ContactFormClient(factory);

        using var response = await form.PostRawAsync(ContactFormClient.Fields("Ana", "3113706576", "", "Quiero reservar"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertSecurityHeaders(response);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/privacidad")]
    [InlineData("/Error")]
    public async Task Get_Page_HasNoInlineStylesOrScripts_NorThirdPartyResources_RNF06(string path)
    {
        using var factory = new PizzeriaWebFactory();
        await factory.SeedMenuAsync();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync(path);

        // What the CSP would block: inline styles, inline or external scripts, event handlers, frames.
        Assert.DoesNotContain("<style", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" style=", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<iframe", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(@"\son[a-z]+=""", html);
        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);

        var resourceUrls = Regex.Matches(html, "<(?:link|img|script)\\b[^>]*?\\b(?:href|src)=\"([^\"]*)\"")
            .Select(match => match.Groups[1].Value)
            .ToList();
        Assert.NotEmpty(resourceUrls);
        Assert.All(resourceUrls, url => Assert.Matches("^/[^/]", url));

        // The form posts to the same origin (form-action 'self').
        Assert.All(
            Regex.Matches(html, @"<form\b[^>]*action=""([^""]*)""").Select(match => match.Groups[1].Value),
            action => Assert.StartsWith("/", action));
    }

    // ---------- Rate limiting (RNF-05, S-7) ----------

    [Fact]
    public async Task Post_SixthInARow_Returns429_AndSavesNothingMore_RNF05()
    {
        using var factory = new PizzeriaWebFactory();
        using var form = new ContactFormClient(factory);
        var fields = ContactFormClient.Fields(
            "Ana", "311 370 6576", "", "Quiero reservar", token: await form.GetTokenAsync());

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var accepted = await form.PostRawAsync(fields);
            Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        }

        using var rejected = await form.PostRawAsync(fields);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal(5, await SavedCountAsync(factory));
        AssertSecurityHeaders(rejected);

        var body = await rejected.Content.ReadAsStringAsync();
        Assert.Contains("Espera unos minutos", body);
        Assert.DoesNotContain("<", body);
    }

    [Fact]
    public async Task Post_InvalidAndHoneypotSubmissions_AlsoCountTowardsTheLimit_S7()
    {
        using var factory = new PizzeriaWebFactory();
        using var form = new ContactFormClient(factory);
        var token = await form.GetTokenAsync();

        // Two invalid, two honeypot and one without token: five POSTs in the window.
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            using var invalid = await form.PostRawAsync(ContactFormClient.Fields("", "", "", "", token: token));
            Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);

            using var trapped = await form.PostRawAsync(
                ContactFormClient.Fields("Ana", "3113706576", "", "Quiero reservar", website: "x", token: token));
            Assert.Equal(HttpStatusCode.Redirect, trapped.StatusCode);
        }

        using var withoutToken = await form.PostRawAsync(ContactFormClient.Fields("Ana", "3113706576", "", "Quiero reservar"));
        Assert.Equal(HttpStatusCode.BadRequest, withoutToken.StatusCode);

        using var rejected = await form.PostRawAsync(
            ContactFormClient.Fields("Ana", "3113706576", "", "Quiero reservar", token: token));

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal(0, await SavedCountAsync(factory));
    }

    [Fact]
    public async Task Get_IsNeverRateLimited_EvenAfterThePostLimitIsReached_RNF05()
    {
        using var factory = new PizzeriaWebFactory();
        using var form = new ContactFormClient(factory);

        for (var attempt = 1; attempt <= 6; attempt++)
        {
            using var post = await form.PostRawAsync(ContactFormClient.Fields("Ana", "3113706576", "", "Quiero reservar"));
        }

        for (var attempt = 1; attempt <= 20; attempt++)
        {
            await form.GetHtmlAsync();
        }

        await form.GetHtmlAsync("/privacidad");
    }

    // ---------- Privacy notice (RF-19) ----------

    [Fact]
    public async Task Get_Privacidad_ExplainsDataPurposeResponsibleAndLaw_RF19()
    {
        using var factory = new PizzeriaWebFactory();
        using var client = factory.CreateClient();

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/privacidad"));
        var main = Regex.Match(html, @"<main[\s\S]*?</main>").Value;

        Assert.Contains("<html lang=\"es\">", html);
        Assert.Matches(@"<h1[^>]*>Tratamiento de datos personales</h1>", main);
        Assert.Contains("nombre", main);
        Assert.Contains("teléfono", main);
        Assert.Contains("correo", main);
        Assert.Contains("mensaje", main);
        Assert.Contains("responder tu consulta o tu reserva", main);
        Assert.Contains("Harry's Pizza", main);
        Assert.Contains("Ley 1581 de 2012", main);
        Assert.Contains("No guardamos tu dirección IP ni el agente de usuario", main);
        Assert.DoesNotContain("en preparación", main);
    }

    [Fact]
    public async Task Get_Privacidad_ResponsibleComesFromConfiguration_RF19()
    {
        using var factory = new PizzeriaWebFactory()
            .WithSetting("Business:Name", "Pizzas de Prueba")
            .WithSetting("Business:LegalName", "Pruebas S.A.S.");
        using var client = factory.CreateClient();

        var main = Regex.Match(
            WebUtility.HtmlDecode(await client.GetStringAsync("/privacidad")), @"<main[\s\S]*?</main>").Value;

        Assert.Contains("Pizzas de Prueba (Pruebas S.A.S.)", main);
        Assert.DoesNotContain("Harry's Pizza", main);
    }

    [Fact]
    public async Task Get_Privacidad_WithoutLegalName_ShowsOnlyTheBusinessName_RF19()
    {
        using var factory = new PizzeriaWebFactory();
        using var client = factory.CreateClient();

        var main = Regex.Match(
            WebUtility.HtmlDecode(await client.GetStringAsync("/privacidad")), @"<main[\s\S]*?</main>").Value;

        Assert.Contains("Harry's Pizza, Medellín, Antioquia.", main);
        Assert.DoesNotContain("()", main);
    }

    [Fact]
    public async Task Privacidad_IsLinkedFromTheFormAndTheFooter_RF19()
    {
        using var factory = new PizzeriaWebFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/");
        var form = Regex.Match(html, @"<form\b[\s\S]*?</form>").Value;
        var footer = Regex.Match(html, @"<footer[\s\S]*?</footer>").Value;

        Assert.Contains("href=\"/privacidad\"", form);
        Assert.Contains("href=\"/privacidad\"", footer);
    }
}
