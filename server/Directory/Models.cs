namespace AvaEntra.Server.Data;

public sealed class DirectorySnapshot
{
    public Tenant Tenant { get; set; } = new();
    public List<DirectoryUser> Users { get; set; } = [];
    public List<DirectoryGroup> Groups { get; set; } = [];
    public List<GroupMember> GroupMembers { get; set; } = [];
    public List<Application> Applications { get; set; } = [];
    public List<RedirectUri> RedirectUris { get; set; } = [];
    public List<ClientSecret> Secrets { get; set; } = [];
    public List<ApiScope> Scopes { get; set; } = [];
    public List<AppRole> AppRoles { get; set; } = [];
    public List<AppRoleAssignment> RoleAssignments { get; set; } = [];
    public List<RefreshToken> RefreshTokens { get; set; } = [];
    public List<SignInLog> SignInLogs { get; set; } = [];
}

public sealed class Tenant
{
    public Guid Id { get; set; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public string Name { get; set; } = "AvaEntra";
    public string Domain { get; set; } = "avaentra.local";
}

public sealed class DirectoryUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserPrincipalName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? GivenName { get; set; }
    public string? Surname { get; set; }
    public string? Mail { get; set; }
    public string PasswordHash { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class DirectoryGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = "";
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class GroupMember
{
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
}

public enum ApplicationKind
{
    Spa,
    Web,
    Api,
    Confidential
}

public sealed class Application
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClientId { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = "";
    public ApplicationKind Kind { get; set; }
    public string? IdentifierUri { get; set; }
    public bool IsPublicClient { get; set; }
    public bool RequirePkce { get; set; } = true;
    public bool AllowClientCredentials { get; set; }
    public bool AllowOnBehalfOf { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class RedirectUri
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public string Uri { get; set; } = "";
    public string Type { get; set; } = "spa";
}

public sealed class ClientSecret
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public string DisplayName { get; set; } = "client secret";
    public string SecretHash { get; set; } = "";
    public string Hint { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class ApiScope
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public string Value { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Description { get; set; }
}

public sealed class AppRole
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public string Value { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Description { get; set; }
    public bool AllowedUser { get; set; } = true;
    public bool AllowedApplication { get; set; } = true;
}

public sealed class AppRoleAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid AppRoleId { get; set; }
}

public sealed class RefreshToken
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TokenHash { get; set; } = "";
    public Guid ClientId { get; set; }
    public Guid UserId { get; set; }
    public string Scope { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Revoked { get; set; }
}

public sealed class SignInLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public Guid? ClientId { get; set; }
    public string GrantType { get; set; } = "";
    public string? Scopes { get; set; }
    public string? Audience { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AuthorizationCode
{
    public string Code { get; set; } = "";
    public Guid ClientId { get; set; }
    public Guid UserId { get; set; }
    public string RedirectUri { get; set; } = "";
    public string Scope { get; set; } = "";
    public string? Nonce { get; set; }
    public string? CodeChallenge { get; set; }
    public string? CodeChallengeMethod { get; set; }
    public string? SessionId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Consumed { get; set; }
}
