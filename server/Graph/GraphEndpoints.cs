using AvaEntra.Server.Data;
using AvaEntra.Server.Identity;

namespace AvaEntra.Server.Graph;

public static class GraphEndpoints
{
    public static void MapGraphEndpoints(this WebApplication app)
    {
        app.MapGet("/oidc/userinfo", Me);
        app.MapGet("/v1.0/me", Me);
        app.MapGet("/v1.0/me/memberOf", MemberOf);
        app.MapGet("/v1.0/users", Users);
        app.MapGet("/v1.0/users/{id}", UserById);
        app.MapGet("/v1.0/groups", Groups);
        app.MapGet("/v1.0/groups/{id}", GroupById);
    }

    private static IResult Me(HttpContext http, DirectoryStore store, TokenService tokens)
    {
        var user = CurrentUser(http, store, tokens);
        return user is null ? Results.Json(new { error = new { code = "InvalidAuthenticationToken", message = "Access token is missing or invalid." } }, statusCode: 401)
            : Results.Json(GraphUser(user));
    }

    private static IResult MemberOf(HttpContext http, DirectoryStore store, TokenService tokens)
    {
        var user = CurrentUser(http, store, tokens);
        if (user is null)
            return Results.Json(new { error = new { code = "InvalidAuthenticationToken", message = "Access token is missing or invalid." } }, statusCode: 401);

        var values = store.Snapshot.GroupsFor(user.Id).Select(GraphGroup);
        return Results.Json(new { value = values });
    }

    private static IResult Users(DirectoryStore store) =>
        Results.Json(new { value = store.Snapshot.Users.Select(GraphUser) });

    private static IResult UserById(DirectoryStore store, string id)
    {
        var d = store.Snapshot;
        var user = Guid.TryParse(id, out var guid) ? d.User(guid) : d.UserByUpn(id);
        return user is null ? Results.NotFound() : Results.Json(GraphUser(user));
    }

    private static IResult Groups(DirectoryStore store) =>
        Results.Json(new { value = store.Snapshot.Groups.Select(GraphGroup) });

    private static IResult GroupById(DirectoryStore store, Guid id)
    {
        var g = store.Snapshot.Groups.FirstOrDefault(x => x.Id == id);
        return g is null ? Results.NotFound() : Results.Json(GraphGroup(g));
    }

    private static DirectoryUser? CurrentUser(HttpContext http, DirectoryStore store, TokenService tokens)
    {
        var header = http.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
        var payload = tokens.Validate(header["Bearer ".Length..].Trim());
        if (payload is null) return null;
        var oid = payload["oid"]?.ToString() ?? payload["sub"]?.ToString();
        return Guid.TryParse(oid, out var id) ? store.Snapshot.User(id) : null;
    }

    private static object GraphUser(DirectoryUser u) => new
    {
        id = u.Id,
        displayName = u.DisplayName,
        givenName = u.GivenName,
        surname = u.Surname,
        userPrincipalName = u.UserPrincipalName,
        mail = u.Mail,
        accountEnabled = u.Enabled
    };

    private static object GraphGroup(DirectoryGroup g) => new
    {
        id = g.Id,
        displayName = g.DisplayName,
        description = g.Description,
        securityEnabled = true
    };
}
