namespace AvaEntra.Server;

public sealed class AvaEntraOptions
{
    public const string SectionName = "AvaEntra";

    public string PublicOrigin { get; set; } = "http://localhost:5100";
    public int AccessTokenLifetimeMinutes { get; set; } = 60;
    public int IdTokenLifetimeMinutes { get; set; } = 60;
    public int RefreshTokenLifetimeDays { get; set; } = 90;
    public int AuthorizationCodeLifetimeMinutes { get; set; } = 10;
    public bool AllowPasswordlessDevLogin { get; set; } = true;

    /// <summary>Password assigned to seeded directory users on first run. AvaEntra__SeedUserPassword.</summary>
    public string SeedUserPassword { get; set; } = "Passw0rd!";

    /// <summary>Client secret for the seeded confidential backend app. AvaEntra__SeedBackendSecret.</summary>
    public string SeedBackendSecret { get; set; } = "dev-backend-secret";

    /// <summary>Username for the management UI. AvaEntra__AdminUsername.</summary>
    public string AdminUsername { get; set; } = "admin";

    /// <summary>Password for the management UI. AvaEntra__AdminPassword.</summary>
    public string AdminPassword { get; set; } = "AdminPassw0rd!";
}

public static class AdminAuth
{
    public const string Scheme = "Admin";
    public const string Policy = "Admin";
    public const string CookieName = "AvaEntra.Admin";
    public const string ClaimType = "admin";
}
