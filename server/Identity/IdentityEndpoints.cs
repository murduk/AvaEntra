using System.Security.Claims;
using System.Text.Json.Nodes;
using AvaEntra.Server.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace AvaEntra.Server.Identity;

public static class IdentityEndpoints
{
    public static void MapIdentityEndpoints(this WebApplication app)
    {
        app.MapGet("/.well-known/openid-configuration", Discovery);
        app.MapGet("/{tenant}/v2.0/.well-known/openid-configuration", Discovery);
        app.MapGet("/{tenant}/.well-known/openid-configuration", Discovery);
        app.MapGet("/{tenant}/discovery/v2.0/keys", Keys);
        app.MapGet("/discovery/v2.0/keys", Keys);

        app.MapGet("/{tenant}/oauth2/v2.0/authorize", Authorize);
        app.MapGet("/oauth2/v2.0/authorize", Authorize);
        app.MapGet("/{tenant}/oauth2/v2.0/logout", Logout);
        app.MapGet("/oauth2/v2.0/logout", Logout);

        app.MapGet("/login", LoginGet);
        app.MapPost("/login", LoginPost);

        app.MapGet("/dev/callback", DevCallback);

        TokenEndpoints.Map(app);
    }

    private static IResult Discovery(HttpContext http, DirectoryStore store, TokenService tokens, string? tenant)
    {
        var t = ResolveTenant(store.Snapshot, tenant);
        var origin = tokens.Origin;
        var tid = t.Id.ToString();
        var basePath = $"{origin}/{tid}";
        var doc = new JsonObject
        {
            ["token_endpoint"] = $"{basePath}/oauth2/v2.0/token",
            ["token_endpoint_auth_methods_supported"] = new JsonArray("client_secret_post", "client_secret_basic", "none"),
            ["jwks_uri"] = $"{basePath}/discovery/v2.0/keys",
            ["response_modes_supported"] = new JsonArray("query", "fragment", "form_post"),
            ["subject_types_supported"] = new JsonArray("pairwise"),
            ["id_token_signing_alg_values_supported"] = new JsonArray("RS256"),
            ["response_types_supported"] = new JsonArray("code", "id_token", "code id_token"),
            ["scopes_supported"] = new JsonArray("openid", "profile", "email", "offline_access"),
            ["issuer"] = tokens.Issuer(t.Id),
            ["request_uri_parameter_supported"] = false,
            ["userinfo_endpoint"] = $"{origin}/v1.0/me",
            ["authorization_endpoint"] = $"{basePath}/oauth2/v2.0/authorize",
            ["http_logout_supported"] = true,
            ["frontchannel_logout_supported"] = true,
            ["end_session_endpoint"] = $"{basePath}/oauth2/v2.0/logout",
            ["claims_supported"] = new JsonArray("sub", "iss", "aud", "exp", "iat", "name", "oid", "preferred_username", "tid", "roles", "groups", "scp", "email"),
            ["tenant_region_scope"] = "NA",
            ["cloud_instance_name"] = "avaentra.local",
            ["msgraph_host"] = origin.Replace("http://", "").Replace("https://", ""),
            ["code_challenge_methods_supported"] = new JsonArray("plain", "S256"),
            ["grant_types_supported"] = new JsonArray(
                "authorization_code", "refresh_token", "client_credentials", "password",
                "urn:ietf:params:oauth:grant-type:jwt-bearer")
        };
        return Results.Json(doc);
    }

    private static IResult Keys(SigningKeyService keys, string? tenant)
    {
        _ = tenant;
        return Results.Json(new JsonObject { ["keys"] = new JsonArray(keys.Jwk()) });
    }

    private static async Task<IResult> Authorize(HttpContext http, DirectoryStore store, TokenService tokens, CodeStore codes, IOptions<AvaEntraOptions> options, string? tenant)
    {
        var q = http.Request.Query;
        var clientId = q["client_id"].ToString();
        var redirectUri = q["redirect_uri"].ToString();
        var responseType = q["response_type"].ToString();
        var responseMode = q["response_mode"].ToString();
        var scope = q["scope"].ToString();
        var state = q["state"].ToString();
        var nonce = q["nonce"].ToString();
        var challenge = q["code_challenge"].ToString();
        var method = q["code_challenge_method"].ToString();
        var prompt = q["prompt"].ToString();
        var loginHint = q["login_hint"].ToString();

        var dir = store.Snapshot;
        var client = dir.AppByClientId(clientId);
        if (client is null)
            return Results.Content(ErrorHtml("AADSTS700016: Application not found in the directory."), "text/html", statusCode: 400);

        if (string.IsNullOrEmpty(redirectUri) || !dir.IsRedirectAllowed(client, redirectUri))
            return Results.Content(ErrorHtml($"AADSTS50011: The redirect URI '{redirectUri}' does not match a registered URI for this client."), "text/html", statusCode: 400);

        if (string.IsNullOrWhiteSpace(responseType) || !responseType.Contains("code", StringComparison.OrdinalIgnoreCase))
            return RedirectError(redirectUri, responseMode, "unsupported_response_type", "AADSTS700054: response_type 'code' is required.", state);

        if (client.RequirePkce && string.IsNullOrEmpty(challenge))
            return RedirectError(redirectUri, responseMode, "invalid_request", "AADSTS9002325: Proof Key for Code Exchange is required for this application.", state);

        var needsLogin = prompt.Contains("login", StringComparison.OrdinalIgnoreCase)
                         || prompt.Contains("select_account", StringComparison.OrdinalIgnoreCase)
                         || http.User.Identity?.IsAuthenticated != true;

        if (prompt.Contains("none", StringComparison.OrdinalIgnoreCase) && http.User.Identity?.IsAuthenticated != true)
            return RedirectError(redirectUri, responseMode, "login_required", "AADSTS50058: A session for this user has not been established.", state);

        if (needsLogin)
        {
            if (prompt.Contains("none", StringComparison.OrdinalIgnoreCase))
                return RedirectError(redirectUri, responseMode, "login_required", "AADSTS50058: A session for this user has not been established.", state);

            var returnUrl = http.Request.Path + http.Request.QueryString;
            var loginUrl = $"/login?returnUrl={Uri.EscapeDataString(returnUrl)}&login_hint={Uri.EscapeDataString(loginHint)}";
            return Results.Redirect(loginUrl);
        }

        var oid = http.User.FindFirstValue("oid");
        if (!Guid.TryParse(oid, out var userId) || dir.User(userId) is not { Enabled: true } user)
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var returnUrl = http.Request.Path + http.Request.QueryString;
            return Results.Redirect($"/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        var code = new AuthorizationCode
        {
            Code = Crypto.NewToken(32),
            ClientId = client.ClientId,
            UserId = user.Id,
            RedirectUri = redirectUri,
            Scope = string.IsNullOrWhiteSpace(scope) ? "openid profile offline_access" : scope,
            Nonce = string.IsNullOrEmpty(nonce) ? null : nonce,
            CodeChallenge = string.IsNullOrEmpty(challenge) ? null : challenge,
            CodeChallengeMethod = string.IsNullOrEmpty(method) ? "plain" : method,
            SessionId = http.User.FindFirstValue("sid"),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(options.Value.AuthorizationCodeLifetimeMinutes)
        };
        codes.Add(code);

        store.LogSignIn(new SignInLog
        {
            UserId = user.Id,
            ClientId = client.ClientId,
            GrantType = "authorization_code",
            Scopes = code.Scope,
            Success = true
        });

        return SendCode(redirectUri, responseMode, code.Code, state);
    }

    private static IResult LoginGet(HttpContext http, DirectoryStore store, IOptions<AvaEntraOptions> options)
    {
        var html = LoginPage.Render(
            store.Snapshot,
            options.Value,
            action: "/login" + http.Request.QueryString,
            loginHint: http.Request.Query["login_hint"]);
        return Results.Content(html, "text/html");
    }

    private static async Task<IResult> LoginPost(HttpContext http, DirectoryStore store, IOptions<AvaEntraOptions> options)
    {
        var form = await http.Request.ReadFormAsync();
        var dir = store.Snapshot;
        DirectoryUser? user = null;
        var error = "AADSTS50126: Invalid username or password.";

        if (options.Value.AllowPasswordlessDevLogin && Guid.TryParse(form["userId"], out var quickId))
        {
            user = dir.User(quickId) is { Enabled: true } u ? u : null;
            if (user is null) error = "That account is disabled or missing.";
        }
        else
        {
            var username = form["username"].ToString().Trim();
            var password = form["password"].ToString();
            user = dir.UserByUpn(username);
            if (user is null || !user.Enabled || !Crypto.VerifyPassword(password, user.PasswordHash))
                user = null;
        }

        var returnUrl = http.Request.Query["returnUrl"].ToString();
        if (string.IsNullOrEmpty(returnUrl) || !returnUrl.StartsWith('/') || returnUrl.StartsWith("//"))
            returnUrl = "/";

        if (user is null)
        {
            var html = LoginPage.Render(dir, options.Value, "/login" + http.Request.QueryString, error, form["username"]);
            return Results.Content(html, "text/html", statusCode: 401);
        }

        var claims = new List<Claim>
        {
            new("oid", user.Id.ToString()),
            new("tid", dir.Tenant.Id.ToString()),
            new("name", user.DisplayName),
            new("preferred_username", user.UserPrincipalName),
            new("sid", Crypto.NewToken(16)),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return Results.Redirect(returnUrl);
    }

    private static async Task<IResult> Logout(HttpContext http, DirectoryStore store, string? tenant)
    {
        _ = tenant;
        var post = http.Request.Query["post_logout_redirect_uri"].ToString();
        var clientId = http.Request.Query["client_id"].ToString();
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        if (!string.IsNullOrEmpty(post))
        {
            var app = store.Snapshot.AppByClientId(clientId);
            if (app is null || store.Snapshot.IsPostLogoutAllowed(app, post) || store.Snapshot.IsRedirectAllowed(app, post))
                return Results.Redirect(post);
        }

        return Results.Content("<!doctype html><html><body style=\"font-family:Segoe UI,sans-serif;padding:40px\"><h1>Signed out</h1><p>You can close this window.</p></body></html>", "text/html");
    }

    private static IResult DevCallback(HttpContext http)
    {
        var code = http.Request.Query["code"].ToString();
        var error = http.Request.Query["error"].ToString();
        var desc = http.Request.Query["error_description"].ToString();
        var html = $"""
            <!doctype html><html><head><meta charset="utf-8"/><title>AvaEntra callback</title></head>
            <body style="font-family:Segoe UI,sans-serif;padding:40px;max-width:720px">
            <h1>AvaEntra callback</h1>
            {(string.IsNullOrEmpty(error) ? $"<p>Authorization code received.</p><pre style='white-space:pre-wrap;word-break:break-all'>{System.Net.WebUtility.HtmlEncode(code)}</pre>" : $"<p style='color:#c50f1f'>{System.Net.WebUtility.HtmlEncode(error)}: {System.Net.WebUtility.HtmlEncode(desc)}</p>")}
            <p>This page is a registered redirect for the sample SPA so you can try the authorize endpoint from a browser.</p>
            </body></html>
            """;
        return Results.Content(html, "text/html");
    }

    public static Tenant ResolveTenant(DirectorySnapshot dir, string? tenant)
    {
        if (string.IsNullOrEmpty(tenant) || tenant is "common" or "organizations" or "consumers")
            return dir.Tenant;
        if (Guid.TryParse(tenant, out var id) && id == dir.Tenant.Id)
            return dir.Tenant;
        if (string.Equals(tenant, dir.Tenant.Domain, StringComparison.OrdinalIgnoreCase))
            return dir.Tenant;
        return dir.Tenant;
    }

    public static IResult TokenError(string error, string description, int status = 400) =>
        Results.Json(new { error, error_description = description }, statusCode: status);

    public static IResult RedirectError(string redirectUri, string? mode, string error, string description, string? state)
    {
        var parts = new Dictionary<string, string> { ["error"] = error, ["error_description"] = description };
        if (!string.IsNullOrEmpty(state)) parts["state"] = state;
        return SendParams(redirectUri, mode, parts);
    }

    public static IResult SendCode(string redirectUri, string? mode, string code, string? state)
    {
        var parts = new Dictionary<string, string> { ["code"] = code };
        if (!string.IsNullOrEmpty(state)) parts["state"] = state;
        return SendParams(redirectUri, mode, parts);
    }

    private static IResult SendParams(string redirectUri, string? mode, Dictionary<string, string> parts)
    {
        var qs = string.Join('&', parts.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
        if (string.Equals(mode, "fragment", StringComparison.OrdinalIgnoreCase))
            return Results.Redirect($"{redirectUri}#{qs}");
        if (string.Equals(mode, "form_post", StringComparison.OrdinalIgnoreCase))
        {
            var inputs = string.Join("", parts.Select(kv =>
                $"<input type=\"hidden\" name=\"{kv.Key}\" value=\"{System.Net.WebUtility.HtmlEncode(kv.Value)}\"/>"));
            var html = $"<!doctype html><html><body onload=\"document.forms[0].submit()\"><form method=\"post\" action=\"{System.Net.WebUtility.HtmlEncode(redirectUri)}\">{inputs}</form></body></html>";
            return Results.Content(html, "text/html");
        }

        var sep = redirectUri.Contains('?') ? '&' : '?';
        return Results.Redirect($"{redirectUri}{sep}{qs}");
    }

    private static string ErrorHtml(string message) =>
        $"<!doctype html><html><body style=\"font-family:Segoe UI,sans-serif;padding:40px\"><h1>AvaEntra</h1><p>{System.Net.WebUtility.HtmlEncode(message)}</p></body></html>";
}
