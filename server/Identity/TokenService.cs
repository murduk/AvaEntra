using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using AvaEntra.Server.Data;
using Microsoft.Extensions.Options;

namespace AvaEntra.Server.Identity;

public sealed class TokenService
{
    private static readonly JsonSerializerOptions JwtJson = new()
    {
        PropertyNamingPolicy = null
    };

    private readonly SigningKeyService _keys;
    private readonly AvaEntraOptions _options;

    public TokenService(SigningKeyService keys, IOptions<AvaEntraOptions> options)
    {
        _keys = keys;
        _options = options.Value;
    }

    public string Origin => _options.PublicOrigin.TrimEnd('/');
    public string Issuer(Guid tenantId) => $"{Origin}/{tenantId}/v2.0";
    public int AccessLifetime => _options.AccessTokenLifetimeMinutes * 60;
    public int IdLifetime => _options.IdTokenLifetimeMinutes * 60;

    public string CreateUserAccessToken(
        DirectorySnapshot dir,
        DirectoryUser user,
        Application client,
        Application? resource,
        IEnumerable<string> delegatedScopes,
        string? azpacr = "0")
    {
        var now = DateTimeOffset.UtcNow;
        var aud = resource?.IdentifierUri ?? resource?.ClientId.ToString() ?? client.ClientId.ToString();
        var payload = BasePayload(dir.Tenant.Id, now, AccessLifetime, aud);
        payload["oid"] = user.Id.ToString();
        payload["sub"] = user.Id.ToString();
        payload["name"] = user.DisplayName;
        payload["preferred_username"] = user.UserPrincipalName;
        if (!string.IsNullOrEmpty(user.Mail)) payload["email"] = user.Mail;
        payload["azp"] = client.ClientId.ToString();
        payload["azpacr"] = azpacr ?? "0";
        payload["appid"] = client.ClientId.ToString();
        payload["idtyp"] = "user";
        payload["sid"] = Crypto.NewToken(16);

        var scp = string.Join(' ', delegatedScopes);
        if (!string.IsNullOrEmpty(scp)) payload["scp"] = scp;

        var groups = dir.GroupsFor(user.Id).Select(g => g.Id.ToString()).ToList();
        if (groups.Count > 0)
            payload["groups"] = ToArray(groups);

        if (resource is not null)
        {
            var roles = dir.RoleValuesFor(user.Id, resource.Id).ToList();
            if (roles.Count > 0) payload["roles"] = ToArray(roles);
        }

        return Sign(payload);
    }

    public string CreateAppAccessToken(DirectorySnapshot dir, Application client, Application? resource)
    {
        var now = DateTimeOffset.UtcNow;
        var aud = resource?.IdentifierUri ?? resource?.ClientId.ToString() ?? client.ClientId.ToString();
        var payload = BasePayload(dir.Tenant.Id, now, AccessLifetime, aud);
        payload["oid"] = client.Id.ToString();
        payload["sub"] = client.Id.ToString();
        payload["azp"] = client.ClientId.ToString();
        payload["azpacr"] = "1";
        payload["appid"] = client.ClientId.ToString();
        payload["idtyp"] = "app";

        if (resource is not null)
        {
            var roles = dir.AppRoles
                .Where(r => r.ApplicationId == resource.Id && r.AllowedApplication)
                .Select(r => r.Value)
                .ToList();
            if (roles.Count > 0) payload["roles"] = ToArray(roles);
        }

        return Sign(payload);
    }

    public string CreateIdToken(DirectorySnapshot dir, DirectoryUser user, Application client, string? nonce)
    {
        var now = DateTimeOffset.UtcNow;
        var payload = BasePayload(dir.Tenant.Id, now, IdLifetime, client.ClientId.ToString());
        payload["oid"] = user.Id.ToString();
        payload["sub"] = user.Id.ToString();
        payload["name"] = user.DisplayName;
        payload["preferred_username"] = user.UserPrincipalName;
        if (!string.IsNullOrEmpty(user.Mail)) payload["email"] = user.Mail;
        if (!string.IsNullOrEmpty(user.GivenName)) payload["given_name"] = user.GivenName;
        if (!string.IsNullOrEmpty(user.Surname)) payload["family_name"] = user.Surname;
        if (!string.IsNullOrEmpty(nonce)) payload["nonce"] = nonce;
        payload["rh"] = Crypto.NewToken(12);
        return Sign(payload);
    }

    public (string token, RefreshToken record) CreateRefreshToken(Guid clientId, Guid userId, string scope)
    {
        var raw = Crypto.NewToken(48);
        var record = new RefreshToken
        {
            TokenHash = Crypto.HashSecret(raw),
            ClientId = clientId,
            UserId = userId,
            Scope = scope,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_options.RefreshTokenLifetimeDays)
        };
        return (raw, record);
    }

    public JsonObject? Validate(string token, bool validateLifetime = true)
    {
        var parts = token.Split('.');
        if (parts.Length != 3) return null;
        byte[] sig;
        try { sig = Crypto.Base64UrlDecode(parts[2]); }
        catch { return null; }

        var data = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
        if (!_keys.Rsa.VerifyData(data, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            return null;

        JsonObject? payload;
        try
        {
            var json = Encoding.UTF8.GetString(Crypto.Base64UrlDecode(parts[1]));
            payload = JsonNode.Parse(json) as JsonObject;
        }
        catch { return null; }

        if (payload is null) return null;
        if (validateLifetime && payload["exp"]?.GetValue<long>() is long exp)
        {
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp) return null;
        }

        return payload;
    }

    private JsonObject BasePayload(Guid tenantId, DateTimeOffset now, int lifetimeSeconds, string aud)
    {
        var unix = now.ToUnixTimeSeconds();
        return new JsonObject
        {
            ["aud"] = aud,
            ["iss"] = Issuer(tenantId),
            ["iat"] = unix,
            ["nbf"] = unix,
            ["exp"] = unix + lifetimeSeconds,
            ["aio"] = Crypto.NewToken(18),
            ["tid"] = tenantId.ToString(),
            ["uti"] = Crypto.NewToken(12),
            ["ver"] = "2.0"
        };
    }

    private string Sign(JsonObject payload)
    {
        var header = new JsonObject
        {
            ["typ"] = "JWT",
            ["alg"] = "RS256",
            ["kid"] = _keys.Kid
        };
        var h = Crypto.Base64UrlEncode(Encoding.UTF8.GetBytes(header.ToJsonString(JwtJson)));
        var p = Crypto.Base64UrlEncode(Encoding.UTF8.GetBytes(payload.ToJsonString(JwtJson)));
        var sig = _keys.Rsa.SignData(Encoding.ASCII.GetBytes($"{h}.{p}"), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{h}.{p}.{Crypto.Base64UrlEncode(sig)}";
    }

    private static JsonArray ToArray(IEnumerable<string> values)
    {
        var arr = new JsonArray();
        foreach (var v in values) arr.Add(v);
        return arr;
    }
}
