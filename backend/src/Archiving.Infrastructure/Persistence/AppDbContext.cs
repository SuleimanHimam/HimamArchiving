using Archiving.Domain.Common;
using Archiving.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    private readonly IPublisher? _publisher;

    /// <summary>Runtime constructor — receives the MediatR publisher used to dispatch domain events.</summary>
    public AppDbContext(DbContextOptions<AppDbContext> options, IPublisher publisher) : base(options)
        => _publisher = publisher;

    /// <summary>Design-time constructor (dotnet ef) — no publisher; events are never dispatched at design time.</summary>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Identity & access
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserPermissionGrant> UserPermissionGrants => Set<UserPermissionGrant>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<PositionAssignment> PositionAssignments => Set<PositionAssignment>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserOrgUnitAccess> UserOrgUnitAccess => Set<UserOrgUnitAccess>();

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
    public DbSet<UserFile> UserFiles => Set<UserFile>();
    public DbSet<DocumentFavorite> DocumentFavorites => Set<DocumentFavorite>();
    public DbSet<DocumentShare> DocumentShares => Set<DocumentShare>();
    public DbSet<FolderShare> FolderShares => Set<FolderShare>();
    public DbSet<UserFileShare> UserFileShares => Set<UserFileShare>();
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

    /// <summary>Encryption metadata index — one row per encrypted object on the file server.
    /// Holds the wrapped DEK, so crypto-shredding and key rotation are database operations.</summary>
    public DbSet<ArchivedObject> ArchivedObjects => Set<ArchivedObject>();
    public DbSet<PreservationPolicy> PreservationPolicies => Set<PreservationPolicy>();

    // OAIS information packages (ISO 14721)
    public DbSet<InformationPackage> InformationPackages => Set<InformationPackage>();
    public DbSet<RepresentationInfo> RepresentationInfos => Set<RepresentationInfo>();
    public DbSet<DesignatedCommunity> DesignatedCommunities => Set<DesignatedCommunity>();

    // Records metadata (ISO 23081)
    public DbSet<RecordAgent> RecordAgents => Set<RecordAgent>();
    public DbSet<RecordRelationship> RecordRelationships => Set<RecordRelationship>();

    // Mail
    public DbSet<ReportTemplate> ReportTemplates => Set<ReportTemplate>();
    public DbSet<CorrespondenceGroup> CorrespondenceGroups => Set<CorrespondenceGroup>();
    public DbSet<CorrespondenceGroupMember> CorrespondenceGroupMembers => Set<CorrespondenceGroupMember>();

    // ── الديوان (external register) ──
    public DbSet<DiwanMail> DiwanMails => Set<DiwanMail>();
    public DbSet<DiwanAttachment> DiwanAttachments => Set<DiwanAttachment>();
    public DbSet<DiwanBureau> DiwanBureaus => Set<DiwanBureau>();
    public DbSet<DiwanAccess> DiwanAccess => Set<DiwanAccess>();
    public DbSet<MailCategory> MailCategories => Set<MailCategory>();
    // ── المراسلات (internal correspondence) ──
    public DbSet<InternalMail> InternalMails => Set<InternalMail>();
    public DbSet<InternalRecipient> InternalRecipients => Set<InternalRecipient>();
    public DbSet<InternalRead> InternalReads => Set<InternalRead>();
    public DbSet<InternalAttachment> InternalAttachments => Set<InternalAttachment>();

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

    // Domain event audit trail (Phase 1)
    public DbSet<EventLog> EventLogs => Set<EventLog>();

    // Delegation & substitution (Phase 5)
    public DbSet<PositionDelegation> PositionDelegations => Set<PositionDelegation>();

    // Escalation engine (Phase 6)
    public DbSet<EscalationRule> EscalationRules => Set<EscalationRule>();
    public DbSet<EscalationLog> EscalationLogs => Set<EscalationLog>();

    // Numbering engine (Phase 9)
    public DbSet<NumberingTemplate> NumberingTemplates => Set<NumberingTemplate>();

    // Reporting / KPI (Phase 10)
    public DbSet<DailyMetricSnapshot> DailyMetricSnapshots => Set<DailyMetricSnapshot>();

    // Integration hooks / webhooks (Phase 11)
    public DbSet<IntegrationHook> IntegrationHooks => Set<IntegrationHook>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();

    // External organizations & contacts (EXT, v1.1)
    public DbSet<ExternalOrganization> ExternalOrganizations => Set<ExternalOrganization>();
    public DbSet<ExternalContact> ExternalContacts => Set<ExternalContact>();
    public DbSet<ContactChannel> ContactChannels => Set<ContactChannel>();

    // Records classification: File Plan + Record Series (RM, v1.1)


    /// <summary>Persists changes, then publishes the domain events raised by the affected entities.
    /// Events fire only after a successful save, and after Ids are assigned to new rows.</summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var result = await base.SaveChangesAsync(cancellationToken);
        if (_publisher is not null) await DispatchDomainEventsAsync(cancellationToken);
        return result;
    }

    private async Task DispatchDomainEventsAsync(CancellationToken ct)
    {
        var entities = ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var events = entities.SelectMany(e => e.DomainEvents).ToList();
        foreach (var entity in entities) entity.ClearDomainEvents();

        // Handlers may persist their own changes (which re-enters this override) — events are already cleared,
        // so there is no re-publish loop.
        foreach (var domainEvent in events) await _publisher!.Publish(domainEvent, ct);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Strings default to a sane, indexable length (MySQL otherwise uses longtext, which can't be indexed).
        builder.Properties<string>().HaveMaxLength(256);

        // Store DateOnly as datetime (Oracle MySQL provider can't read native date columns into DateOnly).
        builder.Properties<DateOnly>().HaveConversion<DateOnlyConverter>();
        builder.Properties<DateOnly?>().HaveConversion<NullableDateOnlyConverter>();
    }

    /// <summary>The engine's "text of any length" column — see <see cref="DatabaseProviders"/>.
    /// Resolved per context because the same model builds against MySQL and SQL Server.</summary>
    private string LongText => Database.LongText();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // ---- Domain events are in-process plumbing, never persisted ----
        foreach (var entityType in b.Model.GetEntityTypes().ToList())
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                b.Entity(entityType.ClrType).Ignore(nameof(BaseEntity.DomainEvents));

        // ---- Event audit trail ----
        b.Entity<EventLog>().Property(x => x.EventData).HasColumnType(LongText);
        b.Entity<EventLog>().HasIndex(x => x.EventType);
        b.Entity<EventLog>().HasIndex(x => x.OccurredAt);

        // ---- Escalation engine (Phase 6) ----
        b.Entity<EscalationRule>().HasIndex(x => new { x.WorkflowDefinitionId, x.WorkflowStageId, x.IsActive });
        b.Entity<EscalationLog>().HasIndex(x => new { x.WorkflowInstanceId, x.WorkflowStageId, x.EscalationLevel });

        // ---- Numbering engine (Phase 9) ----
        b.Entity<NumberingTemplate>().HasIndex(x => new { x.EntityType, x.InstitutionId, x.OrgUnitId, x.DocumentTypeId, x.IsActive });

        // ---- Reporting / KPI (Phase 10) ----
        b.Entity<DailyMetricSnapshot>().Property(x => x.Value).HasPrecision(18, 2);
        b.Entity<DailyMetricSnapshot>().Property(x => x.Breakdown).HasColumnType(LongText);
        b.Entity<DailyMetricSnapshot>().HasIndex(x => new { x.MetricType, x.Date });

        // ---- External organizations & contacts (EXT, v1.1) ----
        b.Entity<ExternalOrganization>().Property(x => x.Notes).HasColumnType(LongText);
        b.Entity<ExternalOrganization>().HasIndex(x => x.Name);
        b.Entity<ExternalOrganization>().HasIndex(x => x.Code);
        b.Entity<ExternalOrganization>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<ExternalContact>().HasIndex(x => x.OrganizationId);
        b.Entity<ExternalContact>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<ExternalContact>().HasOne(x => x.Organization).WithMany(o => o.Contacts)
            .HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<ContactChannel>().HasIndex(x => x.ContactId);
        b.Entity<ContactChannel>().HasOne(x => x.Contact).WithMany(c => c.Channels)
            .HasForeignKey(x => x.ContactId).OnDelete(DeleteBehavior.Cascade);

        // ---- Integration hooks / webhooks (Phase 11) ----
        b.Entity<IntegrationHook>().Property(x => x.HeadersJson).HasColumnType(LongText);
        b.Entity<IntegrationHook>().Property(x => x.CallbackUrl).HasMaxLength(1024);
        b.Entity<IntegrationHook>().HasIndex(x => new { x.EventType, x.IsActive });
        b.Entity<WebhookDelivery>().Property(x => x.PayloadJson).HasColumnType(LongText);
        b.Entity<WebhookDelivery>().Property(x => x.ResponseBody).HasColumnType(LongText);
        b.Entity<WebhookDelivery>().HasIndex(x => new { x.Status, x.NextAttemptAt });
        b.Entity<WebhookDelivery>().HasIndex(x => x.IntegrationHookId);

        // ---- Position delegation (Phase 5) ----
        b.Entity<PositionDelegation>().Property(x => x.Reason).HasColumnType(LongText);
        b.Entity<PositionDelegation>().HasIndex(x => new { x.ToPositionId, x.IsActive });
        b.Entity<PositionDelegation>().HasIndex(x => new { x.FromPositionId, x.IsActive });
        b.Entity<PositionDelegation>().HasOne(x => x.FromPosition).WithMany()
            .HasForeignKey(x => x.FromPositionId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PositionDelegation>().HasOne(x => x.ToPosition).WithMany()
            .HasForeignKey(x => x.ToPositionId).OnDelete(DeleteBehavior.Restrict);

        // ---- Composite keys (join tables) ----
        b.Entity<RolePermission>().HasKey(x => new { x.RoleId, x.PermissionId });
        b.Entity<UserRole>().HasKey(x => new { x.UserId, x.RoleId });
        b.Entity<RoleClassification>().HasKey(x => new { x.RoleId, x.ClassificationTypeId });

        // ---- One-off permissions given to one person ----
        // One row per (user, permission): granting the same thing twice is not a second grant.
        // Cascade is configured after the blanket Restrict pass below, for the same reason the
        // department grants are — a leftover row would follow a reused id.
        b.Entity<UserPermissionGrant>().HasIndex(x => new { x.UserId, x.PermissionId }).IsUnique();

        // ---- Extra departments a user may act in ----
        // One row per (user, unit): granting the same department twice is not a second grant.
        // The cascade this needs is configured after the blanket Restrict pass below.
        b.Entity<UserOrgUnitAccess>().HasIndex(x => new { x.UserId, x.OrgUnitId }).IsUnique();

        // ---- Staff let in on a single external letter ----
        // One row per (letter, user), for the same reason. Cascade is configured below too.
        b.Entity<DiwanAccess>().HasIndex(x => new { x.DiwanMailId, x.UserId }).IsUnique();

        // ---- Unique indexes ----
        b.Entity<User>().HasIndex(x => x.Email).IsUnique();
        b.Entity<Role>().HasIndex(x => x.Name).IsUnique();
        b.Entity<Permission>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Institution>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Document>().HasIndex(x => x.DocumentNumber).IsUnique();
        b.Entity<RefreshToken>().HasIndex(x => x.Token).IsUnique();

        // ---- Helpful lookup indexes ----
        b.Entity<AuditLog>().HasIndex(x => x.CreatedAt);
        b.Entity<AuditLog>().HasIndex(x => new { x.EntityType, x.EntityId });
        b.Entity<Notification>().HasIndex(x => new { x.RecipientUserId, x.IsRead });
        b.Entity<WorkflowTask>().HasIndex(x => new { x.AssignedToPositionId, x.Status });
        b.Entity<FixityCheck>().HasIndex(x => new { x.DocumentAttachmentId, x.CheckedAt });
        b.Entity<FixityCheck>().HasIndex(x => x.CheckedAt);

        // ---- Encryption metadata index ----
        // StorageKey is the natural key and is looked up on every read, so it is unique and indexed.
        // KekId is indexed because key rotation selects on it across the whole archive.
        b.Entity<ArchivedObject>().HasIndex(x => x.StorageKey).IsUnique();
        b.Entity<ArchivedObject>().HasIndex(x => x.KekId);
        b.Entity<ArchivedObject>().HasIndex(x => x.ShreddedAtUtc);
        b.Entity<ArchivedObject>().Property(x => x.StorageKey).HasMaxLength(512).IsRequired();
        b.Entity<ArchivedObject>().Property(x => x.ChecksumSha256).HasMaxLength(128);
        b.Entity<ArchivedObject>().Property(x => x.Algorithm).HasMaxLength(64);
        b.Entity<ArchivedObject>().Property(x => x.KekId).HasMaxLength(128).IsRequired();
        // Bounded binary rather than a blob type: a wrapped 32-byte DEK is ~60 bytes for the local
        // provider and a few hundred for Vault's envelope string. 1 KiB is generous and keeps the row
        // inline, which matters because this column is read on every single retrieval.
        b.Entity<ArchivedObject>().Property(x => x.WrappedDek).HasMaxLength(1024);
        b.Entity<ArchivedObject>().Property(x => x.NoncePrefix).HasMaxLength(16);
        b.Entity<InformationPackage>().HasIndex(x => new { x.DocumentId, x.Type });
        b.Entity<RepresentationInfo>().HasIndex(x => x.DocumentAttachmentId).IsUnique();
        b.Entity<RecordAgent>().HasIndex(x => new { x.RecordType, x.RecordId });
        b.Entity<RecordAgent>().HasIndex(x => new { x.RecordType, x.RecordId, x.AgentKind, x.AgentId, x.Role }).IsUnique();
        b.Entity<RecordRelationship>().HasIndex(x => new { x.SourceType, x.SourceId });
        b.Entity<RecordRelationship>().HasIndex(x => new { x.SourceType, x.SourceId, x.TargetType, x.TargetId, x.Type }).IsUnique();

        // ---- Long text columns ----
        b.Entity<Document>().Property(x => x.Description).HasColumnType(LongText);
        b.Entity<Document>().Property(x => x.Keywords).HasColumnType(LongText);
        b.Entity<DocumentAttachment>().Property(x => x.ExtractedText).HasColumnType(LongText);
        b.Entity<DocumentAttachment>().HasIndex(x => x.ExtractionStatus);
        b.Entity<DocumentNote>().Property(x => x.Content).HasColumnType(LongText);
        b.Entity<DocumentNote>().HasIndex(x => x.DocumentId);
        b.Entity<DocumentNote>().HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<DocumentFavorite>().HasIndex(x => new { x.UserId, x.DocumentId }).IsUnique();
        b.Entity<DocumentShare>().HasIndex(x => new { x.DocumentId, x.SharedWithUserId }).IsUnique();
        b.Entity<Folder>().HasIndex(x => x.UserId);
        // One grant per (item, person): re-sharing changes the level rather than stacking rows.
        b.Entity<FolderShare>().HasIndex(x => new { x.FolderId, x.SharedWithUserId }).IsUnique();
        b.Entity<FolderShare>().HasIndex(x => x.SharedWithUserId);
        b.Entity<UserFileShare>().HasIndex(x => new { x.UserFileId, x.SharedWithUserId }).IsUnique();
        b.Entity<UserFileShare>().HasIndex(x => x.SharedWithUserId);
        b.Entity<UserTablePref>().Property(x => x.ConfigJson).HasColumnType(LongText);
        b.Entity<UserTablePref>().HasIndex(x => new { x.UserId, x.TableKey }).IsUnique();
        b.Entity<CustomFieldDefinition>().Property(x => x.Options).HasColumnType(LongText);
        b.Entity<CustomFieldDefinition>().HasIndex(x => x.EntityType);
        b.Entity<CustomFieldDefinition>().HasIndex(x => new { x.EntityType, x.FieldKey }).IsUnique();
        // Listing a record's fields always filters on the pair (shared fields + the record's own type).
        b.Entity<CustomFieldDefinition>().HasIndex(x => new { x.EntityType, x.DocumentTypeId });
        // Restrict, like every FK here (see the sweep at the end of this method). The type's own
        // fields are cleaned up explicitly in DocumentService.DeleteTypeAsync.
        b.Entity<CustomFieldDefinition>().HasOne<DocumentType>().WithMany()
            .HasForeignKey(x => x.DocumentTypeId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CustomFieldValue>().Property(x => x.Value).HasColumnType(LongText);
        b.Entity<CustomFieldValue>().HasIndex(x => new { x.EntityType, x.EntityId });
        b.Entity<CustomFieldValue>().HasIndex(x => new { x.FieldId, x.EntityId }).IsUnique();
        b.Entity<LegalHold>().Property(x => x.Reason).HasColumnType(LongText);
        b.Entity<LegalHold>().Property(x => x.QueryExpression).HasColumnType(LongText);
        b.Entity<LegalHold>().HasIndex(x => new { x.Scope, x.DocumentId });
        b.Entity<LegalHold>().HasIndex(x => x.ReleasedAtUtc);
        b.Entity<DestructionRequest>().Property(x => x.Reason).HasColumnType(LongText);
        b.Entity<DestructionRequest>().Property(x => x.DecisionNote).HasColumnType(LongText);
        b.Entity<DestructionRequest>().HasIndex(x => x.Status);
        b.Entity<DestructionItem>().Property(x => x.Outcome).HasColumnType(LongText);
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

        b.Entity<DispositionRequest>().Property(x => x.Reason).HasColumnType(LongText);
        b.Entity<DispositionRequest>().Property(x => x.VerificationNotes).HasColumnType(LongText);
        b.Entity<DispositionRequest>().Property(x => x.FinalApprovalNotes).HasColumnType(LongText);
        b.Entity<DispositionRequest>().Property(x => x.RejectionReason).HasColumnType(LongText);
        b.Entity<DispositionRequest>().Property(x => x.CustomMethod).HasColumnType(LongText);
        b.Entity<DispositionRequest>().HasIndex(x => x.Status);
        b.Entity<DispositionRequest>().HasIndex(x => x.DocumentId);
        b.Entity<DispositionRequest>().HasIndex(x => x.RequestedAction);
        b.Entity<DispositionRequest>().HasOne(x => x.Document).WithMany()
            .HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<DispositionCertificate>().Property(x => x.DocumentIds).HasColumnType(LongText);
        b.Entity<DispositionCertificate>().HasIndex(x => x.DispositionRequestId);
        b.Entity<DispositionCertificate>().HasIndex(x => x.CertificateNumber).IsUnique();

        // ---- Inter-department correspondence ----

        // ---- Normalized physical-location hierarchy ----
        b.Entity<Building>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Building>().Property(x => x.Notes).HasColumnType(LongText);
        b.Entity<Room>().HasIndex(x => x.BuildingId);
        b.Entity<Room>().Property(x => x.Notes).HasColumnType(LongText);
        b.Entity<Room>().HasOne(x => x.Building).WithMany(x => x.Rooms).HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<RoomConnection>().HasIndex(x => new { x.RoomId, x.ConnectedRoomId }).IsUnique();
        b.Entity<RoomConnection>().HasOne(x => x.Room).WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<RoomConnection>().HasOne(x => x.ConnectedRoom).WithMany().HasForeignKey(x => x.ConnectedRoomId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Cabinet>().HasIndex(x => x.RoomId);
        b.Entity<Cabinet>().Property(x => x.Notes).HasColumnType(LongText);
        b.Entity<Cabinet>().HasOne(x => x.Room).WithMany(x => x.Cabinets).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Shelf>().HasIndex(x => x.CabinetId);
        b.Entity<Shelf>().HasOne(x => x.Cabinet).WithMany(x => x.Shelves).HasForeignKey(x => x.CabinetId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Box>().HasIndex(x => x.BoxCode).IsUnique();
        b.Entity<Box>().HasIndex(x => x.ShelfId);
        b.Entity<Box>().Property(x => x.Notes).HasColumnType(LongText);
        b.Entity<Box>().HasOne(x => x.Shelf).WithMany(x => x.Boxes).HasForeignKey(x => x.ShelfId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Box>().HasOne(x => x.Room).WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Document>().HasIndex(x => x.BoxId);
        b.Entity<DocumentFavorite>().HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<DocumentShare>().HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        // FolderShare and UserFileShare need the same cascade, configured after the blanket
        // Restrict pass below — see the note there.

        b.Entity<ReportTemplate>().Property(x => x.LogoBase64).HasColumnType(LongText);
        b.Entity<ReportTemplate>().Property(x => x.LayoutJson).HasColumnType(LongText);
        // Slot payloads are small, but the preserved legacy design can be a whole HTML canvas.
        b.Entity<ReportTemplate>().Property(x => x.SlotsJson).HasColumnType(LongText);
        b.Entity<ReportTemplate>().Property(x => x.LegacyLayoutJson).HasColumnType(LongText);
        // The renderer only ever loads Active rows, and the admin review list only NeedsReview.
        b.Entity<ReportTemplate>().HasIndex(x => new { x.InstitutionId, x.Target, x.Status });
        b.Entity<ReportTemplate>().HasIndex(x => new { x.InstitutionId, x.Scope, x.Target });
        // Restrict, like every FK here. A design narrowed to a type is un-narrowed rather than
        // deleted when that type goes (DocumentService.DeleteTypeAsync) — the letterhead someone
        // drew is worth keeping even when the type it was tied to is gone.
        b.Entity<ReportTemplate>().HasOne<DocumentType>().WithMany()
            .HasForeignKey(x => x.DocumentTypeId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ReportTemplate>().HasOne<MailCategory>().WithMany()
            .HasForeignKey(x => x.MailCategoryId).OnDelete(DeleteBehavior.Restrict);

        // ── الديوان (external register) ──
        b.Entity<DiwanMail>().Property(x => x.Body).HasColumnType(LongText);
        b.Entity<DiwanMail>().Property(x => x.Description).HasColumnType(LongText);
        b.Entity<DiwanMail>().HasIndex(x => new { x.InstitutionId, x.RegisterNumber }).IsUnique();
        b.Entity<DiwanMail>().HasIndex(x => new { x.InstitutionId, x.Direction, x.Status });
        b.Entity<DiwanMail>().HasIndex(x => new { x.InstitutionId, x.MailDate });
        b.Entity<DiwanMail>().HasIndex(x => new { x.RemindOnNoReply, x.ResponseDeadline });
        b.Entity<DiwanMail>().HasIndex(x => x.CategoryId);
        b.Entity<DiwanMail>().HasOne(x => x.Category).WithMany()
            .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        // الجهات الخارجية ↔ الديوان: a register entry links to a directory organization.
        // Restrict so an organization in use can never be deleted out from under the register.
        b.Entity<DiwanMail>().HasIndex(x => x.ExternalOrgId);
        b.Entity<DiwanMail>().HasOne(x => x.ExternalOrg).WithMany()
            .HasForeignKey(x => x.ExternalOrgId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<DiwanMail>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<MailCategory>().HasIndex(x => new { x.InstitutionId, x.IsActive });
        // الى ديوان — many-to-many routing (delete behaviour follows the global Restrict convention below).
        b.Entity<DiwanBureau>().HasIndex(x => new { x.DiwanMailId, x.OrgUnitId }).IsUnique();
        b.Entity<DiwanBureau>().HasIndex(x => x.OrgUnitId);
        b.Entity<DiwanBureau>().HasOne(x => x.DiwanMail).WithMany(x => x.ToDiwanOrgUnits)
            .HasForeignKey(x => x.DiwanMailId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<DiwanBureau>().HasOne(x => x.OrgUnit).WithMany()
            .HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<DiwanAttachment>().Property(x => x.OcrText).HasColumnType(LongText);
        b.Entity<DiwanAttachment>().HasOne(x => x.DiwanMail).WithMany(x => x.Attachments)
            .HasForeignKey(x => x.DiwanMailId).OnDelete(DeleteBehavior.Cascade);

        // ── المراسلات (internal correspondence) ──
        b.Entity<InternalMail>().Property(x => x.Body).HasColumnType(LongText);
        b.Entity<InternalMail>().Property(x => x.Description).HasColumnType(LongText);
        b.Entity<InternalMail>().HasIndex(x => new { x.InstitutionId, x.MailNumber }).IsUnique();
        b.Entity<InternalMail>().HasIndex(x => new { x.InstitutionId, x.Status });
        b.Entity<InternalMail>().HasIndex(x => new { x.InstitutionId, x.MailDate });
        b.Entity<InternalMail>().HasIndex(x => new { x.RemindOnNoReply, x.ResponseDeadline });
        b.Entity<InternalMail>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<InternalMail>().HasOne(x => x.Category).WithMany()
            .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<InternalMail>().HasIndex(x => x.CategoryId);
        // Workflow linkage. Indexed on the instance because opening any message in a run looks up its
        // siblings, and on the task because the inbox asks "does this row need my action?" per row.
        b.Entity<InternalMail>().HasIndex(x => x.WorkflowInstanceId);
        b.Entity<InternalMail>().HasIndex(x => x.WorkflowTaskId);
        b.Entity<InternalRecipient>().HasOne(x => x.InternalMail).WithMany(x => x.Recipients)
            .HasForeignKey(x => x.InternalMailId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<InternalRecipient>().HasIndex(x => new { x.InternalMailId, x.Addressing });
        b.Entity<InternalRead>().HasOne(x => x.InternalMail).WithMany(x => x.Reads)
            .HasForeignKey(x => x.InternalMailId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<InternalRead>().HasIndex(x => new { x.InternalMailId, x.UserId }).IsUnique();
        b.Entity<InternalRead>().HasIndex(x => new { x.UserId, x.ReadStatus });
        b.Entity<InternalAttachment>().Property(x => x.OcrText).HasColumnType(LongText);
        b.Entity<InternalAttachment>().HasOne(x => x.InternalMail).WithMany(x => x.Attachments)
            .HasForeignKey(x => x.InternalMailId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<CorrespondenceGroupMember>().HasOne(m => m.Group).WithMany(g => g.Members)
            .HasForeignKey(m => m.GroupId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<CorrespondenceGroupMember>().HasIndex(m => m.GroupId);
        // A document type may be owned by one department (نوع المرفق) or shared (null = عام).
        b.Entity<DocumentType>().Property(x => x.Note).HasColumnType(LongText);
        b.Entity<DocumentType>().HasOne(x => x.OrgUnit).WithMany()
            .HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<DocumentType>().HasIndex(x => x.OrgUnitId);
        b.Entity<WorkflowStage>().Property(x => x.TransitionCondition).HasColumnType(LongText);
        b.Entity<WorkflowTask>().Property(x => x.Note).HasColumnType(LongText);
        b.Entity<WorkflowTask>().HasIndex(x => x.InternalMailId);
        // The run that a piece of correspondence started: unique, so a resubmission can never open a
        // second run against the same request.
        b.Entity<WorkflowInstance>().HasIndex(x => x.RootInternalMailId);
        // Trigger lookup on compose: active definition bound to this تصنيف البريد.
        b.Entity<WorkflowDefinition>().HasIndex(x => new { x.TriggerModule, x.MailCategoryId, x.IsActive });
        // A palette key, not free text — short enough to stay a plain indexable column.
        b.Entity<WorkflowStage>().Property(x => x.Color).HasMaxLength(16);
        b.Entity<DisposalRequest>().Property(x => x.Justification).HasColumnType(LongText);
        b.Entity<Notification>().Property(x => x.Body).HasColumnType(LongText);
        b.Entity<InformationPackage>().Property(x => x.Manifest).HasColumnType(LongText);
        b.Entity<RepresentationInfo>().Property(x => x.RenderingNote).HasColumnType(LongText);
        b.Entity<DesignatedCommunity>().Property(x => x.RenderingExpectations).HasColumnType(LongText);
        b.Entity<Institution>().Property(x => x.LogoBase64).HasColumnType(LongText);
        b.Entity<AutoBackupSettings>().Property(x => x.LastRunError).HasColumnType(LongText);
        b.Entity<AuditLog>().Property(x => x.OldValues).HasColumnType(LongText);
        b.Entity<AuditLog>().Property(x => x.NewValues).HasColumnType(LongText);
        b.Entity<AuditLog>().Property(x => x.UserAgent).HasMaxLength(512);
        b.Entity<AuditLog>().Property(x => x.Hash).HasMaxLength(128);
        b.Entity<AuditLog>().Property(x => x.PreviousHash).HasMaxLength(128);
        b.Entity<User>().Property(x => x.MfaSecret).HasMaxLength(512);
        // Base64 data URI — the default 256-char string convention would truncate it.
        b.Entity<User>().Property(x => x.AvatarUrl).HasColumnType(LongText);
        // The verification code is looked up by an anonymous endpoint on every scan, and two
        // documents must never answer to the same one.
        b.Entity<Document>().Property(x => x.VerificationCode).HasMaxLength(16);
        b.Entity<Document>().HasIndex(x => x.VerificationCode).IsUnique();
        b.Entity<ExternalOrganization>().Property(x => x.LogoUrl).HasColumnType(LongText);
        b.Entity<User>().Property(x => x.SignatureUrl).HasColumnType(LongText);
        b.Entity<User>().Property(x => x.StampUrl).HasColumnType(LongText);

        // ---- Full-text search (MySQL) ----
        // Added via raw SQL in a dedicated migration (CREATE FULLTEXT INDEX) — the Oracle
        // EF Core provider has no IsFullText() fluent helper, and longtext columns can't take a plain index.

        // ---- Soft-delete query filters ----
        b.Entity<User>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<Document>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<UserFile>().HasQueryFilter(x => !x.IsDeleted);
        b.Entity<UserFile>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(400).IsRequired();
            e.Property(x => x.FileExtension).HasMaxLength(32);
            e.Property(x => x.ContentType).HasMaxLength(200);
            e.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();
            e.Property(x => x.Checksum).HasMaxLength(128);
            // Listing a folder is the only read path that matters here.
            e.HasIndex(x => new { x.UserId, x.FolderId });
        });

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

        /* A department grant is meaningless once either end of it is gone, and it must not be what
           stops a user or a unit from being removed. Left behind under Restrict it would also be
           worse than useless: the row survives, and the next user to take that id inherits reach
           into a department nobody granted them. */
        b.Entity<UserOrgUnitAccess>().HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        // A personal permission grant rots identically: it means nothing without the person it was
        // given to, and a survivor would hand the next holder of that id a right nobody granted.
        b.Entity<UserPermissionGrant>().HasOne(x => x.User).WithMany(u => u.PermissionGrants)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<UserPermissionGrant>().HasOne(x => x.Permission).WithMany()
            .HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<UserOrgUnitAccess>().HasOne(x => x.OrgUnit).WithMany()
            .HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Cascade);

        // A letter grant is the same kind of row and rots the same way — a leftover pointing at a
        // reused id would quietly admit the wrong person to someone else's correspondence.
        b.Entity<DiwanAccess>().HasOne(x => x.DiwanMail).WithMany()
            .HasForeignKey(x => x.DiwanMailId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<DiwanAccess>().HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        // A grant on a folder or a file rots the same way once the thing itself is gone, and under
        // Restrict it would also be what stops the owner from deleting their own folder. It follows
        // the item out.
        b.Entity<FolderShare>().HasOne(x => x.Folder).WithMany()
            .HasForeignKey(x => x.FolderId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<UserFileShare>().HasOne(x => x.File).WithMany()
            .HasForeignKey(x => x.UserFileId).OnDelete(DeleteBehavior.Cascade);
    }
}
