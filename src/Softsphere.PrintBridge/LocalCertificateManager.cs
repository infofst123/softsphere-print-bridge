using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Softsphere.PrintBridge;

public static class LocalCertificateManager
{
    private const string CertificatePassword = "Softsphere-PrintBridge-Localhost";

    public static X509Certificate2 GetOrCreate(SettingsStore settingsStore)
    {
        var pfxPath = Path.Combine(settingsStore.SettingsDirectory, "localhost-server.pfx");
        var rootPath = Path.Combine(settingsStore.SettingsDirectory, "localhost-root.cer");

        if (!File.Exists(pfxPath) || !File.Exists(rootPath))
        {
            CreateCertificateFiles(pfxPath, rootPath);
        }

        var root = X509CertificateLoader.LoadCertificateFromFile(rootPath);
        TrustForCurrentUser(root);

        var server = X509CertificateLoader.LoadPkcs12FromFile(
            pfxPath,
            CertificatePassword,
            X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet
        );
        if (server.NotAfter.ToUniversalTime() <= DateTime.UtcNow.AddDays(7))
        {
            server.Dispose();
            root.Dispose();
            CreateCertificateFiles(pfxPath, rootPath);
            root = X509CertificateLoader.LoadCertificateFromFile(rootPath);
            TrustForCurrentUser(root);
            server = X509CertificateLoader.LoadPkcs12FromFile(
                pfxPath,
                CertificatePassword,
                X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet
            );
        }

        root.Dispose();
        return server;
    }

    private static void CreateCertificateFiles(string pfxPath, string rootPath)
    {
        using var rootKey = RSA.Create(2048);
        var rootRequest = new CertificateRequest(
            "CN=Softsphere Print Bridge Local Root",
            rootKey,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        rootRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true)
        );
        rootRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(rootRequest.PublicKey, false));

        using var root = rootRequest.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(10)
        );

        using var serverKey = RSA.Create(2048);
        var serverRequest = new CertificateRequest(
            "CN=localhost",
            serverKey,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        serverRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        serverRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true)
        );
        serverRequest.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new("1.3.6.1.5.5.7.3.1") },
                true
            )
        );
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("localhost");
        san.AddIpAddress(IPAddress.Loopback);
        san.AddIpAddress(IPAddress.IPv6Loopback);
        serverRequest.CertificateExtensions.Add(san.Build());

        var serial = RandomNumberGenerator.GetBytes(16);
        using var unsignedServer = serverRequest.Create(
            root,
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(5),
            serial
        );
        using var server = unsignedServer.CopyWithPrivateKey(serverKey);

        File.WriteAllBytes(rootPath, root.Export(X509ContentType.Cert));
        File.WriteAllBytes(pfxPath, server.Export(X509ContentType.Pfx, CertificatePassword));
    }

    private static void TrustForCurrentUser(X509Certificate2 root)
    {
        using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);
        var existing = store.Certificates.Find(X509FindType.FindByThumbprint, root.Thumbprint, false);
        if (existing.Count == 0)
        {
            store.Add(root);
        }
    }
}
