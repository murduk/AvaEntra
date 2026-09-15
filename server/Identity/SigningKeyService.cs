using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Nodes;

namespace AvaEntra.Server.Identity;

public sealed class SigningKeyService
{
    private readonly string _path;
    private RSA _rsa = RSA.Create(2048);
    private string _kid = "";
    private string _x5c = "";
    private string _x5t = "";

    public RSA Rsa => _rsa;
    public string Kid => _kid;
    public string X5c => _x5c;
    public string X5t => _x5t;

    public SigningKeyService(IWebHostEnvironment env)
    {
        var dir = Path.Combine(env.ContentRootPath, "data");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "signing-key.json");
    }

    public void Initialize()
    {
        if (File.Exists(_path))
        {
            var stored = System.Text.Json.JsonSerializer.Deserialize<StoredKey>(File.ReadAllText(_path))
                         ?? throw new InvalidOperationException("Invalid signing key file.");
            _rsa = RSA.Create();
            _rsa.ImportFromPem(stored.PrivateKeyPem);
            _kid = stored.Kid;
            _x5c = stored.CertBase64;
            _x5t = stored.X5t;
            return;
        }

        _rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=AvaEntra", _rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));
        var raw = cert.Export(X509ContentType.Cert);
        _x5c = Convert.ToBase64String(raw);
#pragma warning disable CA5350
        _x5t = Crypto.Base64UrlEncode(SHA1.HashData(raw));
#pragma warning restore CA5350
        var n = _rsa.ExportParameters(false).Modulus!;
        _kid = Convert.ToHexString(SHA256.HashData(n))[..16].ToLowerInvariant();

        var json = System.Text.Json.JsonSerializer.Serialize(new StoredKey
        {
            Kid = _kid,
            PrivateKeyPem = _rsa.ExportPkcs8PrivateKeyPem(),
            CertBase64 = _x5c,
            X5t = _x5t
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_path, json);
    }

    public JsonObject Jwk()
    {
        var p = _rsa.ExportParameters(false);
        return new JsonObject
        {
            ["kty"] = "RSA",
            ["use"] = "sig",
            ["kid"] = _kid,
            ["x5t"] = _x5t,
            ["n"] = Crypto.Base64UrlEncode(p.Modulus!),
            ["e"] = Crypto.Base64UrlEncode(p.Exponent!),
            ["x5c"] = new JsonArray(_x5c)
        };
    }

    private sealed class StoredKey
    {
        public string Kid { get; set; } = "";
        public string PrivateKeyPem { get; set; } = "";
        public string CertBase64 { get; set; } = "";
        public string X5t { get; set; } = "";
    }
}
