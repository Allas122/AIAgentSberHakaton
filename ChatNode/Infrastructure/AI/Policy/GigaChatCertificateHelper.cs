using System.Net.Security;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace ChatNode.Infrastructure.AI.Policy;

public sealed class GigaChatCertificateHelper
{
    private readonly ILogger<GigaChatCertificateHelper> _logger;

    private readonly X509Certificate2? _gostRootCert;
    private readonly List<X509Certificate2> _trustedRoots = [];

    public GigaChatCertificateHelper(ILogger<GigaChatCertificateHelper> logger)
    {
        _logger = logger;

        var assembly = typeof(GigaChatCertificateHelper).Assembly;

        var rsaRootCert = LoadCert(assembly, "ChatNode.Resources.russian_ca.cer");
        _gostRootCert = LoadCert(assembly, "ChatNode.Resources.russian_ca_gost_2025.cer");

        if (rsaRootCert is not null) _trustedRoots.Add(rsaRootCert);
        if (_gostRootCert is not null) _trustedRoots.Add(_gostRootCert);
    }

    public SocketsHttpHandler CreateHandlerWithRussianCerts()
    {
        return new SocketsHttpHandler
        {
            SslOptions = new SslClientAuthenticationOptions
            {
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                RemoteCertificateValidationCallback = ValidateServerCertificate
            }
        };
    }

    private bool ValidateServerCertificate(
        object sender,
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        if (sslPolicyErrors == SslPolicyErrors.None) return true;

        if (sslPolicyErrors != SslPolicyErrors.RemoteCertificateChainErrors) return false;
        if (certificate is null || _trustedRoots.Count == 0) return false;

        var serverCert = certificate as X509Certificate2
                         ?? X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());

        using var verification = new X509Chain();
        verification.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        verification.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

        foreach (var root in _trustedRoots)
        {
            verification.ChainPolicy.CustomTrustStore.Add(root);
        }

        if (chain is not null)
        {
            foreach (var element in chain.ChainElements)
            {
                verification.ChainPolicy.ExtraStore.Add(element.Certificate);
            }
        }

        if (verification.Build(serverCert)) return true;

        if (IsUnverifiableGostChain(verification))
        {
            _logger.LogWarning(
                "GigaChat TLS: цепочка сходится к вшитому ГОСТ-корню Минцифры, но подпись проверить " +
                "нечем (нужен КриптоПро). Соединение принято по совпадению корня");

            return true;
        }

        LogRejection(serverCert, verification, sslPolicyErrors);

        return false;
    }

    private bool IsUnverifiableGostChain(X509Chain chain)
    {
        if (_gostRootCert is null || chain.ChainElements.Count == 0) return false;

        var root = chain.ChainElements[^1].Certificate;

        if (!string.Equals(root.Thumbprint, _gostRootCert.Thumbprint, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        const X509ChainStatusFlags tolerated =
            X509ChainStatusFlags.UntrustedRoot
            | X509ChainStatusFlags.NotSignatureValid;

        return chain.ChainStatus.Length > 0
               && chain.ChainStatus.All(status => (status.Status & ~tolerated) == 0);
    }

    private void LogRejection(X509Certificate2 serverCert, X509Chain chain, SslPolicyErrors errors)
    {
        _logger.LogError(
            "GigaChat TLS: сертификат отклонён. ошибки={Errors}, subject={Subject}, issuer={Issuer}",
            errors,
            serverCert.Subject,
            serverCert.Issuer);

        foreach (var element in chain.ChainElements)
        {
            var status = element.ChainElementStatus.Length == 0
                ? "ok"
                : string.Join(", ", element.ChainElementStatus.Select(s => s.Status.ToString()));

            _logger.LogError(
                "GigaChat TLS: звено цепочки {Subject} ({Status})",
                element.Certificate.Subject,
                status);
        }
    }

    private X509Certificate2? LoadCert(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName);

        if (stream is null)
        {
            _logger.LogError(
                "GigaChat TLS: встроенный ресурс {ResourceName} не найден — проверьте Build Action = Embedded Resource",
                resourceName);

            return null;
        }

        using var ms = new MemoryStream();
        stream.CopyTo(ms);

        return LoadCert(ms.ToArray());
    }

    private static X509Certificate2 LoadCert(byte[] raw)
    {
        var text = Encoding.ASCII.GetString(raw);

        return text.Contains("-----BEGIN CERTIFICATE-----", StringComparison.Ordinal)
            ? X509Certificate2.CreateFromPem(text)
            : X509CertificateLoader.LoadCertificate(raw);
    }
}
