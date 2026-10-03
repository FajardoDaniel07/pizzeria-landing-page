using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Pizzeria.Tests.TestSupport;

/// <summary>
/// Drives the contact form like a browser would: loads the page to get the antiforgery token and
/// cookie, posts the fields and leaves redirects to the test.
/// </summary>
public sealed class ContactFormClient : IDisposable
{
    public const string TokenField = "__RequestVerificationToken";

    private readonly HttpClient _client;

    public ContactFormClient(PizzeriaWebFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    /// <summary>GET that returns the raw (still HTML-encoded) body of a 200 response.</summary>
    public async Task<string> GetHtmlAsync(string path = "/")
    {
        using var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>Reads the antiforgery token rendered inside the form.</summary>
    public async Task<string> GetTokenAsync()
    {
        var html = await GetHtmlAsync();
        var input = Regex.Match(html, $@"<input[^>]*name=""{TokenField}""[^>]*>").Value;
        var token = Regex.Match(input, @"value=""([^""]+)""").Groups[1].Value;

        Assert.NotEmpty(token);
        return token;
    }

    /// <summary>Posts the form with a valid antiforgery token.</summary>
    public async Task<HttpResponseMessage> PostAsync(
        string? name = null,
        string? phone = null,
        string? email = null,
        string? message = null,
        string? website = null)
    {
        var token = await GetTokenAsync();
        return await PostRawAsync(Fields(name, phone, email, message, website, token));
    }

    /// <summary>Posts exactly the given fields (no token is added).</summary>
    public Task<HttpResponseMessage> PostRawAsync(IEnumerable<KeyValuePair<string, string>> fields) =>
        _client.PostAsync("/", new FormUrlEncodedContent(fields));

    public static List<KeyValuePair<string, string>> Fields(
        string? name,
        string? phone,
        string? email,
        string? message,
        string? website = null,
        string? token = null)
    {
        var fields = new List<KeyValuePair<string, string>>();

        void Add(string key, string? value)
        {
            if (value is not null)
            {
                fields.Add(new KeyValuePair<string, string>(key, value));
            }
        }

        Add("Contact.Name", name);
        Add("Contact.Phone", phone);
        Add("Contact.Email", email);
        Add("Contact.Message", message);
        Add("Contact.Website", website);
        Add(TokenField, token);
        return fields;
    }

    public void Dispose() => _client.Dispose();
}
