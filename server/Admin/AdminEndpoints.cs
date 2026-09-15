using System.Security.Claims;
using AvaEntra.Server.Data;
using AvaEntra.Server.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AvaEntra.Server.Admin;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/admin");
        g.MapPost("/login", Login).AllowAnonymous();
        g.MapPost("/logout", (Delegate)Logout);
        g.MapGet("/me", (Delegate)Me);

        var secured = g.MapGroup("").RequireAuthorization(AdminAuth.Policy);
        secured.MapGet("/overview", Overview);
        secured.MapGet("/tenant", GetTenant);
        secured.MapPut("/tenant", UpdateTenant);

        secured.MapGet("/users", ListUsers);
        secured.MapPost("/users", CreateUser);
        secured.MapGet("/users/{id:guid}", GetUser);
        secured.MapPut("/users/{id:guid}", UpdateUser);
        secured.MapDelete("/users/{id:guid}", DeleteUser);
        secured.MapPost("/users/{id:guid}/password", SetPassword);
        secured.MapPost("/users/{id:guid}/groups/{groupId:guid}", AddUserToGroup);
        secured.MapDelete("/users/{id:guid}/groups/{groupId:guid}", RemoveUserFromGroup);
        secured.MapPost("/users/{id:guid}/roles", AssignRole);
        secured.MapDelete("/users/{id:guid}/roles/{assignmentId:guid}", UnassignRole);

        secured.MapGet("/groups", ListGroups);
        secured.MapPost("/groups", CreateGroup);
        secured.MapGet("/groups/{id:guid}", GetGroup);
        secured.MapPut("/groups/{id:guid}", UpdateGroup);
        secured.MapDelete("/groups/{id:guid}", DeleteGroup);
        secured.MapPost("/groups/{id:guid}/members/{userId:guid}", AddMember);
        secured.MapDelete("/groups/{id:guid}/members/{userId:guid}", RemoveMember);

        secured.MapGet("/applications", ListApps);
        secured.MapPost("/applications", CreateApp);
        secured.MapGet("/applications/{id:guid}", GetApp);
        secured.MapPut("/applications/{id:guid}", UpdateApp);
        secured.MapDelete("/applications/{id:guid}", DeleteApp);
        secured.MapPost("/applications/{id:guid}/redirects", AddRedirect);
        secured.MapDelete("/applications/{id:guid}/redirects/{rid:guid}", DeleteRedirect);
        secured.MapPost("/applications/{id:guid}/secrets", AddSecret);
        secured.MapDelete("/applications/{id:guid}/secrets/{sid:guid}", DeleteSecret);
        secured.MapPost("/applications/{id:guid}/scopes", AddScope);
        secured.MapPut("/applications/{id:guid}/scopes/{sid:guid}", UpdateScope);
        secured.MapDelete("/applications/{id:guid}/scopes/{sid:guid}", DeleteScope);
        secured.MapPost("/applications/{id:guid}/roles", AddRole);
        secured.MapPut("/applications/{id:guid}/roles/{rid:guid}", UpdateRole);
        secured.MapDelete("/applications/{id:guid}/roles/{rid:guid}", DeleteRole);

        secured.MapGet("/logs", Logs);
    }

    private static async Task<IResult> Login(HttpContext http, IOptions<AvaEntraOptions> options, AdminLoginBody body)
    {
        var opts = options.Value;
        var username = (body.Username ?? "").Trim();
        var password = body.Password ?? "";
        if (!FixedEquals(username, opts.AdminUsername) || !FixedEquals(password, opts.AdminPassword))
            return Results.Json(new { error = "Invalid username or password." }, statusCode: 401);

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, opts.AdminUsername),
            new Claim(AdminAuth.ClaimType, "true")
        ], AdminAuth.Scheme);
        await http.SignInAsync(AdminAuth.Scheme, new ClaimsPrincipal(identity));
        return Results.Json(new { username = opts.AdminUsername });
    }

    private static async Task<IResult> Logout(HttpContext http)
    {
        await http.SignOutAsync(AdminAuth.Scheme);
        return Results.Ok(new { ok = true });
    }

    private static async Task<IResult> Me(HttpContext http)
    {
        var result = await http.AuthenticateAsync(AdminAuth.Scheme);
        if (!result.Succeeded || result.Principal?.HasClaim(AdminAuth.ClaimType, "true") != true)
            return Results.Unauthorized();

        return Results.Json(new { username = result.Principal.Identity?.Name });
    }

    private static bool FixedEquals(string a, string b)
    {
        var ba = System.Text.Encoding.UTF8.GetBytes(a);
        var bb = System.Text.Encoding.UTF8.GetBytes(b);
        if (ba.Length != bb.Length) return false;
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(ba, bb);
    }

    private static IResult Overview(DirectoryStore store, TokenService tokens, IOptions<AvaEntraOptions> options)
    {
        var d = store.Snapshot;
        var origin = tokens.Origin;
        var tid = d.Tenant.Id;
        var authority = $"{origin}/{tid}";
        return Results.Json(new
        {
            tenant = d.Tenant,
            origin,
            authority,
            issuer = tokens.Issuer(tid),
            discovery = $"{authority}/v2.0/.well-known/openid-configuration",
            authorize = $"{authority}/oauth2/v2.0/authorize",
            token = $"{authority}/oauth2/v2.0/token",
            jwks = $"{authority}/discovery/v2.0/keys",
            graph = $"{origin}/v1.0",
            allowPasswordlessDevLogin = options.Value.AllowPasswordlessDevLogin,
            counts = new
            {
                users = d.Users.Count,
                groups = d.Groups.Count,
                applications = d.Applications.Count,
                logs = d.SignInLogs.Count
            },
            seed = new
            {
                spaClientId = WellKnown.SpaClientId,
                apiClientId = WellKnown.ApiClientId,
                apiIdentifier = WellKnown.ApiIdentifier,
                backendClientId = WellKnown.BackendClientId,
                backendSecret = options.Value.SeedBackendSecret,
                sampleScope = $"{WellKnown.ApiIdentifier}/access_as_user",
                defaultPassword = options.Value.SeedUserPassword
            },
            msalBrowser = new
            {
                auth = new
                {
                    clientId = WellKnown.SpaClientId.ToString(),
                    authority,
                    knownAuthorities = new[] { new Uri(origin).Authority },
                    redirectUri = "http://localhost:3000"
                }
            },
            identityWeb = new
            {
                Instance = origin + "/",
                TenantId = tid.ToString(),
                ClientId = WellKnown.ApiClientId.ToString(),
                Audience = WellKnown.ApiIdentifier
            }
        });
    }

    private static IResult GetTenant(DirectoryStore store) => Results.Json(store.Snapshot.Tenant);

    private static async Task<IResult> UpdateTenant(DirectoryStore store, TenantUpdate body)
    {
        await store.UpdateAsync(d =>
        {
            if (!string.IsNullOrWhiteSpace(body.Name)) d.Tenant.Name = body.Name.Trim();
            if (!string.IsNullOrWhiteSpace(body.Domain)) d.Tenant.Domain = body.Domain.Trim();
        });
        return Results.Json(store.Snapshot.Tenant);
    }

    private static IResult ListUsers(DirectoryStore store) =>
        Results.Json(store.Snapshot.Users.OrderBy(u => u.DisplayName).Select(u => UserDto(store.Snapshot, u)));

    private static IResult GetUser(DirectoryStore store, Guid id)
    {
        var d = store.Snapshot;
        var user = d.User(id);
        return user is null ? Results.NotFound() : Results.Json(UserDto(d, user, true));
    }

    private static async Task<IResult> CreateUser(DirectoryStore store, UserWrite body, IOptions<AvaEntraOptions> options)
    {
        if (string.IsNullOrWhiteSpace(body.UserPrincipalName) || string.IsNullOrWhiteSpace(body.DisplayName))
            return Results.BadRequest(new { error = "userPrincipalName and displayName are required." });
        if (store.Snapshot.UserByUpn(body.UserPrincipalName) is not null)
            return Results.Conflict(new { error = "A user with that UPN already exists." });

        var user = new DirectoryUser
        {
            UserPrincipalName = body.UserPrincipalName.Trim(),
            DisplayName = body.DisplayName.Trim(),
            GivenName = body.GivenName,
            Surname = body.Surname,
            Mail = body.Mail ?? body.UserPrincipalName.Trim(),
            PasswordHash = Crypto.HashPassword(string.IsNullOrEmpty(body.Password) ? options.Value.SeedUserPassword : body.Password),
            Enabled = body.Enabled ?? true
        };
        await store.UpdateAsync(d => d.Users.Add(user));
        return Results.Created($"/api/admin/users/{user.Id}", UserDto(store.Snapshot, user, true));
    }

    private static async Task<IResult> UpdateUser(DirectoryStore store, Guid id, UserWrite body)
    {
        DirectoryUser? updated = null;
        await store.UpdateAsync(d =>
        {
            var user = d.User(id);
            if (user is null) return;
            if (!string.IsNullOrWhiteSpace(body.DisplayName)) user.DisplayName = body.DisplayName.Trim();
            if (!string.IsNullOrWhiteSpace(body.UserPrincipalName)) user.UserPrincipalName = body.UserPrincipalName.Trim();
            if (body.GivenName is not null) user.GivenName = body.GivenName;
            if (body.Surname is not null) user.Surname = body.Surname;
            if (body.Mail is not null) user.Mail = body.Mail;
            if (body.Enabled is not null) user.Enabled = body.Enabled.Value;
            updated = user;
        });
        return updated is null ? Results.NotFound() : Results.Json(UserDto(store.Snapshot, updated, true));
    }

    private static async Task<IResult> DeleteUser(DirectoryStore store, Guid id)
    {
        var ok = await store.UpdateAsync(d =>
        {
            var n = d.Users.RemoveAll(u => u.Id == id);
            d.GroupMembers.RemoveAll(m => m.UserId == id);
            d.RoleAssignments.RemoveAll(a => a.UserId == id);
            d.RefreshTokens.RemoveAll(t => t.UserId == id);
            return n > 0;
        });
        return ok ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> SetPassword(DirectoryStore store, Guid id, PasswordWrite body)
    {
        if (string.IsNullOrEmpty(body.Password)) return Results.BadRequest(new { error = "password is required." });
        var ok = await store.UpdateAsync(d =>
        {
            var user = d.User(id);
            if (user is null) return false;
            user.PasswordHash = Crypto.HashPassword(body.Password);
            return true;
        });
        return ok ? Results.Ok(new { ok = true }) : Results.NotFound();
    }

    private static async Task<IResult> AddUserToGroup(DirectoryStore store, Guid id, Guid groupId) =>
        await AddMember(store, groupId, id);

    private static async Task<IResult> RemoveUserFromGroup(DirectoryStore store, Guid id, Guid groupId) =>
        await RemoveMember(store, groupId, id);

    private static async Task<IResult> AssignRole(DirectoryStore store, Guid id, RoleAssignWrite body)
    {
        AppRoleAssignment? assignment = null;
        await store.UpdateAsync(d =>
        {
            if (d.User(id) is null) return;
            var role = d.AppRoles.FirstOrDefault(r => r.Id == body.AppRoleId);
            if (role is null) return;
            assignment = new AppRoleAssignment { UserId = id, ApplicationId = role.ApplicationId, AppRoleId = role.Id };
            d.RoleAssignments.RemoveAll(a => a.UserId == id && a.AppRoleId == role.Id);
            d.RoleAssignments.Add(assignment);
        });
        return assignment is null ? Results.NotFound() : Results.Ok(assignment);
    }

    private static async Task<IResult> UnassignRole(DirectoryStore store, Guid id, Guid assignmentId)
    {
        var ok = await store.UpdateAsync(d => d.RoleAssignments.RemoveAll(a => a.Id == assignmentId && a.UserId == id) > 0);
        return ok ? Results.NoContent() : Results.NotFound();
    }

    private static IResult ListGroups(DirectoryStore store) =>
        Results.Json(store.Snapshot.Groups.OrderBy(g => g.DisplayName).Select(g => GroupDto(store.Snapshot, g)));

    private static IResult GetGroup(DirectoryStore store, Guid id)
    {
        var g = store.Snapshot.Groups.FirstOrDefault(x => x.Id == id);
        return g is null ? Results.NotFound() : Results.Json(GroupDto(store.Snapshot, g, true));
    }

    private static async Task<IResult> CreateGroup(DirectoryStore store, GroupWrite body)
    {
        if (string.IsNullOrWhiteSpace(body.DisplayName)) return Results.BadRequest(new { error = "displayName is required." });
        var group = new DirectoryGroup { DisplayName = body.DisplayName.Trim(), Description = body.Description };
        await store.UpdateAsync(d => d.Groups.Add(group));
        return Results.Created($"/api/admin/groups/{group.Id}", GroupDto(store.Snapshot, group, true));
    }

    private static async Task<IResult> UpdateGroup(DirectoryStore store, Guid id, GroupWrite body)
    {
        DirectoryGroup? group = null;
        await store.UpdateAsync(d =>
        {
            group = d.Groups.FirstOrDefault(x => x.Id == id);
            if (group is null) return;
            if (!string.IsNullOrWhiteSpace(body.DisplayName)) group.DisplayName = body.DisplayName.Trim();
            if (body.Description is not null) group.Description = body.Description;
        });
        return group is null ? Results.NotFound() : Results.Json(GroupDto(store.Snapshot, group, true));
    }

    private static async Task<IResult> DeleteGroup(DirectoryStore store, Guid id)
    {
        var ok = await store.UpdateAsync(d =>
        {
            var n = d.Groups.RemoveAll(g => g.Id == id);
            d.GroupMembers.RemoveAll(m => m.GroupId == id);
            return n > 0;
        });
        return ok ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> AddMember(DirectoryStore store, Guid id, Guid userId)
    {
        var ok = await store.UpdateAsync(d =>
        {
            if (d.Groups.All(g => g.Id != id) || d.User(userId) is null) return false;
            if (d.GroupMembers.Any(m => m.GroupId == id && m.UserId == userId)) return true;
            d.GroupMembers.Add(new GroupMember { GroupId = id, UserId = userId });
            return true;
        });
        return ok ? Results.Ok(new { ok = true }) : Results.NotFound();
    }

    private static async Task<IResult> RemoveMember(DirectoryStore store, Guid id, Guid userId)
    {
        await store.UpdateAsync(d => d.GroupMembers.RemoveAll(m => m.GroupId == id && m.UserId == userId));
        return Results.NoContent();
    }

    private static IResult ListApps(DirectoryStore store) =>
        Results.Json(store.Snapshot.Applications.OrderBy(a => a.DisplayName).Select(a => AppSummary(store.Snapshot, a)));

    private static IResult GetApp(DirectoryStore store, Guid id)
    {
        var app = store.Snapshot.AppById(id);
        return app is null ? Results.NotFound() : Results.Json(AppDetail(store.Snapshot, app));
    }

    private static async Task<IResult> CreateApp(DirectoryStore store, AppWrite body)
    {
        if (string.IsNullOrWhiteSpace(body.DisplayName)) return Results.BadRequest(new { error = "displayName is required." });
        var kind = ParseKind(body.Kind);
        var app = new Application
        {
            DisplayName = body.DisplayName.Trim(),
            Kind = kind,
            ClientId = body.ClientId ?? Guid.NewGuid(),
            IdentifierUri = body.IdentifierUri,
            IsPublicClient = kind == ApplicationKind.Spa || (body.IsPublicClient ?? false),
            RequirePkce = body.RequirePkce ?? kind == ApplicationKind.Spa,
            AllowClientCredentials = body.AllowClientCredentials ?? kind is ApplicationKind.Confidential or ApplicationKind.Api,
            AllowOnBehalfOf = body.AllowOnBehalfOf ?? kind is ApplicationKind.Confidential or ApplicationKind.Api
        };
        await store.UpdateAsync(d => d.Applications.Add(app));
        return Results.Created($"/api/admin/applications/{app.Id}", AppDetail(store.Snapshot, app));
    }

    private static async Task<IResult> UpdateApp(DirectoryStore store, Guid id, AppWrite body)
    {
        Application? app = null;
        await store.UpdateAsync(d =>
        {
            app = d.AppById(id);
            if (app is null) return;
            if (!string.IsNullOrWhiteSpace(body.DisplayName)) app.DisplayName = body.DisplayName.Trim();
            if (body.Kind is not null) app.Kind = ParseKind(body.Kind);
            if (body.IdentifierUri is not null) app.IdentifierUri = body.IdentifierUri;
            if (body.IsPublicClient is not null) app.IsPublicClient = body.IsPublicClient.Value;
            if (body.RequirePkce is not null) app.RequirePkce = body.RequirePkce.Value;
            if (body.AllowClientCredentials is not null) app.AllowClientCredentials = body.AllowClientCredentials.Value;
            if (body.AllowOnBehalfOf is not null) app.AllowOnBehalfOf = body.AllowOnBehalfOf.Value;
        });
        return app is null ? Results.NotFound() : Results.Json(AppDetail(store.Snapshot, app));
    }

    private static async Task<IResult> DeleteApp(DirectoryStore store, Guid id)
    {
        var ok = await store.UpdateAsync(d =>
        {
            var n = d.Applications.RemoveAll(a => a.Id == id);
            d.RedirectUris.RemoveAll(r => r.ApplicationId == id);
            d.Secrets.RemoveAll(s => s.ApplicationId == id);
            d.Scopes.RemoveAll(s => s.ApplicationId == id);
            d.AppRoles.RemoveAll(r => r.ApplicationId == id);
            d.RoleAssignments.RemoveAll(a => a.ApplicationId == id);
            return n > 0;
        });
        return ok ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> AddRedirect(DirectoryStore store, Guid id, RedirectWrite body)
    {
        if (string.IsNullOrWhiteSpace(body.Uri)) return Results.BadRequest(new { error = "uri is required." });
        RedirectUri? row = null;
        await store.UpdateAsync(d =>
        {
            if (d.AppById(id) is null) return;
            row = new RedirectUri { ApplicationId = id, Uri = body.Uri.Trim(), Type = string.IsNullOrWhiteSpace(body.Type) ? "spa" : body.Type };
            d.RedirectUris.Add(row);
        });
        return row is null ? Results.NotFound() : Results.Ok(row);
    }

    private static async Task<IResult> DeleteRedirect(DirectoryStore store, Guid id, Guid rid)
    {
        await store.UpdateAsync(d => d.RedirectUris.RemoveAll(r => r.ApplicationId == id && r.Id == rid));
        return Results.NoContent();
    }

    private static async Task<IResult> AddSecret(DirectoryStore store, Guid id, SecretWrite body)
    {
        var plaintext = string.IsNullOrEmpty(body.Value) ? Crypto.NewToken(32) : body.Value;
        ClientSecret? row = null;
        await store.UpdateAsync(d =>
        {
            if (d.AppById(id) is null) return;
            row = new ClientSecret
            {
                ApplicationId = id,
                DisplayName = string.IsNullOrWhiteSpace(body.DisplayName) ? "client secret" : body.DisplayName,
                SecretHash = Crypto.HashSecret(plaintext),
                Hint = plaintext.Length >= 3 ? plaintext[^3..] : plaintext,
                ExpiresAt = body.ExpiresAt
            };
            d.Secrets.Add(row);
        });
        return row is null ? Results.NotFound() : Results.Ok(new { row.Id, row.DisplayName, row.Hint, row.CreatedAt, secret = plaintext });
    }

    private static async Task<IResult> DeleteSecret(DirectoryStore store, Guid id, Guid sid)
    {
        await store.UpdateAsync(d => d.Secrets.RemoveAll(s => s.ApplicationId == id && s.Id == sid));
        return Results.NoContent();
    }

    private static async Task<IResult> AddScope(DirectoryStore store, Guid id, ScopeWrite body)
    {
        if (string.IsNullOrWhiteSpace(body.Value)) return Results.BadRequest(new { error = "value is required." });
        ApiScope? row = null;
        await store.UpdateAsync(d =>
        {
            if (d.AppById(id) is null) return;
            row = new ApiScope
            {
                ApplicationId = id,
                Value = body.Value.Trim(),
                DisplayName = body.DisplayName?.Trim() ?? body.Value.Trim(),
                Description = body.Description
            };
            d.Scopes.Add(row);
        });
        return row is null ? Results.NotFound() : Results.Ok(row);
    }

    private static async Task<IResult> UpdateScope(DirectoryStore store, Guid id, Guid sid, ScopeWrite body)
    {
        ApiScope? row = null;
        await store.UpdateAsync(d =>
        {
            row = d.Scopes.FirstOrDefault(s => s.ApplicationId == id && s.Id == sid);
            if (row is null) return;
            if (!string.IsNullOrWhiteSpace(body.Value)) row.Value = body.Value.Trim();
            if (body.DisplayName is not null) row.DisplayName = body.DisplayName;
            if (body.Description is not null) row.Description = body.Description;
        });
        return row is null ? Results.NotFound() : Results.Ok(row);
    }

    private static async Task<IResult> DeleteScope(DirectoryStore store, Guid id, Guid sid)
    {
        await store.UpdateAsync(d => d.Scopes.RemoveAll(s => s.ApplicationId == id && s.Id == sid));
        return Results.NoContent();
    }

    private static async Task<IResult> AddRole(DirectoryStore store, Guid id, RoleWrite body)
    {
        if (string.IsNullOrWhiteSpace(body.Value)) return Results.BadRequest(new { error = "value is required." });
        AppRole? row = null;
        await store.UpdateAsync(d =>
        {
            if (d.AppById(id) is null) return;
            row = new AppRole
            {
                ApplicationId = id,
                Value = body.Value.Trim(),
                DisplayName = body.DisplayName?.Trim() ?? body.Value.Trim(),
                Description = body.Description,
                AllowedUser = body.AllowedUser ?? true,
                AllowedApplication = body.AllowedApplication ?? true
            };
            d.AppRoles.Add(row);
        });
        return row is null ? Results.NotFound() : Results.Ok(row);
    }

    private static async Task<IResult> UpdateRole(DirectoryStore store, Guid id, Guid rid, RoleWrite body)
    {
        AppRole? row = null;
        await store.UpdateAsync(d =>
        {
            row = d.AppRoles.FirstOrDefault(r => r.ApplicationId == id && r.Id == rid);
            if (row is null) return;
            if (!string.IsNullOrWhiteSpace(body.Value)) row.Value = body.Value.Trim();
            if (body.DisplayName is not null) row.DisplayName = body.DisplayName;
            if (body.Description is not null) row.Description = body.Description;
            if (body.AllowedUser is not null) row.AllowedUser = body.AllowedUser.Value;
            if (body.AllowedApplication is not null) row.AllowedApplication = body.AllowedApplication.Value;
        });
        return row is null ? Results.NotFound() : Results.Ok(row);
    }

    private static async Task<IResult> DeleteRole(DirectoryStore store, Guid id, Guid rid)
    {
        await store.UpdateAsync(d =>
        {
            d.AppRoles.RemoveAll(r => r.ApplicationId == id && r.Id == rid);
            d.RoleAssignments.RemoveAll(a => a.AppRoleId == rid);
        });
        return Results.NoContent();
    }

    private static IResult Logs(DirectoryStore store) =>
        Results.Json(store.Snapshot.SignInLogs.Take(100).Select(l => new
        {
            l.Id,
            l.At,
            l.GrantType,
            l.Success,
            l.Error,
            l.Scopes,
            l.Audience,
            user = store.Snapshot.Users.FirstOrDefault(u => u.Id == l.UserId)?.UserPrincipalName,
            client = store.Snapshot.Applications.FirstOrDefault(a => a.ClientId == l.ClientId)?.DisplayName
        }));

    private static object UserDto(DirectorySnapshot d, DirectoryUser u, bool detail = false)
    {
        var groups = d.GroupsFor(u.Id).Select(g => new { g.Id, g.DisplayName }).ToList();
        var roles = d.RoleAssignments.Where(a => a.UserId == u.Id).Select(a =>
        {
            var role = d.AppRoles.FirstOrDefault(r => r.Id == a.AppRoleId);
            var app = d.AppById(a.ApplicationId);
            return new { a.Id, a.AppRoleId, a.ApplicationId, role = role?.Value, app = app?.DisplayName };
        }).ToList();
        return new
        {
            u.Id,
            u.UserPrincipalName,
            u.DisplayName,
            u.GivenName,
            u.Surname,
            u.Mail,
            u.Enabled,
            u.CreatedAt,
            groups,
            roles = detail ? roles : null
        };
    }

    private static object GroupDto(DirectorySnapshot d, DirectoryGroup g, bool detail = false)
    {
        var memberIds = d.GroupMembers.Where(m => m.GroupId == g.Id).Select(m => m.UserId).ToList();
        var members = d.Users.Where(u => memberIds.Contains(u.Id)).Select(u => new { u.Id, u.DisplayName, u.UserPrincipalName });
        return new { g.Id, g.DisplayName, g.Description, g.CreatedAt, memberCount = memberIds.Count, members = detail ? members : null };
    }

    private static object AppSummary(DirectorySnapshot d, Application a) => new
    {
        a.Id,
        a.ClientId,
        a.DisplayName,
        kind = a.Kind.ToString(),
        a.IdentifierUri,
        a.IsPublicClient,
        a.RequirePkce,
        a.AllowClientCredentials,
        a.AllowOnBehalfOf,
        a.CreatedAt,
        redirectCount = d.RedirectUris.Count(r => r.ApplicationId == a.Id),
        secretCount = d.Secrets.Count(s => s.ApplicationId == a.Id)
    };

    private static object AppDetail(DirectorySnapshot d, Application a) => new
    {
        a.Id,
        a.ClientId,
        a.DisplayName,
        kind = a.Kind.ToString(),
        a.IdentifierUri,
        a.IsPublicClient,
        a.RequirePkce,
        a.AllowClientCredentials,
        a.AllowOnBehalfOf,
        a.CreatedAt,
        redirects = d.RedirectUris.Where(r => r.ApplicationId == a.Id),
        secrets = d.Secrets.Where(s => s.ApplicationId == a.Id).Select(s => new { s.Id, s.DisplayName, s.Hint, s.CreatedAt, s.ExpiresAt }),
        scopes = d.Scopes.Where(s => s.ApplicationId == a.Id),
        roles = d.AppRoles.Where(r => r.ApplicationId == a.Id),
        assignments = d.RoleAssignments.Where(x => x.ApplicationId == a.Id).Select(x =>
        {
            var user = d.User(x.UserId);
            var role = d.AppRoles.FirstOrDefault(r => r.Id == x.AppRoleId);
            return new { x.Id, x.UserId, user = user?.DisplayName, upn = user?.UserPrincipalName, x.AppRoleId, role = role?.Value };
        })
    };

    private static ApplicationKind ParseKind(string? kind) =>
        Enum.TryParse<ApplicationKind>(kind, true, out var parsed) ? parsed : ApplicationKind.Spa;
}

public sealed record AdminLoginBody(string? Username, string? Password);
public sealed record TenantUpdate(string? Name, string? Domain);
public sealed record UserWrite(string? UserPrincipalName, string? DisplayName, string? GivenName, string? Surname, string? Mail, string? Password, bool? Enabled);
public sealed record PasswordWrite(string Password);
public sealed record GroupWrite(string? DisplayName, string? Description);
public sealed record RoleAssignWrite(Guid AppRoleId);
public sealed record AppWrite(string? DisplayName, string? Kind, Guid? ClientId, string? IdentifierUri, bool? IsPublicClient, bool? RequirePkce, bool? AllowClientCredentials, bool? AllowOnBehalfOf);
public sealed record RedirectWrite(string Uri, string? Type);
public sealed record SecretWrite(string? DisplayName, string? Value, DateTimeOffset? ExpiresAt);
public sealed record ScopeWrite(string? Value, string? DisplayName, string? Description);
public sealed record RoleWrite(string? Value, string? DisplayName, string? Description, bool? AllowedUser, bool? AllowedApplication);
