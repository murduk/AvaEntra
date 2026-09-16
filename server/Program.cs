using AvaEntra.Server;
using AvaEntra.Server.Admin;
using AvaEntra.Server.Data;
using AvaEntra.Server.Graph;
using AvaEntra.Server.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

var httpsCert = DevHttpsCertificate.Ensure(builder.Environment.ContentRootPath);
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureHttpsDefaults(https => https.ServerCertificate = httpsCert);
});

builder.Services.Configure<AvaEntraOptions>(builder.Configuration.GetSection(AvaEntraOptions.SectionName));
builder.Services.AddSingleton<DirectoryStore>();
builder.Services.AddSingleton<CodeStore>();
builder.Services.AddSingleton<SigningKeyService>();
builder.Services.AddSingleton<TokenService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.Name = "AvaEntra.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    })
    .AddCookie(AdminAuth.Scheme, options =>
    {
        options.Cookie.Name = AdminAuth.CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AdminAuth.Policy, policy =>
        policy.AddAuthenticationSchemes(AdminAuth.Scheme)
            .RequireAuthenticatedUser()
            .RequireClaim(AdminAuth.ClaimType, "true"));
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyHeader()
        .AllowAnyMethod()
        .SetIsOriginAllowed(_ => true)
        .AllowCredentials()));

var app = builder.Build();

var opts = app.Services.GetRequiredService<IOptions<AvaEntraOptions>>().Value;
if (string.IsNullOrWhiteSpace(opts.AdminUsername) || string.IsNullOrWhiteSpace(opts.AdminPassword))
    throw new InvalidOperationException("AvaEntra:AdminUsername and AvaEntra:AdminPassword must be set (env AvaEntra__AdminUsername / AvaEntra__AdminPassword).");

app.Services.GetRequiredService<DirectoryStore>().Initialize();
app.Services.GetRequiredService<SigningKeyService>().Initialize();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapIdentityEndpoints();
app.MapAdminEndpoints();
app.MapGraphEndpoints();
app.MapFallbackToFile("index.html");

app.Logger.LogInformation(
    "HTTPS enabled with local certificate at storage/https-dev.pfx (browsers will warn until trusted).");

app.Run();
