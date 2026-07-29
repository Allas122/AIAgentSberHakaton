using System.Net.Security;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace ChatNode.Infrastructure.AI.Policy;

public static class GigaChatCertificateHelper
{
    private static readonly X509Certificate2? RootCert;
    private static readonly X509Certificate2? SubCert;

    static GigaChatCertificateHelper()
    {
        var assembly = typeof(GigaChatCertificateHelper).Assembly;
        RootCert = LoadCert(assembly, "ChatNode.Resources.russian_ca.cer");
        SubCert = LoadCert(assembly, "ChatNode.Resources.russian_ca_gost_2025.cer");
    }
    public static SocketsHttpHandler CreateHandlerWithRussianCerts()
    {
        var handler = new SocketsHttpHandler
        {
            SslOptions = new SslClientAuthenticationOptions
            {
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                RemoteCertificateValidationCallback = ValidateServerCertificate
            }
        };

        return handler;
    }

    private static bool ValidateServerCertificate(
        object sender, 
        X509Certificate? certificate, 
        X509Chain? chain, 
        SslPolicyErrors sslPolicyErrors)
    {
        if (sslPolicyErrors == SslPolicyErrors.None) return true;
        if (chain == null || certificate == null || RootCert == null || SubCert == null) return false;
        
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
        
        chain.ChainPolicy.ExtraStore.Add(RootCert);
        chain.ChainPolicy.ExtraStore.Add(SubCert);
        
        var isValid = chain.Build((X509Certificate2)certificate);
        if (isValid) return true;
        
        foreach (var element in chain.ChainElements)
        {
            var thumbprint = element.Certificate.Thumbprint;
            if (thumbprint == RootCert.Thumbprint || thumbprint == SubCert.Thumbprint)
            {
                return true;
            }
        }

        return false;
    }

    private static X509Certificate2 LoadCert(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null) 
            throw new FileNotFoundException($"Embedded resource '{resourceName}' not found. Ensure Build Action is set to 'Embedded Resource'.");
        
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        
        return new X509Certificate2(ms.ToArray(), (string?)null, X509KeyStorageFlags.EphemeralKeySet);
    }
}
