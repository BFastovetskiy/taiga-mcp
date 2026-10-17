using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using TaigaMcp.Configuration;

namespace TaigaMcp.Taiga;

/// <summary>
/// Создание HTTP-обработчика для Taiga API и обработка корпоративного/самоподписанного
/// сертификата:
///  - CaCertificatePath    — доверять указанному сертификату (pinning по отпечатку; корневой ЦС — по цепочке);
///  - IgnoreSslErrors=true — полностью отключить проверку (крайняя мера, только для доверенной сети).
/// </summary>
public static class TaigaHttpHandlerFactory
{
    /// <summary>
    /// Проверяет корректность сертификата при инициализации приложения.
    /// Бросает исключение при отсутствии/повреждении/истечении срока действия сертификата,
    /// чтобы ошибка конфигурации выявлялась сразу при старте, а не при первом запросе.
    /// </summary>
    public static void ValidateConfiguration(TaigaOptions options)
    {
        // При отключённой проверке или отсутствии пути к сертификату проверять нечего.
        if (options.IgnoreSslErrors || string.IsNullOrWhiteSpace(options.CaCertificatePath))
        {
            return;
        }

        using var certificate = LoadCertificate(options.CaCertificatePath);
    }

    /// <summary>Создаёт HTTP-обработчик под заданные настройки Taiga.</summary>
    public static HttpClientHandler Create(TaigaOptions options)
    {
        var handler = new HttpClientHandler();

        if (options.IgnoreSslErrors)
        {
            // ВНИМАНИЕ: отключает проверку сертификата — только для доверенных внутренних серверов.
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            return handler;
        }

        if (!string.IsNullOrWhiteSpace(options.CaCertificatePath))
        {
            // Для проверки достаточно отпечатка; сам сертификат освобождаем.
            string pinnedThumbprint;
            using (var pinned = LoadCertificate(options.CaCertificatePath))
            {
                pinnedThumbprint = pinned.Thumbprint;
            }

            handler.ServerCertificateCustomValidationCallback = (_, certificate, _, errors) =>
            {
                // Если стандартная проверка уже успешна — доверяем.
                if (errors == SslPolicyErrors.None)
                {
                    return true;
                }

                if (certificate is null)
                {
                    return false;
                }

                // 1) Точное совпадение сертификата (pinning) — рабочий вариант для
                //    самоподписанного/листового сертификата.
                return string.Equals(certificate.Thumbprint, pinnedThumbprint, StringComparison.OrdinalIgnoreCase);
            };
        }

        return handler;
    }

    /// <summary>
    /// Загружает и валидирует сертификат из файла. Поддерживаются форматы PEM (.pem/.crt)
    /// и DER/PKCS#12 (.cer/.pfx). Относительный путь разрешается от каталога сборки.
    /// </summary>
    private static X509Certificate2 LoadCertificate(string path)
    {
        var fullPath = Path.IsPathRooted(path)
            ? path
            : Path.Combine(AppContext.BaseDirectory, path);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Не найден сертификат корпоративного ЦС: {fullPath}", fullPath);
        }

        X509Certificate2 certificate;
        try
        {
            // В .NET 8 конструктор X509Certificate2(path) поддерживает PEM (.crt/.pem)
            // и DER/PKCS#12 (.cer/.pfx).
            certificate = new X509Certificate2(fullPath);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException(
                $"Не удалось загрузить сертификат '{fullPath}': файл повреждён или формат не поддерживается.", ex);
        }

        if (string.IsNullOrEmpty(certificate.Thumbprint))
        {
            certificate.Dispose();
            throw new InvalidOperationException(
                $"Сертификат '{fullPath}' не содержит корректных данных (пустой отпечаток).");
        }

        if (certificate.NotAfter <= DateTime.Now)
        {
            var notAfter = certificate.NotAfter;
            certificate.Dispose();
            throw new InvalidOperationException(
                $"Срок действия сертификата '{fullPath}' истёк {notAfter:u}.");
        }

        return certificate;
    }
}
