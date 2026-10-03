using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Pizzeria.Data;
using Pizzeria.Models;
using Pizzeria.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Antiforgery stays on (Razor Pages validates every POST). The cookie is Secure whenever the
// request is HTTPS, which is always the case outside local HTTP runs.
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    // X-Frame-Options is set to DENY for every response by the security headers middleware below.
    options.SuppressXFrameOptionsHeader = true;
});

// Spam protection for the public form: only POST requests count, 5 per fixed 10-minute window per
// client IP (valid, invalid and honeypot submissions alike). GET requests are never limited.
// Note for deployment: behind the Azure proxy every request shares one IP until ForwardedHeaders is configured.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        if (!HttpMethods.IsPost(context.Request.Method))
        {
            return RateLimitPartition.GetNoLimiter("not-post");
        }

        // The IP is only a partition key held in memory; it is never stored or logged.
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(10),
            QueueLimit = 0,
        });
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        var response = context.HttpContext.Response;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        response.ContentType = "text/plain; charset=utf-8";
        await response.WriteAsync(
            "Has enviado demasiados mensajes seguidos. Espera unos minutos e inténtalo de nuevo, o escríbenos por WhatsApp.",
            cancellationToken);
    };
});

// The connection string is resolved from the final configuration (user-secrets locally,
// App Settings in Azure). It never has a default value in code or in appsettings*.json.
builder.Services.AddDbContext<AppDbContext>((services, options) =>
    options.UseSqlServer(
        GetRequiredConnectionString(services.GetRequiredService<IConfiguration>()),
        // Bounded retries so an unavailable database does not leave the page hanging.
        sqlOptions => sqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)));

// Public business data (name, phone, hours...). Not secrets: they live in appsettings.json.
builder.Services.Configure<BusinessInfo>(builder.Configuration.GetSection(BusinessInfo.SectionName));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<MenuService>();
builder.Services.AddScoped<ContactService>();

var app = builder.Build();

// Fail fast at startup with a clear message when the connection string is missing.
GetRequiredConnectionString(app.Configuration);

// Sample data only in Development. Migrations are never applied at startup.
if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DbSeeder));
    await DbSeeder.SeedAsync(context, logger);
}

// Security headers on every response, including static files, error pages and 429 answers.
// The CSP allows only same-origin resources: the site has no inline styles or scripts.
var contentSecurityPolicy =
    "default-src 'self'; img-src 'self' data:; style-src 'self'; script-src 'self'; " +
    "frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

if (app.Environment.IsDevelopment())
{
    // Hot reload (dotnet watch / browser refresh) talks to the tooling over a WebSocket on another port.
    contentSecurityPolicy += "; connect-src 'self' ws: wss:";
}

app.Use(async (context, next) =>
{
    // OnStarting keeps the headers even when the exception handler clears the response.
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers.XFrameOptions = "DENY";
        headers.ContentSecurityPolicy = contentSecurityPolicy;
        return Task.CompletedTask;
    });

    await next(context);
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

// After routing, before the endpoints: a rejected POST never reaches the page handler.
app.UseRateLimiter();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

static string GetRequiredConnectionString(IConfiguration configuration)
{
    var connectionString = configuration.GetConnectionString("Default");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "Falta la cadena de conexión 'ConnectionStrings:Default'. " +
            "En local, configúrala con: " +
            "dotnet user-secrets set \"ConnectionStrings:Default\" \"<cadena>\" --project src/Pizzeria");
    }

    return connectionString;
}
