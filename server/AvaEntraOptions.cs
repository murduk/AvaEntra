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
}
