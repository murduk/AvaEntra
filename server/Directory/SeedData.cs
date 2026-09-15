using AvaEntra.Server.Identity;

namespace AvaEntra.Server.Data;

public static class SeedData
{
    public static DirectorySnapshot Create()
    {
        var password = Crypto.HashPassword(WellKnown.DefaultPassword);
        var now = DateTimeOffset.UtcNow;

        var admin = new DirectoryUser
        {
            Id = WellKnown.AdminUserId,
            UserPrincipalName = "admin@avaentra.local",
            DisplayName = "Ava Entra Admin",
            GivenName = "Ava",
            Surname = "Admin",
            Mail = "admin@avaentra.local",
            PasswordHash = password,
            CreatedAt = now
        };
        var alice = new DirectoryUser
        {
            Id = WellKnown.AliceUserId,
            UserPrincipalName = "alice@avaentra.local",
            DisplayName = "Alice Contoso",
            GivenName = "Alice",
            Surname = "Contoso",
            Mail = "alice@avaentra.local",
            PasswordHash = password,
            CreatedAt = now
        };
        var bob = new DirectoryUser
        {
            Id = WellKnown.BobUserId,
            UserPrincipalName = "bob@avaentra.local",
            DisplayName = "Bob Contoso",
            GivenName = "Bob",
            Surname = "Contoso",
            Mail = "bob@avaentra.local",
            PasswordHash = password,
            CreatedAt = now
        };

        var spa = new Application
        {
            Id = WellKnown.SpaObjectId,
            ClientId = WellKnown.SpaClientId,
            DisplayName = "Sample SPA",
            Kind = ApplicationKind.Spa,
            IsPublicClient = true,
            RequirePkce = true,
            CreatedAt = now
        };
        var api = new Application
        {
            Id = WellKnown.ApiObjectId,
            ClientId = WellKnown.ApiClientId,
            DisplayName = "Sample API",
            Kind = ApplicationKind.Api,
            IdentifierUri = WellKnown.ApiIdentifier,
            IsPublicClient = false,
            RequirePkce = false,
            AllowClientCredentials = true,
            AllowOnBehalfOf = true,
            CreatedAt = now
        };
        var backend = new Application
        {
            Id = WellKnown.BackendObjectId,
            ClientId = WellKnown.BackendClientId,
            DisplayName = "Sample Backend",
            Kind = ApplicationKind.Confidential,
            IsPublicClient = false,
            RequirePkce = false,
            AllowClientCredentials = true,
            AllowOnBehalfOf = true,
            CreatedAt = now
        };
        var graph = new Application
        {
            Id = WellKnown.GraphObjectId,
            ClientId = WellKnown.GraphClientId,
            DisplayName = "Microsoft Graph (mock)",
            Kind = ApplicationKind.Api,
            IdentifierUri = "https://graph.microsoft.com",
            IsPublicClient = false,
            RequirePkce = false,
            CreatedAt = now
        };

        return new DirectorySnapshot
        {
            Tenant = new Tenant
            {
                Id = WellKnown.TenantId,
                Name = "AvaEntra",
                Domain = "avaentra.local"
            },
            Users = [admin, alice, bob],
            Groups =
            [
                new DirectoryGroup { Id = WellKnown.AdminsGroupId, DisplayName = "Administrators", Description = "Directory administrators", CreatedAt = now },
                new DirectoryGroup { Id = WellKnown.DevelopersGroupId, DisplayName = "Developers", Description = "Application developers", CreatedAt = now }
            ],
            GroupMembers =
            [
                new GroupMember { GroupId = WellKnown.AdminsGroupId, UserId = WellKnown.AdminUserId },
                new GroupMember { GroupId = WellKnown.DevelopersGroupId, UserId = WellKnown.AliceUserId },
                new GroupMember { GroupId = WellKnown.DevelopersGroupId, UserId = WellKnown.BobUserId }
            ],
            Applications = [spa, api, backend, graph],
            RedirectUris =
            [
                new RedirectUri { ApplicationId = spa.Id, Uri = "http://localhost:3000", Type = "spa" },
                new RedirectUri { ApplicationId = spa.Id, Uri = "http://localhost:3000/", Type = "spa" },
                new RedirectUri { ApplicationId = spa.Id, Uri = "http://localhost:5173", Type = "spa" },
                new RedirectUri { ApplicationId = spa.Id, Uri = "http://localhost:5173/", Type = "spa" },
                new RedirectUri { ApplicationId = spa.Id, Uri = "http://localhost:4200", Type = "spa" },
                new RedirectUri { ApplicationId = spa.Id, Uri = "http://127.0.0.1:3000", Type = "spa" },
                new RedirectUri { ApplicationId = spa.Id, Uri = "http://localhost:5100/dev/callback", Type = "spa" }
            ],
            Secrets =
            [
                new ClientSecret
                {
                    ApplicationId = backend.Id,
                    DisplayName = "Seed secret",
                    SecretHash = Crypto.HashSecret(WellKnown.BackendSecret),
                    Hint = WellKnown.BackendSecret[^3..],
                    CreatedAt = now
                }
            ],
            Scopes =
            [
                new ApiScope
                {
                    Id = WellKnown.ApiAccessScopeId,
                    ApplicationId = api.Id,
                    Value = "access_as_user",
                    DisplayName = "Access the sample API as the signed-in user",
                    Description = "Allows the app to call the sample API on behalf of the user."
                },
                new ApiScope { ApplicationId = graph.Id, Value = "User.Read", DisplayName = "Read user profile" },
                new ApiScope { ApplicationId = graph.Id, Value = "Group.Read.All", DisplayName = "Read all groups" },
                new ApiScope { ApplicationId = graph.Id, Value = "Directory.Read.All", DisplayName = "Read directory data" }
            ],
            AppRoles =
            [
                new AppRole { Id = WellKnown.ApiReaderRoleId, ApplicationId = api.Id, Value = "Reader", DisplayName = "Reader", AllowedUser = true, AllowedApplication = true },
                new AppRole { Id = WellKnown.ApiWriterRoleId, ApplicationId = api.Id, Value = "Writer", DisplayName = "Writer", AllowedUser = true, AllowedApplication = true }
            ],
            RoleAssignments =
            [
                new AppRoleAssignment { UserId = WellKnown.AliceUserId, ApplicationId = api.Id, AppRoleId = WellKnown.ApiWriterRoleId },
                new AppRoleAssignment { UserId = WellKnown.BobUserId, ApplicationId = api.Id, AppRoleId = WellKnown.ApiReaderRoleId }
            ]
        };
    }
}
