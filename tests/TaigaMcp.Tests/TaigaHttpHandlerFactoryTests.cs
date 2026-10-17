using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using TaigaMcp.Configuration;
using TaigaMcp.Taiga;
using Xunit;

namespace TaigaMcp.Tests;

public class TaigaHttpHandlerFactoryTests
{
    private static string WriteCertificatePem(DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        var path = Path.Combine(Path.GetTempPath(), $"pin-{Guid.NewGuid():N}.pem");
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=taiga.test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(notBefore, notAfter);
        File.WriteAllText(path, certificate.ExportCertificatePem());
        return path;
    }

    [Fact]
    public void IgnoreSslErrors_SetsDangerousValidator()
    {
        var options = new TaigaOptions { IgnoreSslErrors = true };

        var handler = Assert.IsType<HttpClientHandler>(TaigaHttpHandlerFactory.Create(options));

        Assert.NotNull(handler.ServerCertificateCustomValidationCallback);
    }

    [Fact]
    public void MissingCertificate_Throws()
    {
        var options = new TaigaOptions { CaCertificatePath = "does-not-exist.pem" };

        Assert.Throws<FileNotFoundException>(() => TaigaHttpHandlerFactory.Create(options));
    }

    [Fact]
    public void DamagedCertificate_ThrowsInvalidOperation()
    {
        var path = Path.Combine(Path.GetTempPath(), $"broken-{Guid.NewGuid():N}.pem");
        File.WriteAllText(path, "это вовсе не сертификат");

        try
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => TaigaHttpHandlerFactory.Create(new TaigaOptions { CaCertificatePath = path }));
            Assert.Contains("Не удалось загрузить сертификат", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ExpiredCertificate_ThrowsInvalidOperation()
    {
        var path = WriteCertificatePem(
            DateTimeOffset.UtcNow.AddDays(-10),
            DateTimeOffset.UtcNow.AddDays(-1));

        try
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => TaigaHttpHandlerFactory.Create(new TaigaOptions { CaCertificatePath = path }));
            Assert.Contains("истёк", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ValidateConfiguration_WhenIgnoreSslErrors_DoesNotThrow()
    {
        TaigaHttpHandlerFactory.ValidateConfiguration(new TaigaOptions
        {
            IgnoreSslErrors = true,
            CaCertificatePath = "does-not-exist.pem",
        });
    }

    [Fact]
    public void ValidateConfiguration_WithoutCertificate_DoesNotThrow()
    {
        TaigaHttpHandlerFactory.ValidateConfiguration(new TaigaOptions());
    }

    [Fact]
    public void ValidateConfiguration_MissingCertificate_ThrowsAtStartup()
    {
        Assert.Throws<FileNotFoundException>(() => TaigaHttpHandlerFactory.ValidateConfiguration(
            new TaigaOptions { CaCertificatePath = "still-missing.pem" }));
    }

    [Fact]
    public void ValidateConfiguration_ValidCertificate_DoesNotThrow()
    {
        var path = WriteCertificatePem(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

        try
        {
            TaigaHttpHandlerFactory.ValidateConfiguration(new TaigaOptions { CaCertificatePath = path });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PinnedCertificate_AcceptsMatchingThumbprint()
    {
        var path = WriteCertificatePem(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

        try
        {
            var handler = Assert.IsType<HttpClientHandler>(
                TaigaHttpHandlerFactory.Create(new TaigaOptions { CaCertificatePath = path }));
            var validate = handler.ServerCertificateCustomValidationCallback;
            Assert.NotNull(validate);

            using var loaded = new X509Certificate2(path);
            var accepted = validate!(null!, loaded, null, SslPolicyErrors.RemoteCertificateNameMismatch);

            Assert.True(accepted);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PinnedCertificate_RejectsDifferentThumbprint()
    {
        var pinnedPath = WriteCertificatePem(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        var otherPath = WriteCertificatePem(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

        try
        {
            var handler = Assert.IsType<HttpClientHandler>(
                TaigaHttpHandlerFactory.Create(new TaigaOptions { CaCertificatePath = pinnedPath }));
            var validate = handler.ServerCertificateCustomValidationCallback;
            Assert.NotNull(validate);

            using var other = new X509Certificate2(otherPath);
            var accepted = validate!(null!, other, null, SslPolicyErrors.RemoteCertificateNameMismatch);

            Assert.False(accepted);
        }
        finally
        {
            File.Delete(pinnedPath);
            File.Delete(otherPath);
        }
    }

    [Fact]
    public void PinnedCertificate_AcceptsWhenStandardValidationSucceeds()
    {
        var path = WriteCertificatePem(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

        try
        {
            var handler = Assert.IsType<HttpClientHandler>(
                TaigaHttpHandlerFactory.Create(new TaigaOptions { CaCertificatePath = path }));
            var validate = handler.ServerCertificateCustomValidationCallback;
            Assert.NotNull(validate);

            // Ошибок нет — доверяем независимо от отпечатка.
            var accepted = validate!(null!, null, null, SslPolicyErrors.None);

            Assert.True(accepted);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
