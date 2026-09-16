using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace AvaEntra.Server;

public static class DevHttpsCertificate
{
    public const string DefaultPassword = "avaentra-dev-cert";

    public static X509Certificate2 Ensure(string contentRoot)
    {
        var dir = Path.Combine(contentRoot, "storage");
        Directory.CreateDirectory(dir);
        var pfxPath = Path.Combine(dir, "https-dev.pfx");

        if (File.Exists(pfxPath))
            return X509CertificateLoader.LoadPkcs12FromFile(pfxPath, DefaultPassword);

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=localhost",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                critical: true));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                [new Oid("1.3.6.1.5.5.7.3.1")],
                critical: true));

        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("localhost");
        san.AddDnsName("host.docker.internal");
        san.AddIpAddress(IPAddress.Loopback);
        san.AddIpAddress(IPAddress.IPv6Loopback);
        request.CertificateExtensions.Add(san.Build());

        using var created = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(5));

        var pfx = created.Export(X509ContentType.Pfx, DefaultPassword);
        File.WriteAllBytes(pfxPath, pfx);
        return X509CertificateLoader.LoadPkcs12(pfx, DefaultPassword);
    }
}
