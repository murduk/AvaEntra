using System.Text;
using AvaEntra.Server.Data;

namespace AvaEntra.Server.Identity;

public static class TokenEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/{tenant}/oauth2/v2.0/token", Issue);
        app.MapPost("/oauth2/v2.0/token", Issue);
    }

    private static async Task<IResult> Issue(HttpContext http, DirectoryStore store, TokenService tokens, CodeStore codes, string? tenant)
    {
        _ = tenant;
        var form = await http.Request.ReadFormAsync();
        var grant = form["grant_type"].ToString();
        var dir = store.Snapshot;

        var (client, clientError) = AuthenticateClient(http, dir, form["client_id"].ToString(), form["client_secret"].ToString());
        if (client is null)
            return IdentityEndpoints.TokenError("invalid_client", clientError ?? "AADSTS700016: Application not found.");

        return grant switch
        {
            "authorization_code" => await AuthCode(http, store, tokens, codes, dir, client, form),
            "refresh_token" => await Refresh(store, tokens, dir, client, form),
            "client_credentials" => ClientCredentials(store, tokens, dir, client, form),
            "password" => await Password(store, tokens, dir, client, form),
            "urn:ietf:params:oauth:grant-type:jwt-bearer" => await OnBehalfOf(store, tokens, dir, client, form),
            "on_behalf_of" => await OnBehalfOf(store, tokens, dir, client, form),
            _ => IdentityEndpoints.TokenError("unsupported_grant_type", $"AADSTS70003: The grant type '{grant}' is not supported.")
        };
    }

    private static async Task<IResult> AuthCode(
        HttpContext http, DirectoryStore store, TokenService tokens, CodeStore codes,
        DirectorySnapshot dir, Application client, IFormCollection form)
    {
        var code = form["code"].ToString();
        var redirectUri = form["redirect_uri"].ToString();
        var verifier = form["code_verifier"].ToString();
        var stored = codes.Consume(code);
        if (stored is null)
            return Fail(store, client, "authorization_code", "invalid_grant", "AADSTS70008: The provided authorization code has expired or is invalid.");
        if (stored.ClientId != client.ClientId)
            return Fail(store, client, "authorization_code", "invalid_grant", "AADSTS70000: The authorization code was issued to another client.");
        if (!string.Equals(stored.RedirectUri, redirectUri, StringComparison.Ordinal))
            return Fail(store, client, "authorization_code", "invalid_grant", "AADSTS50011: The redirect URI does not match the original authorize request.");

        if (!string.IsNullOrEmpty(stored.CodeChallenge))
        {
            if (string.IsNullOrEmpty(verifier) || !Crypto.VerifyPkce(verifier, stored.CodeChallenge, stored.CodeChallengeMethod))
                return Fail(store, client, "authorization_code", "invalid_grant", "AADSTS50148: The code_verifier does not match the code_challenge.");
        }
        else if (client.RequirePkce)
        {
            return Fail(store, client, "authorization_code", "invalid_grant", "AADSTS9002325: PKCE is required for this application.");
        }

        var user = dir.User(stored.UserId);
        if (user is null || !user.Enabled)
            return Fail(store, client, "authorization_code", "invalid_grant", "AADSTS50034: The user account does not exist or is disabled.");

        return await TokensForUser(store, tokens, dir, client, user, stored.Scope, stored.Nonce, "authorization_code", "0");
    }

    private static async Task<IResult> Refresh(DirectoryStore store, TokenService tokens, DirectorySnapshot dir, Application client, IFormCollection form)
    {
        var raw = form["refresh_token"].ToString();
        if (string.IsNullOrEmpty(raw))
            return Fail(store, client, "refresh_token", "invalid_grant", "AADSTS900144: The refresh_token parameter is missing.");

        var hash = Crypto.HashSecret(raw);
        RefreshToken? record = null;
        await store.UpdateAsync(d =>
        {
            record = d.RefreshTokens.FirstOrDefault(t => t.TokenHash == hash && !t.Revoked);
            if (record is not null) record.Revoked = true;
        });

        if (record is null || record.ExpiresAt < DateTimeOffset.UtcNow)
            return Fail(store, client, "refresh_token", "invalid_grant", "AADSTS700082: The refresh token has expired or been revoked.");
        if (record.ClientId != client.ClientId)
            return Fail(store, client, "refresh_token", "invalid_grant", "AADSTS70000: The refresh token was issued to another client.");

        var user = dir.User(record.UserId);
        if (user is null || !user.Enabled)
            return Fail(store, client, "refresh_token", "invalid_grant", "AADSTS50034: The user account does not exist or is disabled.");

        var scope = string.IsNullOrWhiteSpace(form["scope"]) ? record.Scope : form["scope"].ToString();
        return await TokensForUser(store, tokens, dir, client, user, scope, null, "refresh_token", "0");
    }

    private static IResult ClientCredentials(DirectoryStore store, TokenService tokens, DirectorySnapshot dir, Application client, IFormCollection form)
    {
        if (client.IsPublicClient || !client.AllowClientCredentials)
            return Fail(store, client, "client_credentials", "unauthorized_client", "AADSTS700016: This application is not allowed to use the client credentials flow.");

        var parsed = ScopeParser.Parse(dir, form["scope"]);
        var at = tokens.CreateAppAccessToken(dir, client, parsed.Resource);
        var aud = parsed.Resource?.IdentifierUri ?? parsed.Resource?.ClientId.ToString();
        store.LogSignIn(new SignInLog
        {
            ClientId = client.ClientId,
            GrantType = "client_credentials",
            Scopes = form["scope"],
            Audience = aud,
            Success = true
        });
        return Results.Json(new
        {
            token_type = "Bearer",
            expires_in = tokens.AccessLifetime,
            ext_expires_in = tokens.AccessLifetime,
            access_token = at
        });
    }

    private static async Task<IResult> Password(DirectoryStore store, TokenService tokens, DirectorySnapshot dir, Application client, IFormCollection form)
    {
        var username = form["username"].ToString();
        var password = form["password"].ToString();
        var user = dir.UserByUpn(username);
        if (user is null || !user.Enabled || !Crypto.VerifyPassword(password, user.PasswordHash))
            return Fail(store, client, "password", "invalid_grant", "AADSTS50126: Invalid username or password.", user?.Id);

        var scope = form["scope"].ToString();
        if (string.IsNullOrWhiteSpace(scope)) scope = "openid profile email offline_access";
        return await TokensForUser(store, tokens, dir, client, user, scope, null, "password", client.IsPublicClient ? "0" : "1");
    }

    private static async Task<IResult> OnBehalfOf(DirectoryStore store, TokenService tokens, DirectorySnapshot dir, Application client, IFormCollection form)
    {
        if (!client.AllowOnBehalfOf)
            return Fail(store, client, "on_behalf_of", "unauthorized_client", "AADSTS50013: This application is not allowed to use the on-behalf-of flow.");

        var use = form["requested_token_use"].ToString();
        if (!string.IsNullOrEmpty(use) && !use.Equals("on_behalf_of", StringComparison.OrdinalIgnoreCase))
            return Fail(store, client, "on_behalf_of", "invalid_request", "AADSTS90023: requested_token_use must be 'on_behalf_of'.");

        var assertion = form["assertion"].ToString();
        if (string.IsNullOrEmpty(assertion))
            return Fail(store, client, "on_behalf_of", "invalid_request", "AADSTS900144: The assertion parameter is missing.");

        var payload = tokens.Validate(assertion);
        if (payload is null)
            return Fail(store, client, "on_behalf_of", "invalid_grant", "AADSTS50013: Assertion is invalid or expired.");

        var oid = payload["oid"]?.ToString() ?? payload["sub"]?.ToString();
        if (!Guid.TryParse(oid, out var userId) || dir.User(userId) is not { Enabled: true } user)
            return Fail(store, client, "on_behalf_of", "invalid_grant", "AADSTS50034: The user in the assertion does not exist.");

        var scope = form["scope"].ToString();
        if (string.IsNullOrWhiteSpace(scope))
            return Fail(store, client, "on_behalf_of", "invalid_request", "AADSTS900144: The scope parameter is missing.");

        return await TokensForUser(store, tokens, dir, client, user, scope, null, "on_behalf_of", "1", includeId: false);
    }

    private static async Task<IResult> TokensForUser(
        DirectoryStore store,
        TokenService tokens,
        DirectorySnapshot dir,
        Application client,
        DirectoryUser user,
        string scope,
        string? nonce,
        string grant,
        string azpacr,
        bool includeId = true)
    {
        var parsed = ScopeParser.Parse(dir, scope);
        var at = tokens.CreateUserAccessToken(dir, user, client, parsed.Resource, parsed.Delegated, azpacr);
        string? idToken = null;
        if (includeId && parsed.Oidc.Contains("openid", StringComparer.OrdinalIgnoreCase))
            idToken = tokens.CreateIdToken(dir, user, client, nonce);

        string? refresh = null;
        if (parsed.OfflineAccess || grant is "refresh_token" or "authorization_code")
        {
            var created = tokens.CreateRefreshToken(client.ClientId, user.Id, scope);
            refresh = created.token;
            await store.UpdateAsync(d => d.RefreshTokens.Add(created.record));
        }

        var aud = parsed.Resource?.IdentifierUri ?? parsed.Resource?.ClientId.ToString();
        store.LogSignIn(new SignInLog
        {
            UserId = user.Id,
            ClientId = client.ClientId,
            GrantType = grant,
            Scopes = scope,
            Audience = aud,
            Success = true
        });

        var granted = string.Join(' ', parsed.Oidc.Concat(parsed.Delegated.Select(s =>
            parsed.Resource is null ? s : $"{parsed.Resource.IdentifierUri ?? parsed.Resource.ClientId.ToString()}/{s}")));

        var body = new Dictionary<string, object?>
        {
            ["token_type"] = "Bearer",
            ["scope"] = granted,
            ["expires_in"] = tokens.AccessLifetime,
            ["ext_expires_in"] = tokens.AccessLifetime,
            ["access_token"] = at
        };
        if (idToken is not null) body["id_token"] = idToken;
        if (refresh is not null) body["refresh_token"] = refresh;
        return Results.Json(body);
    }

    private static IResult Fail(DirectoryStore store, Application client, string grant, string error, string description, Guid? userId = null)
    {
        store.LogSignIn(new SignInLog
        {
            UserId = userId,
            ClientId = client.ClientId,
            GrantType = grant,
            Success = false,
            Error = description
        });
        return IdentityEndpoints.TokenError(error, description);
    }

    private static (Application? client, string? error) AuthenticateClient(HttpContext http, DirectorySnapshot dir, string clientIdField, string secretField)
    {
        string? headerId = null;
        string? headerSecret = null;
        var auth = http.Request.Headers.Authorization.ToString();
        if (auth.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(auth["Basic ".Length..].Trim()));
                var i = decoded.IndexOf(':');
                if (i >= 0)
                {
                    headerId = decoded[..i];
                    headerSecret = decoded[(i + 1)..];
                }
            }
            catch { /* ignore malformed basic auth */ }
        }

        var clientId = headerId ?? clientIdField;
        var secret = headerSecret ?? secretField;
        var client = dir.AppByClientId(clientId);
        if (client is null) return (null, "AADSTS700016: Application not found in the directory.");

        if (client.IsPublicClient)
            return (client, null);

        if (!dir.VerifyClientSecret(client, secret))
            return (null, "AADSTS7000215: Invalid client secret provided.");

        return (client, null);
    }
}
