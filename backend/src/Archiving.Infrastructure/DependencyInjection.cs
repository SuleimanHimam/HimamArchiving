using Archiving.Application.Common.Interfaces;
using Archiving.Infrastructure.Auth;
using Archiving.Infrastructure.EventHandlers;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Persistence.Interceptors;
using Archiving.Infrastructure.Security;
using Archiving.Infrastructure.Security.KeyManagement;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Archiving.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var conn = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");
        // MySQL or SQL Server, from Database:Provider — or read off the connection string when
        // that setting is absent, so existing deployments need no config change.
        var provider = DatabaseProviders.Parse(config["Database:Provider"], conn);

        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseArchivingDatabase(provider, conn);
            options.AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>());
        });

        // ---- In-process domain event bus (MediatR) ----
        // The assembly scan also registers open-generic handlers (e.g. EventLogHandler<>), so it must
        // NOT be registered again manually or every event would be logged twice.
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);  // Infrastructure handlers
            cfg.RegisterServicesFromAssembly(typeof(ICurrentUser).Assembly);          // Application handlers
        });

        // Security & auth
        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();

        // ---- Key management (envelope encryption) ----
        // Registered before storage: the object store will not construct without a working KMS, which is
        // what guarantees there is no code path that archives a document unencrypted.
        services.AddKeyManagement(config);

        // ---- Object storage: encrypted file server ----
        // LocalFileStorage stays registered as a concrete type, not as IFileStorage. It is no longer the
        // store — it is the reader for AENC1/AENC2/plaintext objects written before this change, so an
        // existing archive keeps opening while migration runs.
        services.AddSingleton<Storage.LocalFileStorage>();
        services.AddSingleton<IFileStorage, Storage.EncryptedFileServerStorage>();

        // Retrieval gate: RBAC + ABAC + clearance, all of it before a data key is ever unwrapped.
        services.AddScoped<Security.ISecureArchiveReader, Security.SecureArchiveReader>();

        // Licensing: offline, signature-verified, bound to this machine's fingerprint.
        // Singleton because the fingerprint and the verified licence are process-wide facts.
        services.Configure<Licensing.LicenseOptions>(config.GetSection(Licensing.LicenseOptions.SectionName));
        services.AddSingleton<ILicenseService, Licensing.LicenseService>();

        // Cross-cutting + modules
        services.AddScoped<IAuditWriter, Services.AuditWriter>();
        // Scoped, not transient: it caches the caller's roles and department for the request.
        services.AddScoped<IAccessScope, Services.AccessScope>();
        services.AddScoped<IOrganizationService, Services.OrganizationService>();
        services.AddScoped<IReportTemplateService, Services.ReportTemplateService>();
        services.AddScoped<IGroupService, Services.GroupService>();
        services.AddScoped<IDiwanService, Services.DiwanService>();
        services.AddScoped<IInternalMailService, Services.InternalMailService>();
        services.AddScoped<IDocumentService, Services.DocumentService>();
        // The engine, and the piece that turns its decisions into internal correspondence. The router
        // deliberately does not depend on IInternalMailService: composing is what starts a workflow, so
        // generated step mail must not go back through it.
        services.AddScoped<IWorkflowMailRouter, Services.WorkflowMailRouter>();
        services.AddScoped<IWorkflowService, Services.WorkflowService>();
        services.AddScoped<INotificationService, Services.NotificationService>();
        services.AddScoped<IPhysicalArchiveService, Services.PhysicalArchiveService>();
        services.AddScoped<IArchiveTreeService, Services.ArchiveTreeService>();
        services.AddScoped<ILocationService, Services.LocationService>();
        services.AddScoped<ILifecycleService, Services.LifecycleService>();
        services.AddScoped<IDestructionEligibilityService, Services.DestructionEligibilityService>();
        services.AddScoped<ILegalHoldService, Services.LegalHoldService>();
        services.AddScoped<IDispositionService, Services.DispositionService>();
        services.AddScoped<ICertificateService, Services.CertificateService>();
        services.AddScoped<IDestructionService, Services.DestructionService>();
        services.AddScoped<IReportService, Services.ReportService>();
        services.AddScoped<IUserAdminService, Services.UserAdminService>();
        services.AddScoped<IDelegationService, Services.DelegationService>();
        services.AddScoped<IEscalationService, Services.EscalationService>();
        services.AddScoped<IDocumentTimelineService, Services.DocumentTimelineService>();
        services.AddScoped<INumberingService, Services.NumberingService>();
        services.AddScoped<IIntegrationHookService, Services.IntegrationHookService>();
        services.AddScoped<IExternalDirectoryService, Services.ExternalDirectoryService>();
        services.AddHttpClient("webhooks", c => c.Timeout = TimeSpan.FromSeconds(15));

        // Preservation / integrity (ISO 16363, 15489, 19005)
        services.AddScoped<IFixityService, Services.FixityService>();
        services.AddScoped<IAuditVerificationService, Services.AuditVerificationService>();
        services.AddScoped<IPdfaValidator, Services.VeraPdfValidator>();
        services.AddScoped<IPreservationService, Services.PreservationService>();
        services.AddScoped<IPreservationPackageService, Services.PreservationPackageService>();
        services.AddScoped<IDesignatedCommunityService, Services.DesignatedCommunityService>();
        services.AddScoped<IRecordMetadataService, Services.RecordMetadataService>();
        services.AddScoped<IPreservationPolicyService, Services.PreservationPolicyService>();

        // Full-text search (extract words inside files): managed PDF text + Tesseract OCR for scans.
        services.AddSingleton<ITextExtractionService, Services.TextExtractionService>();
        services.AddScoped<ITextIndexingService, Services.TextIndexingService>();

        // QuestPDF runs under the Community License (see docs/iso-compliance.md — confirm eligibility).
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        // Must happen before the first render: without the vendored Arabic faces QuestPDF falls
        // back to a Latin font and Arabic prints as blank boxes.
        Services.LetterheadRenderer.RegisterFonts();

        // Slot-based letterhead for outgoing correspondence.
        services.AddSingleton<ILetterheadRenderer, Services.LetterheadRenderer>();
        services.AddScoped<ILetterheadService, Services.LetterheadService>();
        services.AddScoped<Services.LetterheadMigrator>();

        // Background jobs
        services.AddHostedService<Services.EscalationBackgroundService>();
        services.AddHostedService<Services.RetentionDispositionBackgroundService>();
        services.AddHostedService<Services.DelegationExpiryBackgroundService>();
        services.AddHostedService<Services.MetricsSnapshotBackgroundService>();
        services.AddHostedService<Services.WebhookDeliveryBackgroundService>();
        services.AddHostedService<Services.FixityVerificationBackgroundService>();
        services.AddHostedService<Services.TextExtractionBackgroundService>();

        return services;
    }
}
