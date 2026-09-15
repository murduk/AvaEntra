using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using AvaEntra.Server.Identity;

namespace AvaEntra.Server.Data;

public sealed class DirectoryStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly ILogger<DirectoryStore> _log;
    private DirectorySnapshot _data = new();

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true
    };

    public DirectoryStore(IWebHostEnvironment env, ILogger<DirectoryStore> log)
    {
        _log = log;
        var dir = Path.Combine(env.ContentRootPath, "storage");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "directory.json");
    }

    public DirectorySnapshot Snapshot => _data;

    public void Initialize()
    {
        if (File.Exists(_path))
        {
            var json = File.ReadAllText(_path);
            _data = JsonSerializer.Deserialize<DirectorySnapshot>(json, JsonOptions) ?? new DirectorySnapshot();
            _log.LogInformation("Loaded directory from {Path}", _path);
            return;
        }

        _data = SeedData.Create();
        SaveUnlocked();
        _log.LogInformation("""
            First-run directory seeded.
              Admin UI:        http://localhost:5100
              Tenant ID:       {Tenant}
              Users:           admin@avaentra.local, alice@avaentra.local, bob@avaentra.local
              Password:        {Password}
              SPA client ID:   {Spa}
              API audience:    {Api}
              Backend client:  {Backend}
              Backend secret:  {Secret}
            """,
            _data.Tenant.Id, WellKnown.DefaultPassword, WellKnown.SpaClientId,
            WellKnown.ApiIdentifier, WellKnown.BackendClientId, WellKnown.BackendSecret);
    }

    public T Read<T>(Func<DirectorySnapshot, T> read)
    {
        _mutex.Wait();
        try { return read(_data); }
        finally { _mutex.Release(); }
    }

    public async Task<T> UpdateAsync<T>(Func<DirectorySnapshot, T> mutate)
    {
        await _mutex.WaitAsync();
        try
        {
            var result = mutate(_data);
            SaveUnlocked();
            return result;
        }
        finally { _mutex.Release(); }
    }

    public Task UpdateAsync(Action<DirectorySnapshot> mutate) =>
        UpdateAsync(d => { mutate(d); return 0; });

    public void LogSignIn(SignInLog entry)
    {
        _mutex.Wait();
        try
        {
            _data.SignInLogs.Insert(0, entry);
            if (_data.SignInLogs.Count > 200)
                _data.SignInLogs.RemoveRange(200, _data.SignInLogs.Count - 200);
            SaveUnlocked();
        }
        finally { _mutex.Release(); }
    }

    private void SaveUnlocked()
    {
        var json = JsonSerializer.Serialize(_data, JsonOptions);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, _path, overwrite: true);
    }
}

public sealed class CodeStore
{
    private readonly ConcurrentDictionary<string, AuthorizationCode> _codes = new();

    public void Add(AuthorizationCode code) => _codes[code.Code] = code;

    public AuthorizationCode? Consume(string code)
    {
        if (!_codes.TryRemove(code, out var stored)) return null;
        if (stored.Consumed || stored.ExpiresAt < DateTimeOffset.UtcNow) return null;
        stored.Consumed = true;
        return stored;
    }
}

public static class DirectoryQueries
{
    public static DirectoryUser? User(this DirectorySnapshot d, Guid id) =>
        d.Users.FirstOrDefault(u => u.Id == id);

    public static DirectoryUser? UserByUpn(this DirectorySnapshot d, string upn) =>
        d.Users.FirstOrDefault(u => u.UserPrincipalName.Equals(upn, StringComparison.OrdinalIgnoreCase));

    public static Application? AppByClientId(this DirectorySnapshot d, Guid clientId) =>
        d.Applications.FirstOrDefault(a => a.ClientId == clientId);

    public static Application? AppById(this DirectorySnapshot d, Guid id) =>
        d.Applications.FirstOrDefault(a => a.Id == id);

    public static Application? AppByClientId(this DirectorySnapshot d, string? clientId) =>
        Guid.TryParse(clientId, out var id) ? d.AppByClientId(id) : null;

    public static bool IsRedirectAllowed(this DirectorySnapshot d, Application app, string redirectUri) =>
        d.RedirectUris.Any(r => r.ApplicationId == app.Id
            && r.Type != "postLogout"
            && string.Equals(r.Uri, redirectUri, StringComparison.Ordinal));

    public static bool IsPostLogoutAllowed(this DirectorySnapshot d, Application app, string uri) =>
        d.RedirectUris.Any(r => r.ApplicationId == app.Id
            && string.Equals(r.Uri, uri, StringComparison.Ordinal));

    public static IEnumerable<DirectoryGroup> GroupsFor(this DirectorySnapshot d, Guid userId) =>
        from m in d.GroupMembers
        where m.UserId == userId
        join g in d.Groups on m.GroupId equals g.Id
        select g;

    public static IEnumerable<string> RoleValuesFor(this DirectorySnapshot d, Guid userId, Guid applicationId) =>
        from a in d.RoleAssignments
        where a.UserId == userId && a.ApplicationId == applicationId
        join r in d.AppRoles on a.AppRoleId equals r.Id
        select r.Value;

    public static bool VerifyClientSecret(this DirectorySnapshot d, Application app, string? secret)
    {
        if (app.IsPublicClient) return true;
        var secrets = d.Secrets.Where(s => s.ApplicationId == app.Id).ToList();
        if (secrets.Count == 0 || string.IsNullOrEmpty(secret)) return false;
        return secrets.Any(s =>
            (s.ExpiresAt is null || s.ExpiresAt > DateTimeOffset.UtcNow) &&
            Crypto.SecretEquals(secret, s.SecretHash));
    }
}
