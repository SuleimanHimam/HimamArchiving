using System.Reflection;
using System.Security.Cryptography;
using Archiving.Api.Authorization;
using Archiving.Api.Controllers.Core;
using Archiving.Api.Controllers.Hr;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Core;
using Archiving.Application.Features.Hr;
using Archiving.Application.Features.Workflow;
using Archiving.Domain.Common;
using Archiving.Domain.Core;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Domain.Hr;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Persistence.Interceptors;
using Archiving.Infrastructure.Security.Crypto;
using Archiving.Infrastructure.Services;
using Archiving.Infrastructure.Services.Core;
using Archiving.Infrastructure.Services.Hr;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Archiving.Tests;

/// <summary>HR phase 1: personnel file, personnel actions and their effects, clearance. All data is generated.</summary>
public class HrPhase1Tests
{
    private const long UnitA = 10, UnitB = 20, UserOne = 100, Workflow = 7;

    // ---- fakes ----

    private sealed class FakeScope(bool admin, params long[] units) : IAccessScope
    {
        public Task<bool> IsSystemAdminAsync(CancellationToken ct = default) => Task.FromResult(admin);
        public Task<bool> HasPermissionAsync(string code, CancellationToken ct = default) => Task.FromResult(admin);
        public Task<long?> OrgUnitIdAsync(CancellationToken ct = default) => Task.FromResult(units.Length > 0 ? units[0] : (long?)null);
        public Task<IReadOnlyList<long>> OrgUnitIdsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<long>>(units);
        public Task<IReadOnlyList<long>> ManagedOrgUnitIdsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<long>>([]);
        public Task<bool> SeesAllDocumentsAsync(CancellationToken ct = default) => Task.FromResult(admin);
    }

    /// <summary>Wraps data keys with a fixed test KEK (XOR), enough to exercise the envelope format.</summary>
    private sealed class FakeKms : IKeyManagementService
    {
        private static readonly byte[] Kek = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
        public string ActiveKekId => "test-kek-1";

        public Task<DataKey> GenerateDataKeyAsync(CancellationToken ct = default)
        {
            var dek = RandomNumberGenerator.GetBytes(32);
            return Task.FromResult(new DataKey(dek, Xor(dek), ActiveKekId));
        }

        public Task<byte[]> UnwrapAsync(string kekId, byte[] wrapped, CancellationToken ct = default) =>
            kekId == ActiveKekId ? Task.FromResult(Xor(wrapped)) : throw new CryptographicException("unknown kek");

        public Task<(byte[] Wrapped, string KekId)> RewrapAsync(string kekId, byte[] wrapped, CancellationToken ct = default) =>
            Task.FromResult((wrapped, kekId));
        public Task<string> CreateKekVersionAsync(CancellationToken ct = default) => Task.FromResult(ActiveKekId);
        public Task<KmsHealth> CheckHealthAsync(CancellationToken ct = default) =>
            Task.FromResult(new KmsHealth(true, "fake", ActiveKekId, 1, null));

        private static byte[] Xor(byte[] b) => b.Select((x, i) => (byte)(x ^ Kek[i % Kek.Length])).ToArray();
    }

    private sealed class RecordingAudit : IAuditWriter
    {
        public List<(string Action, string Entity, long Id, string? New)> Entries { get; } = [];
        public Task WriteAsync(string action, string entityType, long entityId, string? entityTitle = null,
            string? oldValues = null, string? newValues = null, CancellationToken ct = default)
        {
            Entries.Add((action, entityType, entityId, newValues));
            return Task.CompletedTask;
        }
    }

    private sealed class StubApprovals : IApprovalService
    {
        public List<SubmitApprovalRequest> Submitted { get; } = [];
        public Task<Result<ApprovalRequestDto>> SubmitAsync(SubmitApprovalRequest r, CancellationToken ct = default)
        {
            Submitted.Add(r);
            return Task.FromResult(Result<ApprovalRequestDto>.Ok(new ApprovalRequestDto(
                900, "REQ-TEST", r.RequestType, r.SourceModule, r.SourceId, r.Title, "Pending", 1, DateTime.UtcNow, null, null)));
        }
        public Task<IReadOnlyList<ApprovalRequestDto>> ListMineAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ApprovalRequestDto>>([]);
        public Task<IReadOnlyList<ApprovalInboxItem>> InboxAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ApprovalInboxItem>>([]);
    }

    /// <summary>Workflow engine stand-in that answers only the worklist; any other call is a test bug.</summary>
    public class WorklistOnlyWorkflow : DispatchProxy
    {
        public IReadOnlyList<WorklistItem> Items { get; set; } = [];
        public static IWorkflowService Create(params WorklistItem[] items)
        {
            var proxy = Create<IWorkflowService, WorklistOnlyWorkflow>();
            ((WorklistOnlyWorkflow)(object)proxy).Items = items;
            return proxy;
        }
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            method?.Name == nameof(IWorkflowService.MyWorklistAsync)
                ? Task.FromResult(Items)
                : throw new NotSupportedException(method?.Name);
    }

    // ---- fixture ----

    private sealed record Ctx(AppDbContext Db, Dictionary<PersonnelActionEffect, long> Types, long Grade1, long Grade2,
        long Contract, long Married, long Child, long Degree);

    private static Ctx NewCtx()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new AuditableEntityInterceptor(new TestCurrentUser()))
            .Options);

        db.Institutions.Add(new Institution { Id = 1, Name = "مؤسسة الاختبار" });
        db.OrgUnits.Add(new OrgUnit { Id = UnitA, InstitutionId = 1, Name = "دائرة أ" });
        db.OrgUnits.Add(new OrgUnit { Id = UnitB, InstitutionId = 1, Name = "دائرة ب" });
        db.Users.Add(new User { Id = UserOne, FullName = "مستخدم تجريبي", Email = "one@test.invalid", OrgUnitId = UnitA });
        db.WorkflowDefinitions.Add(new WorkflowDefinition { Id = Workflow, Name = "مسار تجريبي", IsActive = true });
        foreach (var (entity, pattern) in new[]
                 {
                     (PersonnelActionEffectHandler.ActionNumbering, "HR-MV-{YYYY}-{SEQ:5}"),
                     (PersonnelActionEffectHandler.ClearanceNumbering, "CLR-{YYYY}-{SEQ:3}"),
                 })
            db.NumberingTemplates.Add(new NumberingTemplate { EntityType = entity, Pattern = pattern });

        var types = new Dictionary<PersonnelActionEffect, long>();
        foreach (var effect in Enum.GetValues<PersonnelActionEffect>())
        {
            var t = new PersonnelActionType { Code = effect.ToString().ToUpperInvariant(), Name = $"نوع {effect}", Effect = effect, WorkflowDefinitionId = Workflow };
            db.PersonnelActionTypes.Add(t);
            db.SaveChanges();
            types[effect] = t.Id;
        }

        long Add(string category, string code)
        {
            var l = new Lookup { Category = category, Code = code, Name = $"قيمة {code}" };
            db.Lookups.Add(l);
            db.SaveChanges();
            return l.Id;
        }

        var ctx = new Ctx(db, types,
            Add(LookupCategories.Grade, "G1"), Add(LookupCategories.Grade, "G2"),
            Add(LookupCategories.AppointmentType, "CONTRACT"), Add(LookupCategories.MaritalStatus, "M"),
            Add(LookupCategories.Relationship, "CHILD"), Add(LookupCategories.QualificationType, "BSC"));
        foreach (var party in new[] { "P1", "P2", "P3", "P4" }) Add(LookupCategories.ClearanceParty, party);
        return ctx;
    }

    private static Employee AddEmployee(AppDbContext db, string number = "T-0001", long unit = UnitA, long? user = UserOne)
    {
        var e = new Employee
        {
            EmployeeNumber = number, FullName = "موظف تجريبي", Category = EmployeeCategory.Civil,
            OrgUnitId = unit, JobTitle = "محاسب", UserId = user,
        };
        db.Employees.Add(e);
        db.SaveChanges();
        return e;
    }

    private static PersonnelActionService Actions(Ctx c, IApprovalService? approvals = null, bool admin = true, params long[] units) =>
        new(c.Db, new FakeScope(admin, units), new NoopAuditWriter(), approvals ?? new StubApprovals());

    private static EmployeeFileService Files(Ctx c, IAuditWriter? audit = null, bool admin = true, params long[] units) =>
        new(c.Db, new FakeScope(admin, units), audit ?? new NoopAuditWriter(), new SensitiveDataProtector(new FakeKms()));

    /// <summary>Simulates the workflow engine finishing the request's path, then saving (as ActOnTaskAsync does).</summary>
    private static async Task DecideAsync(Ctx c, long actionId, WorkflowStatus status, string? note = null)
    {
        var action = await c.Db.PersonnelActions.FirstAsync(a => a.Id == actionId);
        var request = new ApprovalRequest
        {
            Number = $"REQ-{actionId}", RequestType = PersonnelActionService.RequestType, SourceModule = "HR",
            SourceId = actionId, Title = "t",
        };
        c.Db.ApprovalRequests.Add(request);
        action.Status = PersonnelActionStatus.Submitted;
        action.ApprovalRequestId = null;
        await c.Db.SaveChangesAsync();

        var effect = new PersonnelActionEffectHandler(c.Db, new NumberingService(c.Db, new NoopAuditWriter()), new NoopAuditWriter());
        await new ApprovalOutcomeHandler(c.Db, [effect]).HandleAsync(request.Id, status, note, CancellationToken.None);
        await c.Db.SaveChangesAsync();
    }

    private static async Task<long> DraftAsync(Ctx c, PersonnelActionEffect effect, long? employeeId, PersonnelActionPayload payload)
    {
        var r = await Actions(c).CreateAsync(new SavePersonnelActionRequest(c.Types[effect], employeeId, new DateOnly(2026, 11, 1), "سبب تجريبي", payload));
        Assert.True(r.Succeeded, r.Error);
        return r.Value!.Id;
    }

    // ---- sensitive data ----

    [Fact]
    public async Task Protector_round_trips_and_never_stores_clear_text()
    {
        var p = new SensitiveDataProtector(new FakeKms());
        var blob = await p.ProtectAsync("401234567");

        Assert.Equal("401234567", await p.UnprotectAsync(blob));
        Assert.DoesNotContain("401234567", System.Text.Encoding.UTF8.GetString(blob));
        Assert.NotEqual(blob, await p.ProtectAsync("401234567"));   // fresh key and nonce every time
    }

    [Fact]
    public async Task Protector_rejects_a_tampered_value()
    {
        var p = new SensitiveDataProtector(new FakeKms());
        var blob = await p.ProtectAsync("401234567");
        blob[^1] ^= 0xFF;

        await Assert.ThrowsAnyAsync<CryptographicException>(() => p.UnprotectAsync(blob));
    }

    [Theory]
    [InlineData("401235431", "••••5431")]
    [InlineData("12", "••")]
    public void Mask_shows_only_the_last_four(string plain, string expected) =>
        Assert.Equal(expected, ISensitiveDataProtector.Mask(plain));

    // ---- employee file ----

    [Fact]
    public async Task Profile_returns_masked_values_and_reveal_is_audited_without_the_value()
    {
        var c = NewCtx();
        var e = AddEmployee(c.Db);
        var audit = new RecordingAudit();
        var files = Files(c, audit);

        var saved = await files.SaveProfileAsync(e.Id, new SaveProfileRequest(null, c.Married, "عنوان", null, null, "401235431", "0599000111"));
        Assert.True(saved.Succeeded, saved.Error);
        Assert.Equal("••••5431", saved.Value!.NationalIdMasked);

        var revealed = await files.RevealAsync(e.Id, new RevealRequest(SensitiveFields.NationalId));
        Assert.Equal("401235431", revealed.Value!.Value);

        var entry = Assert.Single(audit.Entries, x => x.Action == "SensitiveRevealed");
        Assert.Equal(e.Id, entry.Id);
        Assert.DoesNotContain(audit.Entries, x => x.New?.Contains("401235431") == true);
    }

    [Fact]
    public async Task Out_of_scope_file_is_indistinguishable_from_missing()
    {
        var c = NewCtx();
        var e = AddEmployee(c.Db, unit: UnitB);

        var r = await Files(c, admin: false, units: UnitA).GetAsync(e.Id);
        var reveal = await Files(c, admin: false, units: UnitA).RevealAsync(e.Id, new RevealRequest(SensitiveFields.NationalId));

        Assert.False(r.Succeeded);
        Assert.Equal("الموظف غير موجود", r.Error);
        Assert.Equal("الموظف غير موجود", reveal.Error);
    }

    [Fact]
    public async Task Grade_is_set_once_from_launch_data_then_only_by_a_promotion()
    {
        var c = NewCtx();
        var e = AddEmployee(c.Db);
        var files = Files(c);

        var first = await files.SaveProfileAsync(e.Id, new SaveProfileRequest(null, null, null, null, c.Grade1, null, null));
        Assert.True(first.Succeeded, first.Error);

        var second = await files.SaveProfileAsync(e.Id,
            new SaveProfileRequest(null, null, null, null, c.Grade2, null, null, first.Value!.RowVersion));
        Assert.False(second.Succeeded);
    }

    [Fact]
    public async Task Profile_rejects_a_value_from_the_wrong_list()
    {
        var c = NewCtx();
        var e = AddEmployee(c.Db);

        var r = await Files(c).SaveProfileAsync(e.Id, new SaveProfileRequest(null, c.Grade1, null, null, null, null, null));

        Assert.False(r.Succeeded);   // a grade id passed as marital status
    }

    [Fact]
    public async Task Bank_account_is_stored_encrypted_and_returned_masked()
    {
        var c = NewCtx();
        var e = AddEmployee(c.Db);

        var r = await Files(c).AddBankAccountAsync(e.Id, new SaveBankAccountRequest("بنك تجريبي", "PS00TEST000000000000012345431"));

        Assert.True(r.Succeeded, r.Error);
        Assert.Equal("••••5431", r.Value!.AccountMasked);
        var stored = await c.Db.EmployeeBankAccounts.SingleAsync();
        Assert.DoesNotContain("5431", System.Text.Encoding.UTF8.GetString(stored.AccountEnc));
    }

    [Fact]
    public async Task Family_member_needs_a_valid_relationship()
    {
        var c = NewCtx();
        var e = AddEmployee(c.Db);
        var files = Files(c);

        Assert.True((await files.AddFamilyMemberAsync(e.Id, new SaveFamilyMemberRequest("فرد تجريبي", c.Child, null, null))).Succeeded);
        Assert.False((await files.AddFamilyMemberAsync(e.Id, new SaveFamilyMemberRequest("فرد تجريبي", c.Degree, null, null))).Succeeded);
    }

    // ---- personnel actions: drafting and submitting ----

    [Fact]
    public async Task Transfer_needs_a_target_unit()
    {
        var c = NewCtx();
        var e = AddEmployee(c.Db);

        var r = await Actions(c).CreateAsync(new SavePersonnelActionRequest(c.Types[PersonnelActionEffect.Transfer], e.Id,
            new DateOnly(2026, 11, 1), "سبب", new PersonnelActionPayload()));

        Assert.False(r.Succeeded);
    }

    [Fact]
    public async Task Hire_cannot_reuse_an_employee_number()
    {
        var c = NewCtx();
        AddEmployee(c.Db, "T-0001");

        var r = await Actions(c).CreateAsync(new SavePersonnelActionRequest(c.Types[PersonnelActionEffect.Hire], null,
            new DateOnly(2026, 11, 1), "سبب", new PersonnelActionPayload("T-0001", "موظف جديد", null, "Civil", UnitA, JobTitle: "مدقق")));

        Assert.False(r.Succeeded);
    }

    [Fact]
    public async Task Submit_is_refused_until_an_approval_path_is_configured()
    {
        var c = NewCtx();
        var e = AddEmployee(c.Db);
        var id = await DraftAsync(c, PersonnelActionEffect.Penalty, e.Id, new PersonnelActionPayload(Notes: "ملاحظة"));
        var type = await c.Db.PersonnelActionTypes.FirstAsync(t => t.Id == c.Types[PersonnelActionEffect.Penalty]);
        type.WorkflowDefinitionId = null;
        await c.Db.SaveChangesAsync();
        var approvals = new StubApprovals();

        var r = await Actions(c, approvals).SubmitAsync(id);

        Assert.False(r.Succeeded);
        Assert.Empty(approvals.Submitted);
    }

    [Fact]
    public async Task Submit_sends_the_action_into_its_types_path_and_locks_it()
    {
        var c = NewCtx();
        var e = AddEmployee(c.Db);
        var id = await DraftAsync(c, PersonnelActionEffect.Penalty, e.Id, new PersonnelActionPayload(Notes: "ملاحظة"));
        var approvals = new StubApprovals();
        var svc = Actions(c, approvals);

        var r = await svc.SubmitAsync(id);

        Assert.True(r.Succeeded, r.Error);
        Assert.Equal("Submitted", r.Value!.Status);
        var sent = Assert.Single(approvals.Submitted);
        Assert.Equal(("hr.action", Workflow, id), (sent.RequestType, sent.WorkflowDefinitionId, sent.SourceId));
        var edit = await svc.UpdateAsync(id, new SavePersonnelActionRequest(c.Types[PersonnelActionEffect.Penalty], e.Id,
            new DateOnly(2026, 11, 1), "سبب آخر", new PersonnelActionPayload(), r.Value.RowVersion));
        Assert.False(edit.Succeeded);
    }

    // ---- effects on approval ----

    [Fact]
    public async Task Approved_hire_creates_the_employee_and_numbers_the_decision()
    {
        var c = NewCtx();
        var id = await DraftAsync(c, PersonnelActionEffect.Hire, null,
            new PersonnelActionPayload("T-0100", "موظف جديد", null, "Contract", UnitB, JobTitle: "مدقق", GradeId: c.Grade1));

        await DecideAsync(c, id, WorkflowStatus.Completed);

        var action = await c.Db.PersonnelActions.FirstAsync(a => a.Id == id);
        var employee = await c.Db.Employees.SingleAsync(x => x.EmployeeNumber == "T-0100");
        Assert.Equal(PersonnelActionStatus.Executed, action.Status);
        Assert.Equal(employee.Id, action.EmployeeId);
        Assert.Matches(@"^HR-MV-\d{4}-00001$", action.Number);
        Assert.Equal(new DateOnly(2026, 11, 1), employee.HireDate);
        Assert.Equal(c.Grade1, (await c.Db.EmployeeProfiles.SingleAsync(p => p.EmployeeId == employee.Id)).GradeId);
    }

    [Fact]
    public async Task Hire_whose_number_was_taken_while_pending_is_refused_at_execution()
    {
        var c = NewCtx();
        var id = await DraftAsync(c, PersonnelActionEffect.Hire, null,
            new PersonnelActionPayload("T-0200", "موظف جديد", null, "Civil", UnitA, JobTitle: "مدقق"));
        AddEmployee(c.Db, "T-0200", user: null);

        await Assert.ThrowsAsync<DomainException>(() => DecideAsync(c, id, WorkflowStatus.Completed));
    }

    [Fact]
    public async Task Approved_transfer_moves_the_employee_and_their_login()
    {
        var c = NewCtx();
        var e = AddEmployee(c.Db);
        var id = await DraftAsync(c, PersonnelActionEffect.Transfer, e.Id, new PersonnelActionPayload(OrgUnitId: UnitB));

        await DecideAsync(c, id, WorkflowStatus.Completed);

        Assert.Equal(UnitB, (await c.Db.Employees.FirstAsync(x => x.Id == e.Id)).OrgUnitId);
        Assert.Equal(UnitB, (await c.Db.Users.FirstAsync(u => u.Id == UserOne)).OrgUnitId);
        var action = await c.Db.PersonnelActions.FirstAsync(a => a.Id == id);
        Assert.Contains($"\"orgUnitId\":{UnitA}", action.BeforeJson);
        Assert.Contains($"\"orgUnitId\":{UnitB}", action.AfterJson);
    }

    [Fact]
    public async Task Approved_promotion_changes_the_grade()
    {
        var c = NewCtx();
        var e = AddEmployee(c.Db);
        var id = await DraftAsync(c, PersonnelActionEffect.Promote, e.Id, new PersonnelActionPayload(GradeId: c.Grade2));

        await DecideAsync(c, id, WorkflowStatus.Completed);

        Assert.Equal(c.Grade2, (await c.Db.EmployeeProfiles.SingleAsync(p => p.EmployeeId == e.Id)).GradeId);
    }

    [Fact]
    public async Task Approved_suspension_disables_the_login()
    {
        var c = NewCtx();
        var e = AddEmployee(c.Db);
        var id = await DraftAsync(c, PersonnelActionEffect.Suspend, e.Id, new PersonnelActionPayload());

        await DecideAsync(c, id, WorkflowStatus.Completed);

        Assert.Equal(EmployeeStatus.Suspended, (await c.Db.Employees.FirstAsync(x => x.Id == e.Id)).Status);
        Assert.False((await c.Db.Users.FirstAsync(u => u.Id == UserOne)).IsActive);
    }

    [Fact]
    public async Task Rejection_needs_a_reason_and_changes_nothing_on_the_employee()
    {
        var c = NewCtx();
        var e = AddEmployee(c.Db);
        var id = await DraftAsync(c, PersonnelActionEffect.Transfer, e.Id, new PersonnelActionPayload(OrgUnitId: UnitB));

        await Assert.ThrowsAsync<DomainException>(() => DecideAsync(c, id, WorkflowStatus.Rejected, "  "));
        var c2 = NewCtx();
        var e2 = AddEmployee(c2.Db);
        var id2 = await DraftAsync(c2, PersonnelActionEffect.Transfer, e2.Id, new PersonnelActionPayload(OrgUnitId: UnitB));
        await DecideAsync(c2, id2, WorkflowStatus.Rejected, "غير مبرر");

        var action = await c2.Db.PersonnelActions.FirstAsync(a => a.Id == id2);
        Assert.Equal(PersonnelActionStatus.Rejected, action.Status);
        Assert.Null(action.Number);
        Assert.Equal(UnitA, (await c2.Db.Employees.FirstAsync(x => x.Id == e2.Id)).OrgUnitId);
    }

    [Fact]
    public async Task Returned_action_can_be_edited_again()
    {
        var c = NewCtx();
        var e = AddEmployee(c.Db);
        var id = await DraftAsync(c, PersonnelActionEffect.Transfer, e.Id, new PersonnelActionPayload(OrgUnitId: UnitB));
        await DecideAsync(c, id, WorkflowStatus.ChangesRequested, "أضف المقعد");

        var action = await c.Db.PersonnelActions.AsNoTracking().FirstAsync(a => a.Id == id);
        Assert.Equal(PersonnelActionStatus.Returned, action.Status);
        var edit = await Actions(c).UpdateAsync(id, new SavePersonnelActionRequest(c.Types[PersonnelActionEffect.Transfer], e.Id,
            new DateOnly(2026, 11, 1), "سبب معدل", new PersonnelActionPayload(OrgUnitId: UnitB), Convert.ToBase64String(action.RowVersion)));
        Assert.True(edit.Succeeded, edit.Error);
    }

    [Fact]
    public async Task Intermediate_level_changes_nothing()
    {
        var c = NewCtx();
        var e = AddEmployee(c.Db);
        var id = await DraftAsync(c, PersonnelActionEffect.Transfer, e.Id, new PersonnelActionPayload(OrgUnitId: UnitB));

        await DecideAsync(c, id, WorkflowStatus.Running);

        Assert.Equal(PersonnelActionStatus.Submitted, (await c.Db.PersonnelActions.FirstAsync(a => a.Id == id)).Status);
        Assert.Equal(UnitA, (await c.Db.Employees.FirstAsync(x => x.Id == e.Id)).OrgUnitId);
    }

    // ---- end of service and clearance ----

    [Fact]
    public async Task End_of_service_opens_a_clearance_and_the_login_is_disabled_only_when_it_is_issued()
    {
        var c = NewCtx();
        var e = AddEmployee(c.Db);
        var id = await DraftAsync(c, PersonnelActionEffect.EndService, e.Id, new PersonnelActionPayload());

        await DecideAsync(c, id, WorkflowStatus.Completed);

        Assert.Equal(EmployeeStatus.Terminated, (await c.Db.Employees.FirstAsync(x => x.Id == e.Id)).Status);
        Assert.True((await c.Db.Users.FirstAsync(u => u.Id == UserOne)).IsActive);

        var svc = new ClearanceService(c.Db, new FakeScope(true), new TestCurrentUser(), new NoopAuditWriter());
        var clearance = Assert.Single(await svc.ListAsync(null));
        Assert.Matches(@"^CLR-\d{4}-001$", clearance.Number);
        Assert.Equal(4, clearance.Items.Count);

        ClearanceDto? last = null;
        foreach (var item in clearance.Items)
        {
            Assert.Equal("Open", (await svc.GetAsync(clearance.Id)).Value!.Status);
            var r = await svc.ClearItemAsync(clearance.Id, item.Id, new ClearItemRequest("تمت التسوية"));
            Assert.True(r.Succeeded, r.Error);
            last = r.Value;
        }

        Assert.Equal("Issued", last!.Status);
        Assert.False((await c.Db.Users.FirstAsync(u => u.Id == UserOne)).IsActive);
    }

    [Fact]
    public async Task An_issued_clearance_cannot_be_cleared_again()
    {
        var c = NewCtx();
        var e = AddEmployee(c.Db);
        var id = await DraftAsync(c, PersonnelActionEffect.EndService, e.Id, new PersonnelActionPayload());
        await DecideAsync(c, id, WorkflowStatus.Completed);
        var svc = new ClearanceService(c.Db, new FakeScope(true), new TestCurrentUser(), new NoopAuditWriter());
        var clearance = (await svc.ListAsync(null)).Single();
        foreach (var item in clearance.Items) await svc.ClearItemAsync(clearance.Id, item.Id, new ClearItemRequest(null));

        var again = await svc.ClearItemAsync(clearance.Id, clearance.Items[0].Id, new ClearItemRequest(null));

        Assert.False(again.Succeeded);
    }

    [Fact]
    public async Task Terminated_employee_takes_no_further_actions()
    {
        var c = NewCtx();
        var e = AddEmployee(c.Db);
        var id = await DraftAsync(c, PersonnelActionEffect.EndService, e.Id, new PersonnelActionPayload());
        await DecideAsync(c, id, WorkflowStatus.Completed);

        var r = await Actions(c).CreateAsync(new SavePersonnelActionRequest(c.Types[PersonnelActionEffect.Penalty], e.Id,
            new DateOnly(2026, 11, 2), "سبب", new PersonnelActionPayload()));

        Assert.False(r.Succeeded);
    }

    // ---- settings ----

    [Fact]
    public async Task Lookups_accept_only_known_lists_and_unique_codes()
    {
        var c = NewCtx();
        var svc = new LookupService(c.Db, new NoopAuditWriter());

        Assert.False((await svc.CreateAsync(new SaveLookupRequest("salary_rule", "X", "س", null))).Succeeded);
        Assert.False((await svc.CreateAsync(new SaveLookupRequest(LookupCategories.Grade, "G1", "مكرر", null))).Succeeded);
        Assert.True((await svc.CreateAsync(new SaveLookupRequest(LookupCategories.Grade, "G3", "الثالثة", null))).Succeeded);
    }

    [Fact]
    public async Task Action_type_effect_is_frozen_once_used()
    {
        var c = NewCtx();
        var e = AddEmployee(c.Db);
        await DraftAsync(c, PersonnelActionEffect.Penalty, e.Id, new PersonnelActionPayload());
        var type = await c.Db.PersonnelActionTypes.AsNoTracking().FirstAsync(t => t.Id == c.Types[PersonnelActionEffect.Penalty]);

        var r = await new ActionTypeService(c.Db, new NoopAuditWriter()).UpdateAsync(type.Id,
            new SaveActionTypeRequest(type.Code, type.Name, "Transfer", type.WorkflowDefinitionId, true, Convert.ToBase64String(type.RowVersion)));

        Assert.False(r.Succeeded);
    }

    // ---- endpoints ----

    private static readonly Type[] Controllers =
    [
        typeof(LookupsController), typeof(ApprovalsController), typeof(HrActionTypesController),
        typeof(HrEmployeeFileController), typeof(HrPersonnelActionsController), typeof(HrClearancesController),
    ];

    [Fact]
    public void Every_new_endpoint_declares_a_known_permission()
    {
        foreach (var controller in Controllers)
        foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var attr = action.GetCustomAttribute<HasPermissionAttribute>();
            Assert.True(attr is not null, $"{controller.Name}.{action.Name} has no permission");
            var code = attr!.Policy![HasPermissionAttribute.PolicyPrefix.Length..];
            Assert.Contains(code, PermissionCatalog.AllCodes);
        }
    }

    [Fact]
    public void Write_endpoints_are_not_guarded_by_a_view_permission_except_reveal()
    {
        foreach (var controller in Controllers)
        foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var verb = action.GetCustomAttributes<HttpMethodAttribute>().FirstOrDefault()?.HttpMethods.Single();
            if (verb is null or "GET" || action.Name == "Reveal") continue;
            Assert.False(action.GetCustomAttribute<HasPermissionAttribute>()!.Policy!.EndsWith(".View"),
                $"{controller.Name}.{action.Name} ({verb}) is guarded only by a View permission");
        }
    }

    [Fact]
    public void Managers_do_not_get_the_reveal_permission_by_default()
    {
        var manager = PermissionCatalog.RoleDefaults.Single(r => r.Name == "Manager");
        Assert.False(manager.Pick(new Permission { Resource = "HrSensitive", Action = PermissionAction.View }));
    }

    [Fact]
    public void Action_json_columns_are_unbounded_on_sql_server()
    {
        // Regression: the 256-char string convention truncated payload_json on the first hire.
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=.;Database=model-only;Trusted_Connection=True").Options);
        var entity = db.Model.FindEntityType(typeof(PersonnelAction))!;
        foreach (var name in new[] { nameof(PersonnelAction.PayloadJson), nameof(PersonnelAction.BeforeJson), nameof(PersonnelAction.AfterJson) })
            Assert.Equal("nvarchar(max)", entity.FindProperty(name)!.GetColumnType());
    }

    [Fact]
    public async Task Inbox_lists_only_pending_approval_tasks_with_request_details()
    {
        var c = NewCtx();
        var pending = new ApprovalRequest { Number = "REQ-T-1", RequestType = "hr.action", SourceModule = "HR", SourceId = 7, Title = "تعيين: موظف", RequesterUserId = UserOne };
        var decided = new ApprovalRequest { Number = "REQ-T-2", RequestType = "hr.action", SourceModule = "HR", SourceId = 8, Title = "نقل", Status = ApprovalRequestStatus.Approved };
        c.Db.ApprovalRequests.AddRange(pending, decided);
        await c.Db.SaveChangesAsync();

        var due = DateTime.UtcNow.AddHours(4);
        var workflow = WorklistOnlyWorkflow.Create(
            new WorklistItem(11, 1, "ApprovalRequest", pending.Id, "مسار", "اعتماد", "Pending", due, false, "131"),
            new WorklistItem(12, 2, "ApprovalRequest", decided.Id, "مسار", "اعتماد", "Pending", due, false, "131"),
            new WorklistItem(13, 3, "InternalMail", pending.Id, "بريد", "مراجعة", "Pending", due, false, "1"));
        var service = new ApprovalService(c.Db, workflow, new NumberingService(c.Db, new NoopAuditWriter()),
            new TestCurrentUser(), new NoopAuditWriter(), []);

        var inbox = await service.InboxAsync();

        var item = Assert.Single(inbox);
        Assert.Equal(11, item.TaskId);
        Assert.Equal("REQ-T-1", item.Number);
        Assert.Equal("مستخدم تجريبي", item.RequesterName);
        Assert.Equal("131", item.AllowedActions);
    }
}
