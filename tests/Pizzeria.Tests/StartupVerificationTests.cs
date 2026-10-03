using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pizzeria.Data;
using Pizzeria.Models;
using Pizzeria.Services;
using Pizzeria.Tests.TestSupport;

namespace Pizzeria.Tests;

/// <summary>
/// Verification phase: behaviour that depends on how the app starts and on its environment
/// (RF-22, RF-23, RF-30, RNF-06, RNF-09). These cases had no automated test.
/// The database is always the SQLite in-memory one of <see cref="PizzeriaWebFactory"/> and the
/// connection string is the fake test value, whatever the environment.
/// </summary>
public class StartupVerificationTests
{
    private const string StrictCsp =
        "default-src 'self'; img-src 'self' data:; style-src 'self'; script-src 'self'; " +
        "frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

    private static WebApplicationFactory<Program> InEnvironment(PizzeriaWebFactory factory, string environment) =>
        factory.WithWebHostBuilder(builder => builder.UseEnvironment(environment));

    private static async Task<bool> TableExistsAsync(AppDbContext context, string table)
    {
        var connection = context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$name";
        parameter.Value = table;
        command.Parameters.Add(parameter);

        return Convert.ToInt64(await command.ExecuteScalarAsync()) > 0;
    }

    private static IEnumerable<Exception> Chain(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }

    // ---------- Connection string (RF-30) ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Startup_WithoutConnectionString_FailsWithSpanishInstructions_RF30(string? value)
    {
        using var factory = new PizzeriaWebFactory().WithSetting("ConnectionStrings:Default", value);

        var thrown = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        var error = Assert.Single(Chain(thrown).OfType<InvalidOperationException>(), exception =>
            exception.Message.Contains("ConnectionStrings:Default", StringComparison.Ordinal));
        Assert.Contains("Falta la cadena de conexión", error.Message);
        Assert.Contains(
            "dotnet user-secrets set \"ConnectionStrings:Default\" \"<cadena>\" --project src/Pizzeria",
            error.Message);
    }

    [Fact]
    public async Task Startup_WithConnectionString_Starts_RF30()
    {
        using var factory = new PizzeriaWebFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---------- Seeder runs only in Development; migrations never run at startup (RF-22, RF-23) ----------

    [Fact]
    public async Task Startup_InDevelopment_WithEmptyMenu_SeedsTheThreePizzas_RF23()
    {
        using var factory = new PizzeriaWebFactory();
        using var development = InEnvironment(factory, "Development");
        using var client = development.CreateClient();

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/"));

        await using var context = factory.CreateDbContext();
        var names = await context.MenuItems.AsNoTracking().Select(item => item.Name).ToListAsync();
        names.Sort(StringComparer.Ordinal);

        Assert.Equal(["Carnes", "Champiñón con pollo", "Mexicana"], names);
        Assert.Equal(0, await context.ContactMessages.CountAsync());
        Assert.Contains("Desde $10.500", html);
    }

    [Fact]
    public async Task Startup_InDevelopment_WithExistingMenu_InsertsNothing_RF23()
    {
        using var factory = new PizzeriaWebFactory();

        await using (var arrange = factory.CreateDbContext())
        {
            arrange.MenuItems.Add(new MenuItem { Name = "Hawaiana", Price = 12000m, Category = MenuCategory.Pizza });
            await arrange.SaveChangesAsync();
        }

        using var development = InEnvironment(factory, "Development");
        using var client = development.CreateClient();
        using var response = await client.GetAsync("/");

        await using var context = factory.CreateDbContext();
        var item = Assert.Single(await context.MenuItems.AsNoTracking().ToListAsync());
        Assert.Equal("Hawaiana", item.Name);
    }

    [Theory]
    [InlineData("Testing")]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Startup_OutsideDevelopment_DoesNotSeed_RF23(string environment)
    {
        using var factory = new PizzeriaWebFactory();
        using var host = InEnvironment(factory, environment);
        using var client = host.CreateClient();

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var context = factory.CreateDbContext();
        Assert.Equal(0, await context.MenuItems.CountAsync());
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Startup_NeverAppliesMigrations_RF22(string environment)
    {
        using var factory = new PizzeriaWebFactory();
        using var host = InEnvironment(factory, environment);
        using var client = host.CreateClient();

        using var response = await client.GetAsync("/");

        // Database.Migrate() would create the history table before applying anything.
        await using var context = factory.CreateDbContext();
        await context.Database.OpenConnectionAsync();
        Assert.False(await TableExistsAsync(context, "__EFMigrationsHistory"));
        Assert.True(await TableExistsAsync(context, "MenuItems"));
    }

    // ---------- CSP per environment (RNF-06) ----------

    [Theory]
    [InlineData("/")]
    [InlineData("/privacidad")]
    public async Task Csp_InDevelopment_OnlyAddsConnectSrcForHotReload_RNF06(string path)
    {
        using var factory = new PizzeriaWebFactory();
        using var development = InEnvironment(factory, "Development");
        using var client = development.CreateClient();

        using var response = await client.GetAsync(path);

        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Equal(StrictCsp + "; connect-src 'self' ws: wss:", csp);
        Assert.DoesNotContain("unsafe-inline", csp);
        Assert.DoesNotContain("unsafe-eval", csp);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Csp_OutsideDevelopment_IsExactlyTheStrictPolicy_RNF06(string environment)
    {
        using var factory = new PizzeriaWebFactory();
        using var host = InEnvironment(factory, environment);
        using var client = host.CreateClient();

        using var response = await client.GetAsync("/");

        Assert.Equal(StrictCsp, Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal(
            "strict-origin-when-cross-origin", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
    }

    // ---------- Unhandled errors outside Development (RNF-09, RF-05) ----------

    /// <summary>
    /// The page model catches every menu failure except cancellation, so this is the one
    /// exception that reaches the exception handler without changing production code.
    /// </summary>
    private sealed class UnhandledMenuService(AppDbContext context) : MenuService(context)
    {
        public const string Detail = "Simulated unhandled failure on sql-prod-host";

        public override Task<IReadOnlyList<MenuCategoryGroup>> GetMenuByCategoryAsync(
            CancellationToken cancellationToken = default) =>
            throw new OperationCanceledException(Detail);
    }

    private sealed class UnhandledContactService(AppDbContext context, TimeProvider timeProvider)
        : ContactService(context, timeProvider)
    {
        public const string Detail = "Simulated unhandled save failure on sql-prod-host";

        public override Task SaveAsync(ContactInput input, CancellationToken cancellationToken = default) =>
            throw new OperationCanceledException($"{Detail} ({input.Name}, {input.Message})");
    }

    private static async Task AssertGenericErrorPageAsync(HttpResponseMessage response, params string[] forbidden)
    {
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("<html lang=\"es\">", html);
        Assert.Contains("Algo falló al cargar la página", html);
        Assert.Contains("https://wa.me/573113706576", html);
        Assert.DoesNotContain("Exception", html);
        Assert.DoesNotContain("   at ", html);
        Assert.DoesNotContain("Pizzeria.Pages", html);
        Assert.All(forbidden, text => Assert.DoesNotContain(text, html));
        Assert.Equal(StrictCsp, Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }

    [Theory]
    [InlineData("Testing")]
    [InlineData("Production")]
    public async Task Get_UnhandledException_OutsideDevelopment_ShowsTheGenericErrorPage_RNF09(string environment)
    {
        using var factory = new PizzeriaWebFactory()
            .WithServices(services => services.AddScoped<MenuService, UnhandledMenuService>());
        using var host = InEnvironment(factory, environment);
        using var client = host.CreateClient();

        using var response = await client.GetAsync("/");

        await AssertGenericErrorPageAsync(response, UnhandledMenuService.Detail, "sql-prod-host");
    }

    [Fact]
    public async Task Post_UnhandledException_OutsideDevelopment_ShowsTheGenericErrorPage_RNF09()
    {
        using var factory = new PizzeriaWebFactory()
            .WithServices(services => services.AddScoped<ContactService, UnhandledContactService>());
        using var form = new ContactFormClient(factory);

        using var response = await form.PostAsync("Ana Secreta", "3113706576", "", "Mensaje privado de prueba");

        await AssertGenericErrorPageAsync(
            response, UnhandledContactService.Detail, "sql-prod-host", "Ana Secreta", "Mensaje privado de prueba");
    }
}
