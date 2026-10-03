using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pizzeria.Models;
using Pizzeria.Services;
using Pizzeria.Tests.TestSupport;

namespace Pizzeria.Tests;

/// <summary>
/// Block 7: contact form end to end (RF-16, RF-17, RF-18, RF-27, RNF-01 to RNF-04, RNF-08, RNF-09, RNF-13).
/// Each test owns its factory, so the POST rate limit never leaks between tests (RNF-29).
/// </summary>
public class ContactFormTests
{
    private const string SaveError = "No pudimos guardar tu mensaje. Inténtalo de nuevo o escríbenos por WhatsApp.";

    private static string Decode(string html) => WebUtility.HtmlDecode(html);

    private static async Task<string> ReadDecodedAsync(HttpResponseMessage response) =>
        Decode(await response.Content.ReadAsStringAsync());

    private static string ContactSection(string html) =>
        Regex.Match(html, @"<section[^>]*id=""contacto""[^>]*>[\s\S]*?</section>").Value;

    private static string Tag(string html, string id) =>
        Regex.Match(html, $@"<(?:input|textarea)[^>]*\bid=""{id}""[^>]*>").Value;

    private static async Task<List<ContactMessage>> SavedMessagesAsync(PizzeriaWebFactory factory)
    {
        await using var context = factory.CreateDbContext();
        return await context.ContactMessages.AsNoTracking().ToListAsync();
    }

    private static void AssertRedirectsToContactSection(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith("/#contacto", response.Headers.Location!.OriginalString);
    }

    // ---------- Markup (RF-16, RNF-01, RNF-04, RNF-13) ----------

    [Fact]
    public async Task Get_Root_Form_PostsToTheContactSection_WithAntiforgeryToken_RF16()
    {
        using var factory = new PizzeriaWebFactory();
        using var form = new ContactFormClient(factory);

        var section = ContactSection(Decode(await form.GetHtmlAsync()));
        var formTag = Regex.Match(section, @"<form\b[^>]*>").Value;

        Assert.Contains("method=\"post\"", formTag);
        Assert.Matches(@"action=""[^""]*#contacto""", formTag);
        Assert.Matches(@"<input[^>]*name=""__RequestVerificationToken""[^>]*type=""hidden""", section);
        Assert.Matches(@"<button[^>]*type=""submit""[^>]*>Enviar mensaje</button>", section);
        Assert.Matches(@"<a[^>]*href=""/privacidad""", section);
    }

    [Fact]
    public async Task Get_Root_Form_HasTheFourLabelledFields_AndNoReservationFields_RF16()
    {
        using var factory = new PizzeriaWebFactory();
        using var form = new ContactFormClient(factory);

        var section = ContactSection(Decode(await form.GetHtmlAsync()));

        Assert.Matches(@"<label[^>]*for=""contact-name""[^>]*>Nombre</label>", section);
        Assert.Matches(@"<label[^>]*for=""contact-phone""[^>]*>Teléfono</label>", section);
        Assert.Matches(@"<label[^>]*for=""contact-email""[^>]*>Correo</label>", section);
        Assert.Matches(@"<label[^>]*for=""contact-message""[^>]*>Mensaje</label>", section);

        var name = Tag(section, "contact-name");
        Assert.Contains("type=\"text\"", name);
        Assert.Matches(@"\srequired[\s>]", name);
        Assert.Contains("maxlength=\"80\"", name);

        var phone = Tag(section, "contact-phone");
        Assert.Contains("type=\"tel\"", phone);
        Assert.Contains("maxlength=\"20\"", phone);
        Assert.DoesNotMatch(@"\srequired[\s>]", phone);

        var email = Tag(section, "contact-email");
        Assert.Contains("type=\"email\"", email);
        Assert.Contains("maxlength=\"254\"", email);
        Assert.DoesNotMatch(@"\srequired[\s>]", email);

        var message = Tag(section, "contact-message");
        Assert.StartsWith("<textarea", message);
        Assert.Matches(@"\srequired[\s>]", message);
        Assert.Contains("maxlength=\"1000\"", message);

        Assert.DoesNotContain("type=\"date\"", section);
        Assert.DoesNotContain("type=\"time\"", section);
        Assert.DoesNotContain("type=\"number\"", section);
        Assert.DoesNotContain("placeholder=", section);
    }

    [Fact]
    public async Task Get_Root_Form_MessageHelp_IsBelowTheFieldAndAssociated_RNF13()
    {
        using var factory = new PizzeriaWebFactory();
        using var form = new ContactFormClient(factory);

        var section = ContactSection(Decode(await form.GetHtmlAsync()));

        Assert.Matches(
            @"</textarea>\s*<p[^>]*id=""contact-message-hint""[^>]*>Para reservar, indica fecha, hora y número de personas</p>",
            section);
        Assert.Matches(@"aria-describedby=""[^""]*contact-message-hint", Tag(section, "contact-message"));
        // No error state on a fresh page.
        Assert.DoesNotContain("Error:", section);
        Assert.DoesNotContain("aria-invalid", section);
        Assert.DoesNotContain("Mensaje enviado", section);
    }

    [Fact]
    public async Task Get_Root_Form_Honeypot_IsHiddenByClass_AndOutOfTabOrder_RNF04()
    {
        using var factory = new PizzeriaWebFactory();
        using var form = new ContactFormClient(factory);

        var section = ContactSection(await form.GetHtmlAsync());
        var trap = Regex.Match(section, @"<div class=""hp"" aria-hidden=""true"">[\s\S]*?</div>").Value;
        var input = Regex.Match(trap, @"<input\b[^>]*>").Value;

        Assert.Contains("name=\"Contact.Website\"", input);
        Assert.Contains("tabindex=\"-1\"", input);
        Assert.Contains("autocomplete=\"off\"", input);
        Assert.DoesNotContain(" style=", section);
    }

    [Fact]
    public void Project_HasNoIgnoreAntiforgeryToken_RNF01()
    {
        var offenders = typeof(Program).Assembly.GetTypes()
            .Where(type => type.GetCustomAttributes(typeof(IgnoreAntiforgeryTokenAttribute), inherit: true).Length > 0)
            .Select(type => type.FullName);

        Assert.Empty(offenders);
    }

    // ---------- Valid submission (RF-17, RNF-03) ----------

    [Theory]
    [InlineData("311 370 6576", null)]
    [InlineData(null, "ana@example.com")]
    [InlineData("311 370 6576", "ana@example.com")]
    public async Task Post_Valid_SavesOneRow_AndRedirectsToTheContactSection_RF17(string? phone, string? email)
    {
        using var factory = new PizzeriaWebFactory();
        using var form = new ContactFormClient(factory);

        using var response = await form.PostAsync("Ana", phone ?? "", email ?? "", "Quiero reservar", website: "");

        AssertRedirectsToContactSection(response);
        var saved = Assert.Single(await SavedMessagesAsync(factory));
        Assert.Equal("Ana", saved.Name);
        Assert.Equal(phone, saved.Phone);
        Assert.Equal(email, saved.Email);
        Assert.Equal("Quiero reservar", saved.Message);
    }

    [Fact]
    public async Task Post_Valid_ThenGet_ShowsTheConfirmationOnce_WithEmptyFields_RF17()
    {
        using var factory = new PizzeriaWebFactory();
        using var form = new ContactFormClient(factory);

        using var response = await form.PostAsync("Ana", "311 370 6576", "", "Quiero reservar");
        AssertRedirectsToContactSection(response);

        var first = ContactSection(Decode(await form.GetHtmlAsync()));
        Assert.Matches(@"<p[^>]*role=""status""[^>]*>Mensaje enviado</p>", first);
        Assert.DoesNotContain("value=\"Ana\"", first);
        Assert.DoesNotMatch(@"\svalue=""[^""]+""", Tag(first, "contact-name"));
        Assert.DoesNotMatch(@"\svalue=""[^""]+""", Tag(first, "contact-phone"));
        Assert.DoesNotMatch(@"\svalue=""[^""]+""", Tag(first, "contact-email"));
        Assert.Matches(@"<textarea[^>]*id=""contact-message""[^>]*></textarea>", first);
        Assert.DoesNotContain("Quiero reservar", first);

        var second = Decode(await form.GetHtmlAsync());
        Assert.DoesNotContain("Mensaje enviado", second);
        Assert.Single(await SavedMessagesAsync(factory));
    }

    // ---------- Invalid submission (RF-18, RNF-02, RNF-09) ----------

    [Theory]
    [InlineData("", "311 370 6576", "", "Quiero reservar", "contact-name", "Escribe tu nombre.")]
    [InlineData("A", "311 370 6576", "", "Quiero reservar", "contact-name", "El nombre debe tener entre 2 y 80 caracteres.")]
    [InlineData("Ana", "311-370-6576", "", "Quiero reservar", "contact-phone", "Escribe un teléfono válido: de 7 a 20 caracteres, solo números y espacios.")]
    [InlineData("Ana", "", "ana@", "Quiero reservar", "contact-email", "Escribe un correo válido.")]
    [InlineData("Ana", "311 370 6576", "", "", "contact-message", "Escribe tu mensaje.")]
    [InlineData("Ana", "311 370 6576", "", "Hola", "contact-message", "El mensaje debe tener entre 5 y 1000 caracteres.")]
    [InlineData("Ana", "", "", "Quiero reservar", "contact-phone", "Indica al menos un teléfono o un correo para poder responderte.")]
    [InlineData("Ana", "", "", "Quiero reservar", "contact-email", "Indica al menos un teléfono o un correo para poder responderte.")]
    public async Task Post_Invalid_Returns200_SavesNothing_AndShowsTheFieldError_RF18(
        string name, string phone, string email, string message, string fieldId, string expectedError)
    {
        using var factory = new PizzeriaWebFactory();
        using var form = new ContactFormClient(factory);

        using var response = await form.PostAsync(name, phone, email, message);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await SavedMessagesAsync(factory));

        var section = ContactSection(await ReadDecodedAsync(response));
        Assert.Matches($@"<p[^>]*id=""{fieldId}-error""[^>]*>Error: {Regex.Escape(expectedError)}</p>", section);

        // The error is tied to its field, not only shown near it (RNF-13).
        var field = Tag(section, fieldId);
        Assert.Contains("aria-invalid=\"true\"", field);
        Assert.Matches($@"aria-describedby=""[^""]*{fieldId}-error", field);
        Assert.Contains("role=\"alert\"", section);
        Assert.DoesNotContain("Mensaje enviado", section);
    }

    [Fact]
    public async Task Post_Invalid_KeepsWhatWasTyped_RF18()
    {
        using var factory = new PizzeriaWebFactory();
        using var form = new ContactFormClient(factory);

        using var response = await form.PostAsync("Ana", "abc", "ana@", "Quiero reservar para cuatro");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var section = ContactSection(await ReadDecodedAsync(response));
        Assert.Contains("value=\"Ana\"", Tag(section, "contact-name"));
        Assert.Contains("value=\"abc\"", Tag(section, "contact-phone"));
        Assert.Contains("value=\"ana@\"", Tag(section, "contact-email"));
        Assert.Matches(@"<textarea[^>]*>Quiero reservar para cuatro</textarea>", section);
        // Only the fields with a problem are flagged.
        Assert.DoesNotContain("aria-invalid", Tag(section, "contact-name"));
        Assert.DoesNotContain("aria-invalid", Tag(section, "contact-message"));
        Assert.Empty(await SavedMessagesAsync(factory));
    }

    [Fact]
    public async Task Post_WithTokenButNoFields_IsRejectedByServerValidation_RNF02()
    {
        using var factory = new PizzeriaWebFactory();
        using var form = new ContactFormClient(factory);

        using var response = await form.PostAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var section = ContactSection(await ReadDecodedAsync(response));
        Assert.Contains("Error: Escribe tu nombre.", section);
        Assert.Contains("Error: Escribe tu mensaje.", section);
        Assert.Empty(await SavedMessagesAsync(factory));
    }

    [Fact]
    public async Task Post_OverMaxLength_IsRejectedEvenWithoutBrowserLimits_RNF02()
    {
        using var factory = new PizzeriaWebFactory();
        using var form = new ContactFormClient(factory);

        using var response = await form.PostAsync(new string('a', 81), "3113706576", "", new string('m', 1001));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var section = ContactSection(await ReadDecodedAsync(response));
        Assert.Contains("Error: El nombre debe tener entre 2 y 80 caracteres.", section);
        Assert.Contains("Error: El mensaje debe tener entre 5 y 1000 caracteres.", section);
        Assert.Empty(await SavedMessagesAsync(factory));
    }

    [Fact]
    public async Task Post_Invalid_EncodesTheSubmittedValue_RNF09()
    {
        using var factory = new PizzeriaWebFactory();
        using var form = new ContactFormClient(factory);

        using var response = await form.PostAsync("<script>alert(1)</script>", "", "", "Quiero reservar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", raw);
        Assert.DoesNotContain("<script", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await SavedMessagesAsync(factory));
    }

    [Fact]
    public async Task Post_Invalid_StillShowsTheMenu()
    {
        using var factory = new PizzeriaWebFactory();
        await factory.SeedMenuAsync();
        using var form = new ContactFormClient(factory);

        using var response = await form.PostAsync("", "", "", "");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await ReadDecodedAsync(response);
        Assert.Contains("Desde $10.500", html);
        Assert.Contains("Pizzas destacadas", html);
        Assert.DoesNotContain(" style=", html);
    }

    // ---------- Honeypot (RNF-04, S-5) ----------

    [Fact]
    public async Task Post_WithHoneypotFilled_SavesNothing_AndAnswersLikeASuccess_RNF04()
    {
        using var factory = new PizzeriaWebFactory();
        using var form = new ContactFormClient(factory);

        using var response = await form.PostAsync("Ana", "311 370 6576", "", "Quiero reservar", website: "https://spam.example");

        AssertRedirectsToContactSection(response);
        Assert.Empty(await SavedMessagesAsync(factory));
        Assert.Matches(@"<p[^>]*role=""status""[^>]*>Mensaje enviado</p>", Decode(await form.GetHtmlAsync()));
    }

    [Fact]
    public async Task Post_WithHoneypotFilled_IsCheckedBeforeValidation_S5()
    {
        using var factory = new PizzeriaWebFactory();
        using var form = new ContactFormClient(factory);

        using var response = await form.PostAsync("", "abc", "nope", "", website: "x");

        AssertRedirectsToContactSection(response);
        Assert.Empty(await SavedMessagesAsync(factory));
    }

    // ---------- Antiforgery (RNF-01) ----------

    [Fact]
    public async Task Post_WithoutAntiforgeryToken_Returns400_AndSavesNothing_RNF01()
    {
        using var factory = new PizzeriaWebFactory();
        using var form = new ContactFormClient(factory);
        // Loads the page first so the antiforgery cookie exists: only the form token is missing.
        await form.GetHtmlAsync();

        using var response = await form.PostRawAsync(
            ContactFormClient.Fields("Ana", "311 370 6576", "", "Quiero reservar"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await SavedMessagesAsync(factory));
    }

    [Fact]
    public async Task Post_WithForgedAntiforgeryToken_Returns400_AndSavesNothing_RNF01()
    {
        using var factory = new PizzeriaWebFactory();
        using var form = new ContactFormClient(factory);
        await form.GetHtmlAsync();

        using var response = await form.PostRawAsync(
            ContactFormClient.Fields("Ana", "311 370 6576", "", "Quiero reservar", token: "forged-token"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await SavedMessagesAsync(factory));
    }

    // ---------- Save failure (RF-27, RNF-08) ----------

    [Fact]
    public async Task Post_Valid_WhenSaveFails_Returns200_WithTheGeneralError_AndKeepsTheValues_RF27()
    {
        using var factory = new PizzeriaWebFactory()
            .WithServices(services => services.AddScoped<ContactService, FailingContactService>());
        using var form = new ContactFormClient(factory);

        using var response = await form.PostAsync("Ana", "311 370 6576", "ana@example.com", "Quiero reservar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await ReadDecodedAsync(response);
        var section = ContactSection(html);
        Assert.Matches($@"<p[^>]*role=""alert""[^>]*>Error: {Regex.Escape(SaveError)}</p>", section);
        Assert.Single(Regex.Matches(html, Regex.Escape(SaveError)));
        Assert.Contains("value=\"Ana\"", Tag(section, "contact-name"));
        Assert.Contains("value=\"311 370 6576\"", Tag(section, "contact-phone"));
        Assert.Contains("value=\"ana@example.com\"", Tag(section, "contact-email"));
        Assert.Matches(@"<textarea[^>]*>Quiero reservar</textarea>", section);
        Assert.DoesNotContain("Mensaje enviado", html);
        Assert.DoesNotContain(FailingContactService.FailureDetail, html);
        Assert.DoesNotContain("Exception", html);
        Assert.Empty(await SavedMessagesAsync(factory));
    }

    [Fact]
    public async Task Post_Valid_WhenSaveFails_LogsTheFailureWithoutPersonalData_RNF08()
    {
        var logs = new RecordingLoggerProvider();
        using var factory = new PizzeriaWebFactory().WithServices(services =>
        {
            services.AddScoped<ContactService, FailingContactService>();
            services.AddSingleton<ILoggerProvider>(logs);
        });
        using var form = new ContactFormClient(factory);

        using var response = await form.PostAsync("Anastasia", "311 370 6576", "ana@example.com", "Quiero reservar una mesa");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(logs.Entries, entry =>
            entry.Level == LogLevel.Error && entry.Text.Contains("could not be saved", StringComparison.Ordinal));

        var allText = logs.AllText;
        Assert.DoesNotContain("Anastasia", allText);
        Assert.DoesNotContain("311 370 6576", allText);
        Assert.DoesNotContain("ana@example.com", allText);
        Assert.DoesNotContain("Quiero reservar una mesa", allText);
    }

    [Fact]
    public async Task Post_Valid_LogsNoPersonalData_RNF08()
    {
        var logs = new RecordingLoggerProvider();
        using var factory = new PizzeriaWebFactory()
            .WithServices(services => services.AddSingleton<ILoggerProvider>(logs));
        using var form = new ContactFormClient(factory);

        using var response = await form.PostAsync("Anastasia", "311 370 6576", "ana@example.com", "Quiero reservar una mesa");

        AssertRedirectsToContactSection(response);
        var allText = logs.AllText;
        Assert.DoesNotContain("Anastasia", allText);
        Assert.DoesNotContain("311 370 6576", allText);
        Assert.DoesNotContain("ana@example.com", allText);
        Assert.DoesNotContain("Quiero reservar una mesa", allText);
    }
}
