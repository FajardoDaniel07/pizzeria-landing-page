using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pizzeria.Data;
using Pizzeria.Models;
using Pizzeria.Tests.TestSupport;

namespace Pizzeria.Tests;

/// <summary>
/// Verification phase: acceptance criteria of 03-requisitos.md that were marked as testable
/// (U/I) or cheaply checkable and had no automated test (RF-01, RF-02, RF-13, RF-16, RF-19,
/// RF-20, RF-21, RF-22, RF-29, RF-30).
/// </summary>
public class RequirementGapTests
{
    private static async Task<string> GetDecodedAsync(PizzeriaWebFactory factory, string path = "/")
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    // ---------- Layout (RF-01, RF-02) ----------

    [Theory]
    [InlineData("/privacidad")]
    [InlineData("/Error")]
    public async Task Get_SecondaryPages_HaveNoEnglishTemplateText_RF01(string path)
    {
        using var factory = new PizzeriaWebFactory();

        var html = await GetDecodedAsync(factory, path);

        Assert.DoesNotContain("Welcome", html);
        Assert.DoesNotContain("Home", html);
        Assert.DoesNotContain("Privacy", html);
        Assert.DoesNotContain("Learn about", html);
        Assert.Matches(@"<title>[^<]*Harry's Pizza[^<]*</title>", html);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Get_Root_EveryHeaderAnchor_HasExactlyOneTargetSection_RF02(bool seeded)
    {
        using var factory = new PizzeriaWebFactory();
        if (seeded)
        {
            await factory.SeedMenuAsync();
        }

        var html = await GetDecodedAsync(factory);

        Assert.All(
            new[] { "menu", "horarios", "contacto" },
            id => Assert.Single(Regex.Matches(html, $@"<section[^>]*\bid=""{id}""")));
        // The skip link target exists too.
        Assert.Single(Regex.Matches(html, @"\bid=""contenido"""));
    }

    [Fact]
    public async Task Get_Root_HasNoDuplicatedIds_RF02()
    {
        using var factory = new PizzeriaWebFactory();
        await factory.SeedMenuAsync();

        var html = await GetDecodedAsync(factory);
        var duplicated = Regex.Matches(html, @"\sid=""([^""]+)""")
            .Select(match => match.Groups[1].Value)
            .GroupBy(id => id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        Assert.Empty(duplicated);
    }

    // ---------- About (RF-13) ----------

    [Fact]
    public async Task Get_Root_About_HasTwoSentences_NoImage_AndNoInventedFigures_RF13()
    {
        using var factory = new PizzeriaWebFactory();

        var html = await GetDecodedAsync(factory);
        var about = Regex.Match(html, @"<section[^>]*aria-labelledby=""about-title""[^>]*>[\s\S]*?</section>").Value;
        var paragraphs = Regex.Matches(about, @"<p\b[^>]*>([\s\S]*?)</p>").Select(match => match.Groups[1].Value).ToList();

        Assert.NotEmpty(about);
        Assert.Equal(2, paragraphs.Count);
        Assert.All(paragraphs, text => Assert.EndsWith(".", text.Trim()));
        Assert.DoesNotContain("<img", about);
        // No years, prizes or history.
        Assert.All(paragraphs, text => Assert.DoesNotMatch(@"\d", text));
        Assert.All(paragraphs, text => Assert.DoesNotMatch("(?i)premi|desde |años|tradici|fundad", text));
    }

    // ---------- The form binds the input model, never an entity (RF-16, overposting) ----------

    [Fact]
    public void PageModels_BindOnlyContactInput_NeverAnEntity_RF16()
    {
        var pageModels = typeof(Program).Assembly.GetTypes()
            .Where(type => typeof(PageModel).IsAssignableFrom(type))
            .ToList();

        var bound = pageModels
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            .Where(property => property.GetCustomAttribute<BindPropertyAttribute>() is not null)
            .ToList();

        var contact = Assert.Single(bound);
        Assert.Equal(typeof(ContactInput), contact.PropertyType);
        Assert.False(contact.GetCustomAttribute<BindPropertyAttribute>()!.SupportsGet);
        Assert.All(pageModels, type => Assert.Null(type.GetCustomAttribute<BindPropertiesAttribute>()));
    }

    [Fact]
    public void ContactInput_ExposesOnlyTheFormFields_RF16()
    {
        var names = typeof(ContactInput).GetProperties().Select(property => property.Name).Order().ToList();

        // No Id and no CreatedAtUtc: a client cannot choose the key or the date of a message.
        Assert.Equal(["Email", "Message", "Name", "Phone", "Website"], names);
    }

    [Fact]
    public async Task Post_WithExtraEntityFields_IgnoresThem_RF16()
    {
        using var factory = new PizzeriaWebFactory();
        using var form = new ContactFormClient(factory);
        var fields = ContactFormClient.Fields(
            "Ana", "3113706576", "", "Quiero reservar", token: await form.GetTokenAsync());
        fields.Add(new("Contact.Id", "999"));
        fields.Add(new("Contact.CreatedAtUtc", "2001-01-01T00:00:00Z"));
        fields.Add(new("Id", "999"));
        fields.Add(new("CreatedAtUtc", "2001-01-01T00:00:00Z"));

        using var response = await form.PostRawAsync(fields);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var context = factory.CreateDbContext();
        var saved = Assert.Single(await context.ContactMessages.AsNoTracking().ToListAsync());
        Assert.NotEqual(999, saved.Id);
        Assert.True(saved.CreatedAtUtc.Year >= 2026);
    }

    // ---------- Privacy route (RF-19) ----------

    [Fact]
    public async Task Privacy_IsServedOnlyAtTheSpanishRoute_RF19()
    {
        using var factory = new PizzeriaWebFactory();
        using var client = factory.CreateClient();

        using var spanish = await client.GetAsync("/privacidad");
        using var english = await client.GetAsync("/Privacy");

        Assert.Equal(HttpStatusCode.OK, spanish.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, english.StatusCode);
        Assert.NotNull(typeof(Program).Assembly.GetType("Pizzeria.Pages.PrivacyModel"));
    }

    // ---------- Migration InitialCreate (RF-20, RF-21, RF-22) ----------

    private static Migration CreateOnlyMigration()
    {
        var type = Assert.Single(
            typeof(Program).Assembly.GetTypes(),
            candidate => candidate.IsSubclassOf(typeof(Migration)));

        Assert.Equal("InitialCreate", type.Name);
        Assert.EndsWith("_InitialCreate", type.GetCustomAttribute<MigrationAttribute>()!.Id);
        Assert.Equal(typeof(AppDbContext), type.GetCustomAttribute<DbContextAttribute>()!.ContextType);
        return (Migration)Activator.CreateInstance(type)!;
    }

    [Fact]
    public void Migrations_ThereIsOnlyInitialCreate_WithTwoTablesOneIndex_AndNoData_RF22()
    {
        var operations = CreateOnlyMigration().UpOperations;

        var tables = operations.OfType<CreateTableOperation>().Select(table => table.Name).Order().ToList();
        Assert.Equal(["ContactMessages", "MenuItems"], tables);

        var index = Assert.Single(operations.OfType<CreateIndexOperation>());
        Assert.Equal("MenuItems", index.Table);
        Assert.Equal(["Category"], index.Columns);

        Assert.Empty(operations.OfType<InsertDataOperation>());
        Assert.Empty(operations.OfType<SqlOperation>());
        Assert.Equal(3, operations.Count);
    }

    [Theory]
    [InlineData("MenuItems", "Id", "int", false)]
    [InlineData("MenuItems", "Name", "nvarchar(80)", false)]
    [InlineData("MenuItems", "Description", "nvarchar(300)", true)]
    [InlineData("MenuItems", "Price", "decimal(10,2)", false)]
    [InlineData("MenuItems", "Category", "nvarchar(20)", false)]
    [InlineData("MenuItems", "ImagePath", "nvarchar(200)", true)]
    [InlineData("MenuItems", "IsFeatured", "bit", false)]
    [InlineData("ContactMessages", "Id", "int", false)]
    [InlineData("ContactMessages", "Name", "nvarchar(80)", false)]
    [InlineData("ContactMessages", "Phone", "nvarchar(20)", true)]
    [InlineData("ContactMessages", "Email", "nvarchar(254)", true)]
    [InlineData("ContactMessages", "Message", "nvarchar(1000)", false)]
    [InlineData("ContactMessages", "CreatedAtUtc", "datetime2(0)", false)]
    public void Migration_ColumnTypes_MatchTheRequirements_RF20_RF21(
        string table, string column, string expectedType, bool expectedNullable)
    {
        var create = Assert.Single(
            CreateOnlyMigration().UpOperations.OfType<CreateTableOperation>(), operation => operation.Name == table);
        var definition = Assert.Single(create.Columns, candidate => candidate.Name == column);

        Assert.Equal(expectedType, definition.ColumnType);
        Assert.Equal(expectedNullable, definition.IsNullable);
    }

    [Theory]
    [InlineData("MenuItems", 7)]
    [InlineData("ContactMessages", 6)]
    public void Migration_Tables_HaveNoExtraColumns_RF20_RF21(string table, int expectedColumns)
    {
        var create = Assert.Single(
            CreateOnlyMigration().UpOperations.OfType<CreateTableOperation>(), operation => operation.Name == table);

        Assert.Equal(expectedColumns, create.Columns.Count);
        Assert.Equal(["Id"], create.PrimaryKey!.Columns);
        // No IP address and no user agent next to the message.
        Assert.DoesNotContain(create.Columns, column => Regex.IsMatch(column.Name, "(?i)^(remote|client)?ip|agent"));
    }

    [Fact]
    public void Migration_IsFeatured_DefaultsToFalse_RF20()
    {
        var create = Assert.Single(
            CreateOnlyMigration().UpOperations.OfType<CreateTableOperation>(), operation => operation.Name == "MenuItems");
        var featured = Assert.Single(create.Columns, column => column.Name == "IsFeatured");

        Assert.Equal(false, featured.DefaultValue);
    }

    // ---------- Configuration (RF-29, RF-30) ----------

    [Fact]
    public void Business_InitialValues_ComeFromAppSettings_RF29()
    {
        using var factory = new PizzeriaWebFactory();

        var business = factory.Services.GetRequiredService<IOptions<BusinessInfo>>().Value;

        Assert.Equal("Harry's Pizza", business.Name);
        Assert.Equal("3113706576", business.Phone);
        Assert.Equal("573113706576", business.WhatsApp);
        Assert.Equal(string.Empty, business.Address);
        Assert.Empty(business.Social);
        Assert.Equal(
            [("Lunes a jueves", "2:00 p. m. a 10:30 p. m."), ("Viernes a domingo", "12:00 m. a 10:30 p. m.")],
            business.Hours.Select(row => (row.Days, row.Time)).ToList());
    }

    private static IEnumerable<(string Path, JsonElement Value)> Flatten(JsonElement element, string path)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    foreach (var child in Flatten(property.Value, $"{path}:{property.Name}"))
                    {
                        yield return child;
                    }
                }

                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var child in Flatten(item, $"{path}:{index++}"))
                    {
                        yield return child;
                    }
                }

                break;
            default:
                yield return (path, element);
                break;
        }
    }

    [Fact]
    public void AppSettingsFiles_HaveNoConnectionStringNorSecrets_RF30()
    {
        using var factory = new PizzeriaWebFactory();
        var contentRoot = factory.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath;

        var files = Directory.GetFiles(contentRoot, "appsettings*.json");
        Assert.Contains(files, file => Path.GetFileName(file) == "appsettings.json");

        foreach (var file in files)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            var entries = Flatten(document.RootElement, Path.GetFileName(file)).ToList();

            Assert.DoesNotContain(entries, entry =>
                Regex.IsMatch(entry.Path, "(?i)connectionstring|password|pwd|secret|apikey|token"));
            Assert.DoesNotContain(entries, entry =>
                entry.Value.ValueKind == JsonValueKind.String &&
                Regex.IsMatch(entry.Value.GetString()!, @"(?i)(server|data source|password|pwd|user id)\s*="));
        }
    }
}
