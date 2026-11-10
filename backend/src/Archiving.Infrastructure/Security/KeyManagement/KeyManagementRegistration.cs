using Archiving.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Archiving.Infrastructure.Security.KeyManagement;

public static class KeyManagementRegistration
{
    /// <summary>
    /// Registers the KMS, the envelope service and the rotation machinery.
    ///
    /// <para><b>There is no unencrypted path.</b> The previous design read an optional key from
    /// configuration and fell back to writing plaintext when it was absent, which meant any deployment
    /// that skipped one setting archived everything in the clear and said nothing. Here the provider is
    /// always constructed, the Development-only provider refuses to load outside Development, and an
    /// unresolvable configuration throws at startup rather than degrading silently.</para>
    /// </summary>
    public static IServiceCollection AddKeyManagement(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<KmsOptions>(config.GetSection(KmsOptions.SectionName));

        var options = config.GetSection(KmsOptions.SectionName).Get<KmsOptions>() ?? new KmsOptions();
        var provider = (options.Provider ?? string.Empty).Trim();

        switch (provider.ToLowerInvariant())
        {
            case "dpapikeyring":
            case "":
                services.AddSingleton<IKekProvider, DpapiKeyringKekProvider>();
                break;

            case "externalkms":
                // Typed client: pooled handlers, and the KMS endpoint gets its own timeout and retry
                // policy rather than sharing the application's default one.
                services.AddHttpClient<IKekProvider, ExternalKmsKekProvider>(client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(15);
                });
                break;

            case "insecure":
                services.AddSingleton<IKekProvider, InsecureConfigKekProvider>();
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown Kms:Provider '{options.Provider}'. Valid values: DpapiKeyring, ExternalKms, Insecure (Development only).");
        }

        services.AddSingleton<IKeyManagementService, EnvelopeKeyManagementService>();
        services.AddScoped<IKeyRotationService, KeyRotationService>();
        services.AddHostedService<KeyRotationBackgroundService>();
        services.AddHostedService<KeyManagementStartupCheck>();

        return services;
    }
}

/// <summary>
/// Proves at startup that the KMS is reachable and its active KEK usable, and stops the host if it is not.
///
/// <para>Failing closed is the whole point. An archive that boots with a broken KMS accepts uploads it
/// cannot encrypt or silently writes them somewhere weaker, and nobody notices until a retrieval fails
/// months later. Better to refuse to start: an outage is visible and recoverable, a year of unprotected
/// documents is neither.</para>
/// </summary>
public sealed class KeyManagementStartupCheck(
    IKeyManagementService kms,
    IHostEnvironment env,
    IHostApplicationLifetime lifetime,
    ILogger<KeyManagementStartupCheck> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var health = await kms.CheckHealthAsync(ct).ConfigureAwait(false);

        if (health.Healthy)
        {
            logger.LogInformation("KMS ready — provider {Provider}, active KEK {KekId}, {Versions} version(s) available",
                health.Provider, health.ActiveKekId, health.KnownKeyVersions);

            if (string.Equals(health.Provider, "Insecure", StringComparison.OrdinalIgnoreCase))
                logger.LogWarning("KMS provider is 'Insecure' — the key encryption key is in a configuration " +
                                  "file. Acceptable in Development only.");
            return;
        }

        logger.LogCritical("KMS is not usable ({Provider}): {Error}", health.Provider, health.Error);

        if (env.IsDevelopment())
        {
            logger.LogWarning("Continuing anyway because the environment is Development. Uploads will fail.");
            return;
        }

        // Production: refuse to serve rather than archive anything unprotected.
        lifetime.StopApplication();
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
