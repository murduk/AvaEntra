using AvaEntra.Server;
using AvaEntra.Server.Admin;
using AvaEntra.Server.Data;
using AvaEntra.Server.Graph;
using AvaEntra.Server.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AvaEntraOptions>(builder.Configuration.GetSection(AvaEntraOptions.SectionName));
builder.Services.AddSingleton<DirectoryStore>();
builder.Services.AddSingleton<CodeStore>();
builder.Services.AddSingleton<SigningKeyService>();
builder.Services.AddSingleton<TokenService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "AvaEntra.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
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
builder.Services.AddAuthorization();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyHeader()
        .AllowAnyMethod()
        .SetIsOriginAllowed(_ => true)
        .AllowCredentials()));

var app = builder.Build();

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

app.Run();
