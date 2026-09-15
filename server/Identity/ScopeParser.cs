using AvaEntra.Server.Data;

namespace AvaEntra.Server.Identity;

public sealed record ParsedScope(
    IReadOnlyList<string> Oidc,
    Application? Resource,
    IReadOnlyList<string> Delegated,
    bool OfflineAccess);

public static class ScopeParser
{
    public static readonly HashSet<string> OidcScopes = new(StringComparer.OrdinalIgnoreCase)
    {
        "openid", "profile", "email", "offline_access"
    };

    public static ParsedScope Parse(DirectorySnapshot dir, string? scope)
    {
        var parts = (scope ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var oidc = parts.Where(p => OidcScopes.Contains(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var rest = parts.Where(p => !OidcScopes.Contains(p)).ToList();

        Application? resource = null;
        var delegated = new List<string>();

        foreach (var raw in rest)
        {
            string resourcePart;
            string scopePart;
            var slash = raw.LastIndexOf('/');
            if (slash > 0 && (raw.StartsWith("api://", StringComparison.OrdinalIgnoreCase)
                              || raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                              || raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                              || Guid.TryParse(raw[..slash], out _)))
            {
                resourcePart = raw[..slash];
                scopePart = raw[(slash + 1)..];
            }
            else
            {
                resourcePart = "";
                scopePart = raw;
            }

            if (resourcePart.Length > 0)
                resource ??= FindResource(dir, resourcePart);

            if (scopePart is ".default")
            {
                resource ??= dir.Applications.FirstOrDefault(a => a.Kind == ApplicationKind.Api);
                if (resource is not null)
                    delegated.AddRange(dir.Scopes.Where(s => s.ApplicationId == resource.Id).Select(s => s.Value));
            }
            else
            {
                delegated.Add(scopePart);
                resource ??= dir.Applications.FirstOrDefault(a =>
                    dir.Scopes.Any(s => s.ApplicationId == a.Id && s.Value.Equals(scopePart, StringComparison.OrdinalIgnoreCase)));
            }
        }

        return new ParsedScope(
            oidc,
            resource,
            delegated.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            oidc.Contains("offline_access", StringComparer.OrdinalIgnoreCase));
    }

    public static Application? FindResource(DirectorySnapshot dir, string resource)
    {
        if (Guid.TryParse(resource, out var id))
            return dir.AppByClientId(id) ?? dir.AppById(id);

        return dir.Applications.FirstOrDefault(a =>
            string.Equals(a.IdentifierUri, resource, StringComparison.OrdinalIgnoreCase)
            || string.Equals($"api://{a.ClientId}", resource, StringComparison.OrdinalIgnoreCase)
            || string.Equals(a.ClientId.ToString(), resource, StringComparison.OrdinalIgnoreCase));
    }
}
