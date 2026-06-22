using Archiving.Application.Common.Interfaces;
using Archiving.Infrastructure.Auth;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Persistence.Interceptors;
using Archiving.Infrastructure.Security;
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

        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseMySQL(conn);
            options.AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>());
        });

        // Security & auth
        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();

        // Storage (local disk for MVP; swap for S3/MinIO in production)
        services.AddSingleton<IFileStorage, Storage.LocalFileStorage>();

        // Cross-cutting + modules
        services.AddScoped<IAuditWriter, Services.AuditWriter>();
        services.AddScoped<IIncomingMailService, Services.IncomingMailService>();
        services.AddScoped<IOutgoingMailService, Services.OutgoingMailService>();
        services.AddScoped<IOrganizationService, Services.OrganizationService>();
        services.AddScoped<IDocumentService, Services.DocumentService>();
        services.AddScoped<IWorkflowService, Services.WorkflowService>();
        services.AddScoped<INotificationService, Services.NotificationService>();
        services.AddScoped<IPhysicalArchiveService, Services.PhysicalArchiveService>();
        services.AddScoped<ILocationService, Services.LocationService>();
        services.AddScoped<ILifecycleService, Services.LifecycleService>();
        services.AddScoped<IDestructionEligibilityService, Services.DestructionEligibilityService>();
        services.AddScoped<ILegalHoldService, Services.LegalHoldService>();
        services.AddScoped<IDispositionService, Services.DispositionService>();
        services.AddScoped<ICertificateService, Services.CertificateService>();
        services.AddScoped<IDestructionService, Services.DestructionService>();
        services.AddScoped<IReportService, Services.ReportService>();
        services.AddScoped<IUserAdminService, Services.UserAdminService>();

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

        // Background jobs
        services.AddHostedService<Services.EscalationBackgroundService>();
        services.AddHostedService<Services.RetentionDispositionBackgroundService>();
        services.AddHostedService<Services.FixityVerificationBackgroundService>();
        services.AddHostedService<Services.TextExtractionBackgroundService>();

        return services;
    }
}
