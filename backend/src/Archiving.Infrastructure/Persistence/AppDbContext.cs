using Archiving.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // Identity & access
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<PositionAssignment> PositionAssignments => Set<PositionAssignment>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Organization
    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();

    // Documents
    public DbSet<DocumentCategory> DocumentCategories => Set<DocumentCategory>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentAttachment> DocumentAttachments => Set<DocumentAttachment>();

    // Preservation (ISO 16363 fixity + policy)
    public DbSet<FixityCheck> FixityChecks => Set<FixityCheck>();
    public DbSet<PreservationPolicy> PreservationPolicies => Set<PreservationPolicy>();

    // OAIS information packages (ISO 14721)
    public DbSet<InformationPackage> InformationPackages => Set<InformationPackage>();
    public DbSet<RepresentationInfo> RepresentationInfos => Set<RepresentationInfo>();
    public DbSet<DesignatedCommunity> DesignatedCommunities => Set<DesignatedCommunity>();

    // Records metadata (ISO 23081)
    public DbSet<RecordAgent> RecordAgents => Set<RecordAgent>();
    public DbSet<RecordRelationship> RecordRelationships => Set<RecordRelationship>();

    // Mail
    public DbSet<IncomingMail> IncomingMails => Set<IncomingMail>();
    public DbSet<OutgoingMail> OutgoingMails => Set<OutgoingMail>();
    public DbSet<MailAttachment> MailAttachments => Set<MailAttachment>();
    public DbSet<LetterTemplate> LetterTemplates => Set<LetterTemplate>();

    // Workflow
    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowStage> WorkflowStages => Set<WorkflowStage>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
    public DbSet<WorkflowTask> WorkflowTasks => Set<WorkflowTask>();

    // Lifecycle
    public DbSet<RetentionPolicy> RetentionPolicies => Set<RetentionPolicy>();
    public DbSet<RetentionAlert> RetentionAlerts => Set<RetentionAlert>();
    public DbSet<DisposalRequest> DisposalRequests => Set<DisposalRequest>();

    // Physical archive
    public DbSet<PhysicalLocation> PhysicalLocations => Set<PhysicalLocation>();
    public DbSet<PhysicalArchiveItem> PhysicalArchiveItems => Set<PhysicalArchiveItem>();

    // Notifications & audit
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Strings default to a sane, indexable length (MySQL otherwise uses longtext, which can't be indexed).
        builder.Properties<string>().HaveMaxLength(256);

        // Store DateOnly as datetime (Oracle MySQL provider can't read native date columns into DateOnly).
        builder.Properties<DateOnly>().HaveConversion<DateOnlyConverter>();
        builder.Properties<DateOnly?>().HaveConversion<NullableDateOnlyConverter>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // ---- Composite keys (join tables) ----
        b.Entity<RolePermission>().HasKey(x => new { x.RoleId, x.PermissionId });
        b.Entity<UserRole>().HasKey(x => new { x.UserId, x.RoleId });

        // ---- Unique indexes ----
        b.Entity<User>().HasIndex(x => x.Email).IsUnique();
        b.Entity<Role>().HasIndex(x => x.Name).IsUnique();
        b.Entity<Permission>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Institution>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Document>().HasIndex(x => x.DocumentNumber).IsUnique();
        b.Entity<IncomingMail>().HasIndex(x => x.TransactionNumber).IsUnique();
        b.Entity<OutgoingMail>().HasIndex(x => x.LetterNumber).IsUnique();
        b.Entity<RefreshToken>().HasIndex(x => x.Token).IsUnique();

        // ---- Helpful lookup indexes ----
        b.Entity<AuditLog>().HasIndex(x => x.CreatedAt);
        b.Entity<AuditLog>().HasIndex(x => new { x.EntityType, x.EntityId });
        b.Entity<Notification>().HasIndex(x => new { x.RecipientUserId, x.IsRead });
        b.Entity<WorkflowTask>().HasIndex(x => new { x.AssignedToPositionId, x.Status });
        b.Entity<IncomingMail>().HasIndex(x => x.Status);
        b.Entity<FixityCheck>().HasIndex(x => new { x.DocumentAttachmentId, x.CheckedAt });
        b.Entity<FixityCheck>().HasIndex(x => x.CheckedAt);
        b.Entity<InformationPackage>().HasIndex(x => new { x.DocumentId, x.Type });
        b.Entity<RepresentationInfo>().HasIndex(x => x.DocumentAttachmentId).IsUnique();
        b.Entity<RecordAgent>().HasIndex(x => new { x.RecordType, x.RecordId });
        b.Entity<RecordAgent>().HasIndex(x => new { x.RecordType, x.RecordId, x.AgentKind, x.AgentId, x.Role }).IsUnique();
        b.Entity<RecordRelationship>().HasIndex(x => new { x.SourceType, x.SourceId });
        b.Entity<RecordRelationship>().HasIndex(x => new { x.SourceType, x.SourceId, x.TargetType, x.TargetId, x.Type }).IsUnique();

        // ---- Long text columns ----
        b.Entity<Document>().Property(x => x.Description).HasColumnType("longtext");
        b.Entity<Document>().Property(x => x.Keywords).HasColumnType("longtext");
        b.Entity<DocumentAttachment>().Property(x => x.ExtractedText).HasColumnType("longtext");
        b.Entity<DocumentAttachment>().HasIndex(x => x.ExtractionStatus);
        b.Entity<IncomingMail>().Property(x => x.Body).HasColumnType("longtext");
        b.Entity<IncomingMail>().Property(x => x.Keywords).HasColumnType("longtext");
        b.Entity<OutgoingMail>().Property(x => x.Body).HasColumnType("longtext");
        b.Entity<LetterTemplate>().Property(x => x.HeaderHtml).HasColumnType("longtext");
        b.Entity<LetterTemplate>().Property(x => x.FooterHtml).HasColumnType("longtext");
        b.Entity<LetterTemplate>().Property(x => x.BodyTemplate).HasColumnType("longtext");
        b.Entity<WorkflowStage>().Property(x => x.TransitionCondition).HasColumnType("longtext");
        b.Entity<WorkflowTask>().Property(x => x.Note).HasColumnType("longtext");
        b.Entity<DisposalRequest>().Property(x => x.Justification).HasColumnType("longtext");
        b.Entity<Notification>().Property(x => x.Body).HasColumnType("longtext");
        b.Entity<InformationPackage>().Property(x => x.Manifest).HasColumnType("longtext");
        b.Entity<RepresentationInfo>().Property(x => x.RenderingNote).HasColumnType("longtext");
        b.Entity<DesignatedCommunity>().Property(x => x.RenderingExpectations).HasColumnType("longtext");
        b.Entity<AuditLog>().Property(x => x.OldValues).HasColumnType("longtext");
        b.Entity<AuditLog>().Property(x => x.NewValues).HasColumnType("longtext");
        b.Entity<AuditLog>().Property(x => x.UserAgent).HasMaxLength(512);
        b.Entity<AuditLog>().Property(x => x.Hash).HasMaxLength(128);
        b.Entity<AuditLog>().Property(x => x.PreviousHash).HasMaxLength(128);
        b.Entity<User>().Property(x => x.MfaSecret).HasMaxLength(512);

        // ---- Full-text search (MySQL) ----
        // Added via raw SQL in a dedicated migration (CREATE FULLTEXT INDEX) — the Oracle
        // EF Core provider has no IsFullText() fluent helper, and longtext columns can't take a plain index.

        // ---- Soft-delete query filters ----
        b.Entity<User>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<Document>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<IncomingMail>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<OutgoingMail>().HasQueryFilter(x => !x.IsDeleted);

        // ---- Explicit relationships EF can't unambiguously infer ----
        b.Entity<Position>()
            .HasOne(p => p.CurrentOccupant)
            .WithMany()
            .HasForeignKey(p => p.CurrentOccupantUserId);

        // Recipient's FK is RecipientUserId (doesn't match the convention "RecipientId",
        // so map it explicitly — otherwise EF adds a shadow RecipientId column).
        b.Entity<Notification>()
            .HasOne(n => n.Recipient)
            .WithMany()
            .HasForeignKey(n => n.RecipientUserId);

        // ---- Cascade: attachments follow their document; everything else restricts ----
        foreach (var fk in b.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;

        b.Entity<Document>()
            .HasMany(d => d.Attachments)
            .WithOne(a => a.Document)
            .HasForeignKey(a => a.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
