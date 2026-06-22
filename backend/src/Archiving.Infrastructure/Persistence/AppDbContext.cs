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

    // System settings
    public DbSet<ClassificationType>  ClassificationTypes  => Set<ClassificationType>();
    public DbSet<RoleClassification>  RoleClassifications  => Set<RoleClassification>();
    public DbSet<AutoBackupSettings>  AutoBackupSettings    => Set<AutoBackupSettings>();

    // Organization
    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();

    // Documents
    public DbSet<DocumentCategory> DocumentCategories => Set<DocumentCategory>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentAttachment> DocumentAttachments => Set<DocumentAttachment>();

    // Per-user features
    public DbSet<Folder> Folders => Set<Folder>();
    public DbSet<DocumentFavorite> DocumentFavorites => Set<DocumentFavorite>();
    public DbSet<DocumentShare> DocumentShares => Set<DocumentShare>();
    public DbSet<DocumentNote> DocumentNotes => Set<DocumentNote>();
    public DbSet<UserTablePref> UserTablePrefs => Set<UserTablePref>();
    public DbSet<CustomFieldDefinition> CustomFieldDefinitions => Set<CustomFieldDefinition>();
    public DbSet<CustomFieldValue> CustomFieldValues => Set<CustomFieldValue>();
    public DbSet<LegalHold> LegalHolds => Set<LegalHold>();
    public DbSet<DestructionRequest> DestructionRequests => Set<DestructionRequest>();
    public DbSet<DestructionItem> DestructionItems => Set<DestructionItem>();
    public DbSet<DestructionCertificate> DestructionCertificates => Set<DestructionCertificate>();
    public DbSet<DestructionMethodOption> DestructionMethodOptions => Set<DestructionMethodOption>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomConnection> RoomConnections => Set<RoomConnection>();
    public DbSet<Cabinet> Cabinets => Set<Cabinet>();
    public DbSet<Shelf> Shelves => Set<Shelf>();
    public DbSet<Box> Boxes => Set<Box>();

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
    public DbSet<DocumentRetention> DocumentRetentions => Set<DocumentRetention>();
    public DbSet<DispositionRequest> DispositionRequests => Set<DispositionRequest>();
    public DbSet<DispositionCertificate> DispositionCertificates => Set<DispositionCertificate>();

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
        b.Entity<RoleClassification>().HasKey(x => new { x.RoleId, x.ClassificationTypeId });

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
        b.Entity<DocumentNote>().Property(x => x.Content).HasColumnType("longtext");
        b.Entity<DocumentNote>().HasIndex(x => x.DocumentId);
        b.Entity<DocumentNote>().HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<DocumentFavorite>().HasIndex(x => new { x.UserId, x.DocumentId }).IsUnique();
        b.Entity<DocumentShare>().HasIndex(x => new { x.DocumentId, x.SharedWithUserId }).IsUnique();
        b.Entity<Folder>().HasIndex(x => x.UserId);
        b.Entity<UserTablePref>().Property(x => x.ConfigJson).HasColumnType("longtext");
        b.Entity<UserTablePref>().HasIndex(x => new { x.UserId, x.TableKey }).IsUnique();
        b.Entity<CustomFieldDefinition>().Property(x => x.Options).HasColumnType("longtext");
        b.Entity<CustomFieldDefinition>().HasIndex(x => x.EntityType);
        b.Entity<CustomFieldDefinition>().HasIndex(x => new { x.EntityType, x.FieldKey }).IsUnique();
        b.Entity<CustomFieldValue>().Property(x => x.Value).HasColumnType("longtext");
        b.Entity<CustomFieldValue>().HasIndex(x => new { x.EntityType, x.EntityId });
        b.Entity<CustomFieldValue>().HasIndex(x => new { x.FieldId, x.EntityId }).IsUnique();
        b.Entity<LegalHold>().Property(x => x.Reason).HasColumnType("longtext");
        b.Entity<LegalHold>().Property(x => x.QueryExpression).HasColumnType("longtext");
        b.Entity<LegalHold>().HasIndex(x => new { x.Scope, x.DocumentId });
        b.Entity<LegalHold>().HasIndex(x => x.ReleasedAtUtc);
        b.Entity<DestructionRequest>().Property(x => x.Reason).HasColumnType("longtext");
        b.Entity<DestructionRequest>().Property(x => x.DecisionNote).HasColumnType("longtext");
        b.Entity<DestructionRequest>().HasIndex(x => x.Status);
        b.Entity<DestructionItem>().Property(x => x.Outcome).HasColumnType("longtext");
        b.Entity<DestructionItem>().HasIndex(x => x.DocumentId);
        b.Entity<DestructionItem>().HasOne(x => x.Request).WithMany(x => x.Items)
            .HasForeignKey(x => x.DestructionRequestId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<DestructionCertificate>().HasIndex(x => x.DestructionRequestId);

        // ---- Retention & two-step Disposition ----
        b.Entity<DocumentRetention>().HasIndex(x => x.DocumentId);
        b.Entity<DocumentRetention>().HasIndex(x => x.ExpiryDate);
        b.Entity<DocumentRetention>().HasIndex(x => x.Status);
        b.Entity<DocumentRetention>().HasOne(x => x.Document).WithMany()
            .HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<DocumentRetention>().HasOne(x => x.RetentionPolicy).WithMany()
            .HasForeignKey(x => x.RetentionPolicyId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<DispositionRequest>().Property(x => x.Reason).HasColumnType("longtext");
        b.Entity<DispositionRequest>().Property(x => x.VerificationNotes).HasColumnType("longtext");
        b.Entity<DispositionRequest>().Property(x => x.FinalApprovalNotes).HasColumnType("longtext");
        b.Entity<DispositionRequest>().Property(x => x.RejectionReason).HasColumnType("longtext");
        b.Entity<DispositionRequest>().Property(x => x.CustomMethod).HasColumnType("longtext");
        b.Entity<DispositionRequest>().HasIndex(x => x.Status);
        b.Entity<DispositionRequest>().HasIndex(x => x.DocumentId);
        b.Entity<DispositionRequest>().HasIndex(x => x.RequestedAction);
        b.Entity<DispositionRequest>().HasOne(x => x.Document).WithMany()
            .HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<DispositionCertificate>().Property(x => x.DocumentIds).HasColumnType("longtext");
        b.Entity<DispositionCertificate>().HasIndex(x => x.DispositionRequestId);
        b.Entity<DispositionCertificate>().HasIndex(x => x.CertificateNumber).IsUnique();

        // ---- Normalized physical-location hierarchy ----
        b.Entity<Building>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Building>().Property(x => x.Notes).HasColumnType("longtext");
        b.Entity<Room>().HasIndex(x => x.BuildingId);
        b.Entity<Room>().Property(x => x.Notes).HasColumnType("longtext");
        b.Entity<Room>().HasOne(x => x.Building).WithMany(x => x.Rooms).HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<RoomConnection>().HasIndex(x => new { x.RoomId, x.ConnectedRoomId }).IsUnique();
        b.Entity<RoomConnection>().HasOne(x => x.Room).WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<RoomConnection>().HasOne(x => x.ConnectedRoom).WithMany().HasForeignKey(x => x.ConnectedRoomId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Cabinet>().HasIndex(x => x.RoomId);
        b.Entity<Cabinet>().Property(x => x.Notes).HasColumnType("longtext");
        b.Entity<Cabinet>().HasOne(x => x.Room).WithMany(x => x.Cabinets).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Shelf>().HasIndex(x => x.CabinetId);
        b.Entity<Shelf>().HasOne(x => x.Cabinet).WithMany(x => x.Shelves).HasForeignKey(x => x.CabinetId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Box>().HasIndex(x => x.BoxCode).IsUnique();
        b.Entity<Box>().HasIndex(x => x.ShelfId);
        b.Entity<Box>().Property(x => x.Notes).HasColumnType("longtext");
        b.Entity<Box>().HasOne(x => x.Shelf).WithMany(x => x.Boxes).HasForeignKey(x => x.ShelfId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Box>().HasOne(x => x.Room).WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Document>().HasIndex(x => x.BoxId);
        b.Entity<DocumentFavorite>().HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<DocumentShare>().HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
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
        b.Entity<Institution>().Property(x => x.LogoBase64).HasColumnType("longtext");
        b.Entity<AutoBackupSettings>().Property(x => x.LastRunError).HasColumnType("longtext");
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
