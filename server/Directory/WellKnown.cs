namespace AvaEntra.Server.Data;

public static class WellKnown
{
    public static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid AdminUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid AliceUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid BobUserId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid AdminsGroupId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid DevelopersGroupId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly Guid SpaObjectId = Guid.Parse("55000000-0000-0000-0000-000000000001");
    public static readonly Guid SpaClientId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    public static readonly Guid ApiObjectId = Guid.Parse("66000000-0000-0000-0000-000000000001");
    public static readonly Guid ApiClientId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    public static readonly Guid BackendObjectId = Guid.Parse("77000000-0000-0000-0000-000000000001");
    public static readonly Guid BackendClientId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    public static readonly Guid GraphObjectId = Guid.Parse("03000000-0000-0000-0000-000000000001");
    public static readonly Guid GraphClientId = Guid.Parse("00000003-0000-0000-c000-000000000000");
    public static readonly Guid ApiAccessScopeId = Guid.Parse("66111111-1111-1111-1111-111111111111");
    public static readonly Guid ApiReaderRoleId = Guid.Parse("66222222-2222-2222-2222-222222222222");
    public static readonly Guid ApiWriterRoleId = Guid.Parse("66333333-3333-3333-3333-333333333333");

    public const string DefaultPassword = "Passw0rd!";
    public const string BackendSecret = "dev-backend-secret";
    public const string ApiIdentifier = "api://sample-api";
}
