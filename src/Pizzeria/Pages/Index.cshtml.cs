using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Pizzeria.Models;
using Pizzeria.Services;

namespace Pizzeria.Pages;

/// <summary>Landing page. Its section partials receive this model.</summary>
public class IndexModel(
    IOptions<BusinessInfo> businessOptions,
    MenuService menuService,
    ContactService contactService,
    ILogger<IndexModel> logger) : PageModel
{
    /// <summary>TempData key of the one-time "message sent" confirmation.</summary>
    public const string ContactOkKey = "ContactOk";

    /// <summary>Id of the contact section, used as the URL fragment of the form and of the redirect.</summary>
    public const string ContactFragment = "contacto";

    /// <summary>Business data shown by the hero, hours and location sections.</summary>
    public BusinessInfo Business { get; } = businessOptions.Value;

    /// <summary>Contact form input. Bound only on POST; the view never binds the entity.</summary>
    [BindProperty]
    public ContactInput Contact { get; set; } = new();

    /// <summary>Menu grouped by category; empty when there are no products or the menu could not be read.</summary>
    public IReadOnlyList<MenuCategoryGroup> Menu { get; private set; } = [];

    /// <summary>Featured products; empty hides the featured section.</summary>
    public IReadOnlyList<MenuItem> Featured { get; private set; } = [];

    /// <summary>Lowest price of the menu, shown by the hero sticker; null hides the sticker.</summary>
    public decimal? MinPrice { get; private set; }

    /// <summary>True when there is at least one product to show.</summary>
    public bool HasMenu => Menu.Count > 0;

    /// <summary>True on the first page load after a message was accepted.</summary>
    public bool ContactSent { get; private set; }

    /// <summary>True when a valid message could not be stored.</summary>
    public bool ContactSaveFailed { get; private set; }

    /// <summary>Renders the landing page.</summary>
    /// <param name="cancellationToken">Cancels the menu queries when the request is aborted.</param>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        // Reading the entry marks it for removal: the confirmation is shown once.
        ContactSent = TempData[ContactOkKey] is true;

        await LoadMenuAsync(cancellationToken);
    }

    /// <summary>Handles the contact form. Antiforgery is validated by Razor Pages before this runs.</summary>
    /// <param name="cancellationToken">Cancels the save when the request is aborted.</param>
    /// <returns>A redirect to the contact section on success, or the page with errors.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        // Honeypot first (S-5): a filled trap field gets the same answer as a real message, and nothing is stored.
        if (!string.IsNullOrEmpty(Contact.Website))
        {
            logger.LogInformation("Contact form submission discarded by the honeypot.");
            return RedirectToContactSection();
        }

        if (!ModelState.IsValid)
        {
            await LoadMenuAsync(cancellationToken);
            return Page();
        }

        try
        {
            await contactService.SaveAsync(Contact, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Only exception types are logged: provider messages can echo the submitted values.
            logger.LogError(
                "A contact message could not be saved ({ExceptionType}, inner {InnerExceptionType}).",
                ex.GetType().Name,
                ex.InnerException?.GetType().Name ?? "none");

            ContactSaveFailed = true;
            await LoadMenuAsync(cancellationToken);
            return Page();
        }

        return RedirectToContactSection();
    }

    /// <summary>Post/Redirect/Get: the confirmation travels in TempData and reloading does not resend the form.</summary>
    private RedirectToPageResult RedirectToContactSection()
    {
        TempData[ContactOkKey] = true;
        return RedirectToPage("/Index", pageHandler: null, routeValues: null, fragment: ContactFragment);
    }

    /// <summary>
    /// Reads the menu. A database failure is logged and leaves the menu empty, so the rest of the
    /// page (hero, hours, contact, WhatsApp links) is still served.
    /// </summary>
    private async Task LoadMenuAsync(CancellationToken cancellationToken)
    {
        try
        {
            var menu = await menuService.GetMenuByCategoryAsync(cancellationToken);
            var featured = await menuService.GetFeaturedAsync(cancellationToken);
            var minPrice = await menuService.GetMinPriceAsync(cancellationToken);

            Menu = menu;
            Featured = featured;
            MinPrice = minPrice;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Menu data is public; the exception carries no personal data.
            logger.LogError(ex, "The menu could not be loaded; the page is served without it.");
        }
    }
}
