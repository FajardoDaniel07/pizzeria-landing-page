using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pizzeria.Models;
using Pizzeria.Services;
using Pizzeria.Tests.TestSupport;

namespace Pizzeria.Tests;

/// <summary>
/// Block 6: hero price sticker, featured pizzas, menu by category and the page without a database
/// (RF-07, RF-09, RF-10, RF-11, RF-12, RF-26, RNF-14, RNF-19).
/// Assertions on text run against the decoded HTML (Razor encodes accented letters).
/// </summary>
public class MenuSectionsTests
{
    private const string UnavailableNotice = "El menú no está disponible en este momento. Escríbenos por WhatsApp.";

    private static async Task<string> GetHtmlAsync(PizzeriaWebFactory factory)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private static async Task<string> GetSeededHtmlAsync(PizzeriaWebFactory factory)
    {
        await factory.SeedMenuAsync();
        return await GetHtmlAsync(factory);
    }

    private static string Section(string html, string openingTagPattern) =>
        Regex.Match(html, openingTagPattern + @"[\s\S]*?</section>").Value;

    private static string FeaturedSection(string html) =>
        Section(html, @"<section[^>]*aria-labelledby=""featured-title""[^>]*>");

    private static string MenuSection(string html) => Section(html, @"<section[^>]*id=""menu""[^>]*>");

    private static string HeroSection(string html) => Section(html, @"<section class=""hero""[^>]*>");

    private static PizzeriaWebFactory FactoryWithFailingMenu(RecordingLoggerProvider? logs = null) =>
        new PizzeriaWebFactory().WithServices(services =>
        {
            services.AddScoped<MenuService, FailingMenuService>();

            if (logs is not null)
            {
                services.AddSingleton<ILoggerProvider>(logs);
            }
        });

    // ---------- Category names (RF-10) ----------

    [Theory]
    [InlineData(MenuCategory.Pizza, "Pizzas")]
    [InlineData(MenuCategory.Starter, "Entradas")]
    [InlineData(MenuCategory.Drink, "Bebidas")]
    [InlineData(MenuCategory.Dessert, "Postres")]
    public void ToDisplayName_ReturnsSpanishName_RF10(MenuCategory category, string expected)
    {
        Assert.Equal(expected, category.ToDisplayName());
    }

    [Fact]
    public void ToDisplayName_CoversEveryCategory_RF10()
    {
        Assert.All(Enum.GetValues<MenuCategory>(), category => Assert.NotEmpty(category.ToDisplayName()));
    }

    // ---------- Hero sticker (RF-07) ----------

    [Fact]
    public async Task Get_Root_WithSeededMenu_HeroShowsLowestPriceSticker_RF07()
    {
        using var factory = new PizzeriaWebFactory();

        var hero = HeroSection(await GetSeededHtmlAsync(factory));

        Assert.Contains("Desde $10.500", hero);
    }

    [Fact]
    public async Task Get_Root_StickerUsesTheLowestPriceOfTheWholeMenu_RF07()
    {
        using var factory = new PizzeriaWebFactory();
        await using (var context = factory.CreateDbContext())
        {
            context.MenuItems.AddRange(
                new MenuItem { Name = "Grande", Price = 12000m, Category = MenuCategory.Pizza },
                new MenuItem { Name = "Limonada", Price = 9500m, Category = MenuCategory.Drink });
            await context.SaveChangesAsync();
        }

        var hero = HeroSection(await GetHtmlAsync(factory));

        Assert.Contains("Desde $9.500", hero);
    }

    [Fact]
    public async Task Get_Root_WithEmptyMenu_HasNoSticker_AndShowsTheNoticeOnce_RF07()
    {
        using var factory = new PizzeriaWebFactory();

        var html = await GetHtmlAsync(factory);

        Assert.DoesNotContain("Desde", html);
        Assert.DoesNotContain("Pizzas destacadas", html);
        Assert.Single(Regex.Matches(html, Regex.Escape(UnavailableNotice)));
        Assert.Single(Regex.Matches(html, @"id=""menu"""));
    }

    // ---------- Featured (RF-09, S-8) ----------

    [Fact]
    public async Task Get_Root_WithSeededMenu_ShowsExactlyTheTwoFeaturedPizzas_RF09()
    {
        using var factory = new PizzeriaWebFactory();

        var featured = FeaturedSection(await GetSeededHtmlAsync(factory));

        Assert.Matches(@"<h2[^>]*>Pizzas destacadas</h2>", featured);
        var names = Regex.Matches(featured, @"<h3[^>]*>([^<]*)</h3>").Select(match => match.Groups[1].Value);
        Assert.Equal(["Carnes", "Mexicana"], names);
        Assert.DoesNotContain("Champiñón con pollo", featured);
    }

    [Fact]
    public async Task Get_Root_FeaturedCards_ShowImageDescriptionAndPrice_RF09()
    {
        using var factory = new PizzeriaWebFactory();

        var featured = FeaturedSection(await GetSeededHtmlAsync(factory));
        var images = Regex.Matches(featured, @"<img\b[^>]*>").Select(match => match.Value).ToList();

        Assert.Equal(2, images.Count);
        // Null ImagePath: the placeholder (with its static-asset fingerprint) and the pizza name as alt.
        Assert.All(images, image =>
        {
            Assert.Matches(@"src=""/img/pizza-placeholder(\.[a-z0-9]+)?\.svg""", image);
            Assert.Contains("loading=\"lazy\"", image);
        });
        Assert.Contains("alt=\"Carnes\"", images[0]);
        Assert.Contains("alt=\"Mexicana\"", images[1]);
        Assert.Contains("Carnes variadas sobre queso mozzarella y salsa de tomate.", featured);
        Assert.Contains("Carne molida, jalapeños y maíz sobre queso mozzarella.", featured);
        Assert.Equal(2, Regex.Matches(featured, Regex.Escape("$10.500")).Count);
    }

    [Fact]
    public async Task Get_Root_FeaturedCard_WithImagePath_UsesIt_RF09()
    {
        using var factory = new PizzeriaWebFactory();
        await using (var context = factory.CreateDbContext())
        {
            context.MenuItems.Add(new MenuItem
            {
                Name = "Hawaiana",
                Price = 11000m,
                Category = MenuCategory.Pizza,
                ImagePath = "img/hawaiana.webp",
                IsFeatured = true,
            });
            await context.SaveChangesAsync();
        }

        var featured = FeaturedSection(await GetHtmlAsync(factory));

        Assert.Matches(@"<img[^>]*src=""/img/hawaiana\.webp""[^>]*alt=""Hawaiana""", featured);
    }

    [Fact]
    public async Task Get_Root_WithoutFeaturedProducts_HidesTheFeaturedSection_S8()
    {
        using var factory = new PizzeriaWebFactory();
        await using (var context = factory.CreateDbContext())
        {
            context.MenuItems.Add(new MenuItem { Name = "Sencilla", Price = 8000m, Category = MenuCategory.Pizza });
            await context.SaveChangesAsync();
        }

        var html = await GetHtmlAsync(factory);

        Assert.DoesNotContain("Pizzas destacadas", html);
        Assert.Contains("Sencilla", MenuSection(html));
        Assert.DoesNotContain(UnavailableNotice, html);
    }

    // ---------- Menu (RF-10, RF-11, RF-12) ----------

    [Fact]
    public async Task Get_Root_WithSeededMenu_ListsPizzasByName_UnderTheOnlyCategoryWithProducts_RF10()
    {
        using var factory = new PizzeriaWebFactory();

        var html = await GetSeededHtmlAsync(factory);
        var menu = MenuSection(html);

        Assert.Matches(@"<h2[^>]*>Menú</h2>", menu);
        var categories = Regex.Matches(menu, @"<h3[^>]*>([^<]*)</h3>").Select(match => match.Groups[1].Value);
        Assert.Equal(["Pizzas"], categories);
        Assert.DoesNotContain("Entradas", html);
        Assert.DoesNotContain("Bebidas", html);
        Assert.DoesNotContain("Postres", html);

        var names = Regex.Matches(menu, @"<span class=""menu__name"">([^<]*)</span>").Select(match => match.Groups[1].Value);
        Assert.Equal(["Carnes", "Champiñón con pollo", "Mexicana"], names);
        Assert.Equal(3, Regex.Matches(menu, Regex.Escape("$10.500")).Count);
        Assert.Contains("Champiñones y pollo sobre queso mozzarella.", menu);
        Assert.DoesNotContain(UnavailableNotice, html);
    }

    [Fact]
    public async Task Get_Root_Menu_CategoriesFollowTheEnumOrder_RF10()
    {
        using var factory = new PizzeriaWebFactory();
        await using (var context = factory.CreateDbContext())
        {
            context.MenuItems.AddRange(
                new MenuItem { Name = "Flan", Price = 6000m, Category = MenuCategory.Dessert },
                new MenuItem { Name = "Limonada", Price = 5000m, Category = MenuCategory.Drink },
                new MenuItem { Name = "Carnes", Price = 10500m, Category = MenuCategory.Pizza });
            await context.SaveChangesAsync();
        }

        var menu = MenuSection(await GetHtmlAsync(factory));
        var categories = Regex.Matches(menu, @"<h3[^>]*>([^<]*)</h3>").Select(match => match.Groups[1].Value);

        Assert.Equal(["Pizzas", "Bebidas", "Postres"], categories);
    }

    [Fact]
    public async Task Get_Root_Menu_MarksOnlyFeaturedRows_RF11()
    {
        using var factory = new PizzeriaWebFactory();

        var menu = MenuSection(await GetSeededHtmlAsync(factory));
        var rows = Regex.Matches(menu, @"<li class=""menu__item"">[\s\S]*?</li>").Select(match => match.Value).ToList();

        Assert.Equal(3, rows.Count);
        Assert.Contains("Destacada", rows.Single(row => row.Contains(">Carnes<")));
        Assert.Contains("Destacada", rows.Single(row => row.Contains(">Mexicana<")));
        Assert.DoesNotContain("Destacada", rows.Single(row => row.Contains(">Champiñón con pollo<")));
    }

    [Fact]
    public async Task Get_Root_WithSeededMenu_EveryImageHasAltWidthAndHeight_RNF14()
    {
        using var factory = new PizzeriaWebFactory();

        var html = await GetSeededHtmlAsync(factory);
        var images = Regex.Matches(html, @"<img\b[^>]*>").Select(match => match.Value).ToList();

        Assert.Equal(3, images.Count);
        Assert.All(images, image =>
        {
            Assert.Contains(" alt=\"", image);
            Assert.Matches(@" width=""\d+""", image);
            Assert.Matches(@" height=""\d+""", image);
        });
        Assert.DoesNotContain(" style=", html);
        Assert.DoesNotContain("<script", html);
    }

    // ---------- Database down (RF-26) ----------

    [Fact]
    public async Task Get_Root_WhenMenuServiceFails_Returns200_WithTheNoticeOnce_RF26()
    {
        using var factory = FactoryWithFailingMenu();

        var html = await GetHtmlAsync(factory);

        Assert.Single(Regex.Matches(html, Regex.Escape(UnavailableNotice)));
        Assert.DoesNotContain("Pizzas destacadas", html);
        Assert.DoesNotContain("menu__row", html);
        Assert.DoesNotContain("Desde", html);
    }

    [Fact]
    public async Task Get_Root_WhenMenuServiceFails_KeepsTheRestOfThePage_RF26()
    {
        using var factory = FactoryWithFailingMenu();

        var html = await GetHtmlAsync(factory);

        Assert.Matches(@"<h1[^>]*>Harry's Pizza</h1>", html);
        Assert.Matches(@"<h2[^>]*>Sobre nosotros</h2>", html);
        Assert.Contains("id=\"horarios\"", html);
        Assert.Contains("Medellín, Antioquia", html);
        Assert.Matches(@"<footer[\s\S]*Harry's Pizza[\s\S]*</footer>", html);
        Assert.True(Regex.Matches(html, @"href=""https://wa\.me/573113706576\?text=").Count >= 4);
    }

    [Fact]
    public async Task Get_Root_WhenMenuServiceFails_ShowsNoTechnicalDetails_AndLogsTheFailure_RF26()
    {
        var logs = new RecordingLoggerProvider();
        using var factory = FactoryWithFailingMenu(logs);

        var html = await GetHtmlAsync(factory);

        Assert.DoesNotContain(FailingMenuService.FailureDetail, html);
        Assert.DoesNotContain("Exception", html);
        Assert.DoesNotContain("sql-test-host", html);
        Assert.DoesNotContain("   at ", html);
        Assert.Contains(logs.Entries, entry =>
            entry.Level == LogLevel.Error && entry.Text.Contains("menu", StringComparison.OrdinalIgnoreCase));
    }
}
