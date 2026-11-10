using System.Globalization;
using System.Text.Json;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Security;
using Archiving.Infrastructure.Services;
using Bogus;
using Microsoft.EntityFrameworkCore;

// ═══════════════════════════════════════════════════════════════════════════════════════════
//  Demo/test data seeder — الديوان الإلكتروني
//
//  Re-runnable: it wipes every table (except the EF migrations history), replays the app's own
//  DbSeeder for the baseline (permissions, roles, admin, org chart, workflows), then layers
//  realistic demo data on top. Running it twice produces the same database — the randomiser is
//  seeded with a fixed value, so names, numbers and dates are identical every run.
//
//    dotnet run --project backend/tools/Archiving.Seeder
//    dotnet run --project backend/tools/Archiving.Seeder -- --connection "server=…"
//
//  It talks to the database directly rather than through the API, so the API may stay running.
// ═══════════════════════════════════════════════════════════════════════════════════════════

// Staff are dealt out per department rather than scattered across the institution: every
// visibility rule in the system is written in terms of a department and the person who heads it,
// so a roster that ignores departments demonstrates none of it.
const int StaffPerDepartment = 8;
const int DocumentCount = 400;
const int DiwanCount = 260;
const int InternalCount = 240;

var cs = ArgValue(args, "--connection") ?? ReadConnectionString();
Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine($"→ {Mask(cs)}");

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseMySQL(cs)          // same provider the API registers (MySql.EntityFrameworkCore)
    .Options;

// The design-time constructor: no MediatR publisher, so bulk inserts do not fire domain events
// (which would try to raise notifications for 400 documents).
await using var db = new AppDbContext(options);

// Deterministic: same seed ⇒ same data, so a failing test can be reproduced exactly.
Randomizer.Seed = new Random(20260802);
var f = new Faker("ar");
var rnd = f.Random;
var counts = new Dictionary<string, int>();

// ── Arabic word banks. Bogus's Latin lorem would make every screen read as placeholder text. ──
// The departments themselves are not listed here: they come from DbSeeder.DefaultDepartments,
// which is what a real installation gets, and inventing a second set here would produce demo
// data arranged around an org chart no deployment has.
string[] givenM = ["محمد", "أحمد", "عبدالله", "خالد", "سليمان", "فيصل", "عمر", "يوسف", "إبراهيم", "طارق", "زياد", "ماجد", "بشار", "نايف", "رامي"];
string[] givenF = ["فاطمة", "عائشة", "مريم", "نورة", "هدى", "سارة", "لينا", "رنا", "دعاء", "أسماء", "سلمى", "ريم", "جواهر", "منال", "لمى"];
string[] family = ["النبالي", "الفارس", "العتيبي", "الحربي", "القحطاني", "الزهراني", "الدوسري", "الشمري", "المطيري", "الغامدي", "العنزي", "السبيعي", "الرشيد", "الخالدي", "البلوي"];
string[] jobTitles = ["مدير إدارة", "رئيس قسم", "أخصائي أول", "أخصائي", "محلل نظم", "محاسب", "مدقق داخلي", "سكرتير تنفيذي", "أمين أرشيف", "مستشار قانوني"];
string[] subjects = ["طلب اعتماد الميزانية التشغيلية", "تعميم بشأن دوام شهر رمضان", "دعوة لحضور اجتماع اللجنة الفنية", "الرد على استفسار ديوان المحاسبة", "طلب صرف مستحقات مالية", "تقرير الأداء الربع سنوي", "طلب توفير أجهزة حاسوب", "إحالة معاملة للبت فيها", "تجديد عقد الصيانة السنوي", "مذكرة داخلية — متابعة اجتماع", "إشعار بانتهاء مدة الحفظ", "طلب إجازة اضطرارية", "محضر استلام عهدة", "توصية بترقية موظف", "طلب تصحيح بيانات"];
string[] orgNames = ["وزارة المالية", "وزارة التربية والتعليم", "ديوان المحاسبة", "هيئة الاتصالات", "شركة النبالي والفارس", "مؤسسة البناء الحديث", "بنك الاستثمار الوطني", "الجمعية الخيرية للتنمية", "شركة الحلول الرقمية", "الغرفة التجارية"];
string[] docTitles = ["عقد توريد أجهزة", "فاتورة مشتريات", "تقرير مالي سنوي", "قرار إداري", "محضر اجتماع", "كشف رواتب", "شهادة خبرة", "طلب توظيف", "مذكرة تفاهم", "دراسة جدوى"];

string ArabicName() => rnd.Bool() ? $"{f.PickRandom(givenM)} {f.PickRandom(family)}" : $"{f.PickRandom(givenF)} {f.PickRandom(family)}";
string Body(int paras = 2) => string.Join("\n\n", Enumerable.Range(0, paras)
    .Select(_ => string.Join("، ", Enumerable.Range(0, rnd.Int(4, 9)).Select(_ => f.PickRandom(subjects))))) + ".";

var sw = System.Diagnostics.Stopwatch.StartNew();

// ═══ 0 · RESCUE — the designs somebody actually drew ════════════════════════════════════════
/* Everything else in this database can be regenerated; a letterhead cannot. It was authored by
   hand in the designer, and the wipe below would take it with no way back. So the designs are
   lifted out first and put back once the org chart exists again, matched by department *name* —
   the ids do not survive a truncate, but "the finance letterhead belongs to finance" does. */
var rescued = new List<(string? UnitName, string? Name, string Layout, string? Logo,
    ReportTemplateScope Scope, ReportTemplateTarget Target, string? Slots, LetterheadStatus Status, bool Shared)>();
if (await db.Database.CanConnectAsync() && await db.ReportTemplates.AnyAsync())
{
    // No tracking: these rows are about to be truncated and re-created, and a tracked OrgUnit
    // makes the reseed collide with itself when the new insert is handed the same generated id.
    var unitNames = await db.OrgUnits.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Name);
    rescued = (await db.ReportTemplates.AsNoTracking().ToListAsync())
        .Select(t => (
            UnitName: t.OrgUnitId is { } o && unitNames.TryGetValue(o, out var n) ? n : null,
            Name: (string?)t.Name, t.LayoutJson, t.LogoBase64,
            t.Scope, t.Target, t.SlotsJson, t.Status, t.IsShared))
        .ToList();
    Console.WriteLine($"• rescued {rescued.Count} existing print designs");
}

// ═══ 1 · RESET ══════════════════════════════════════════════════════════════════════════════
Console.WriteLine("• wiping every table (FK checks off) …");
var tableNames = await db.Database
    .SqlQuery<string>($"SELECT TABLE_NAME AS Value FROM information_schema.tables WHERE table_schema = DATABASE() AND table_type = 'BASE TABLE' AND TABLE_NAME <> '__efmigrationshistory'")
    .ToListAsync();
await db.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS=0");
foreach (var t in tableNames) await db.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE `{t}`");
await db.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS=1");
Console.WriteLine($"  {tableNames.Count} tables truncated, auto-increment reset to 1");

// ═══ 2 · BASELINE — the app's own seeder, so demo data sits on the real configuration ═══════
Console.WriteLine("• replaying DbSeeder (permissions · roles · admin · org chart · workflows) …");
var hasher = new PasswordHasher();
// Demo accounts included: they are the "try the system as…" buttons on the login page, and one of
// them (سارة المدير) heads the general directorate below. Seeding them here rather than leaving it
// to the API's first startup is what lets them own demo data instead of arriving to an empty
// system afterwards.
await DbSeeder.SeedAsync(db, hasher, includeDemoAccounts: true);

var institution = await db.Institutions.FirstAsync();
var roles = await db.Roles.ToListAsync();
var baseUnits = await db.OrgUnits.ToListAsync();

// ═══ 3 · ORG CHART ══════════════════════════════════════════════════════════════════════════
// DbSeeder has already built the institution's directorate, its five departments and the manager
// seat in each. Nothing is added here: the demo is of the shape a real installation has.
var units = new List<OrgUnit>(await db.OrgUnits.ToListAsync());

// The five departments, in seeded order.
var depts = DbSeeder.DefaultDepartments
    .Select(d => units.First(u => u.Name == d.Name))
    .ToList();

/* The general directorate holds staff too, and gets its share of the demo data.
 *
 * It was excluded on the grounds that it heads the institution rather than holding staff — but
 * the demo manager account is filed under it, so signing in as her produced a system with nothing
 * in it at all: no colleagues, no documents, no correspondence, every chart empty. A unit with a
 * person in it and no data is not a neutral omission, it is the first thing a visitor sees.
 *
 * What kept it out was that records filed under an unmanaged unit belong to nobody. So it is
 * given a head, below, and stops being unmanaged. */
var root = units.First(u => u.ParentId == null);
var staffedUnits = new List<OrgUnit>(depts) { root };

var positions = await db.Positions.ToListAsync();
// A deputy seat under each head, so escalation has somewhere to travel and the delegation demo
// below has two seats to run between.
foreach (var u in staffedUnits)
{
    var p = new Position { Title = $"نائب مدير {u.Name}", Code = $"P-{positions.Count + 1:D3}", OrgUnitId = u.Id, Rank = 5, IsActive = true };
    db.Positions.Add(p);
    positions.Add(p);
}
await db.SaveChangesAsync();

// ═══ 4 · USERS — every role, known test credentials, and the awkward cases ══════════════════
Console.WriteLine($"• users ({StaffPerDepartment} per unit across the five departments and the directorate) …");
var pwd = hasher.Hash("password123");
// The baseline admin and the five department heads DbSeeder created. Their seats are already
// filled, which is what makes them managers as far as the access scope is concerned.
var users = await db.Users.ToListAsync();

// department id → the head DbSeeder put in its manager seat.
var deptHeads = DbSeeder.DefaultDepartments
    .Select(d => (Unit: units.First(u => u.Name == d.Name), Head: users.First(u => u.Email == d.ManagerEmail)))
    .ToDictionary(x => x.Unit.Id, x => x.Head);

/* The directorate's head: سارة المدير, the demo manager account, whose department this already is.
 *
 * Holding the seat is what makes someone a head — the Manager role says what they may do, the
 * manager seat says which unit they do it to. She had the role and no seat, so the system treated
 * her as an ordinary employee of a unit with nothing in it: no staff list, no departmental mail,
 * no colleagues' activity in her log, and a dashboard of zeros. */
var sara = users.First(u => u.Email == "demo.manager@archiving.local");
var dgSeat = positions.FirstOrDefault(p => p.OrgUnitId == root.Id && p.Rank >= 20)
    ?? positions.First(p => p.OrgUnitId == root.Id);
dgSeat.CurrentOccupantUserId = sara.Id;
db.PositionAssignments.Add(new PositionAssignment { PositionId = dgSeat.Id, UserId = sara.Id, IsCurrent = true });
root.ManagerPositionId = dgSeat.Id;
await db.SaveChangesAsync();
deptHeads[root.Id] = sara;

var namedAccounts = new (string Email, string Name, string RoleName, ConfidentialityLevel Clearance)[]
{
    ("admin@test.com",     "مدير النظام التجريبي", "System Administrator", ConfidentialityLevel.HighlyConfidential),
    ("manager@test.com",   "مدير الإدارة",         "Manager",              ConfidentialityLevel.Confidential),
    ("archivist@test.com", "أمين الأرشيف",         "Archive Officer",      ConfidentialityLevel.Confidential),
    ("employee@test.com",  "موظف تجريبي",          "Employee",             ConfidentialityLevel.Internal),
};
foreach (var (email, name, roleName, clearance) in namedAccounts)
{
    var u = new User
    {
        FullName = name, Email = email, PasswordHash = pwd, JobTitle = f.PickRandom(jobTitles),
        // The archivist belongs in the archive; the rest sit in the first department, so the
        // try-it accounts land somewhere with colleagues to see rather than in an empty unit.
        OrgUnitId = (roleName == "Archive Officer" ? depts[^1] : depts[0]).Id,
        Clearance = clearance, IsActive = true,
        Gender = Gender.NotSpecified, Phone = "0500000000",
        LastLoginAt = DateTime.UtcNow.AddHours(-rnd.Int(1, 48)),
    };
    db.Users.Add(u);
    await db.SaveChangesAsync();
    db.UserRoles.Add(new UserRole { UserId = u.Id, RoleId = roles.First(r => r.Name == roleName).Id, AssignedAt = DateTime.UtcNow });
    users.Add(u);
}
await db.SaveChangesAsync();

// Edge cases: boundary lengths, unicode/emoji, quoting hazards, inactive, no department.
var edgeUsers = new List<User>
{
    new() { FullName = "ع", Email = "min@test.com", PasswordHash = pwd, JobTitle = "x", Clearance = ConfidentialityLevel.Public, IsActive = true, Gender = Gender.NotSpecified },
    new() { FullName = new string('م', 190), Email = "max@test.com", PasswordHash = pwd, JobTitle = new string('و', 120), Clearance = ConfidentialityLevel.Public, IsActive = true, Gender = Gender.Female },
    new() { FullName = "مستخدم 🚀 اختبار ✅ إيموجي 👨‍💻", Email = "emoji@test.com", PasswordHash = pwd, JobTitle = "مطوّر 👨‍💻", Clearance = ConfidentialityLevel.Internal, IsActive = true, Gender = Gender.Male },
    new() { FullName = "O'Brien \"Quote\" <tag> & ;--", Email = "special@test.com", PasswordHash = pwd, JobTitle = "SQL/HTML edge", Clearance = ConfidentialityLevel.Internal, IsActive = true, Gender = Gender.NotSpecified },
    new() { FullName = "مستخدم معطّل", Email = "inactive@test.com", PasswordHash = pwd, JobTitle = "معطّل", Clearance = ConfidentialityLevel.Public, IsActive = false, Gender = Gender.Male },
    new() { FullName = "بلا إدارة", Email = "noorg@test.com", PasswordHash = pwd, JobTitle = "بلا قسم", Clearance = ConfidentialityLevel.Public, IsActive = true, Gender = Gender.NotSpecified },
};
foreach (var u in edgeUsers)
{
    if (u.Email != "noorg@test.com") u.OrgUnitId = f.PickRandom(staffedUnits).Id;   // one user keeps a null department
    db.Users.Add(u);
    users.Add(u);
}
await db.SaveChangesAsync();
foreach (var u in edgeUsers)
    db.UserRoles.Add(new UserRole { UserId = u.Id, RoleId = roles.First(r => r.Name == "Employee").Id, AssignedAt = DateTime.UtcNow });
await db.SaveChangesAsync();

/* Staff, department by department.
 *
 * Roles are not picked at random: a second Manager inside a department would be a manager who
 * heads nothing (the seat is taken), which reads as a bug when their view turns out to be an
 * employee's. Each department gets one deputy who does hold a seat, one archivist, and the rest
 * ordinary staff — a shape where "what can this person see" has a different answer per row. */
var bulk = new List<User>();
var staffByDept = new Dictionary<long, List<User>>();
var employeeRole = roles.First(r => r.Name == "Employee").Id;
var archiveRole = roles.First(r => r.Name == "Archive Officer").Id;
var managerRole = roles.First(r => r.Name == "Manager").Id;
var pendingRoles = new List<(User User, long RoleId, DateTime At)>();
var deputySeats = new Dictionary<long, Position>();
foreach (var d in staffedUnits) deputySeats[d.Id] = positions.First(p => p.OrgUnitId == d.Id && p.Rank == 5);

foreach (var dept in staffedUnits)
{
    var mine = new List<User>();
    for (var i = 0; i < StaffPerDepartment; i++)
    {
        var male = rnd.Bool();
        var first = male ? f.PickRandom(givenM) : f.PickRandom(givenF);
        var fam = f.PickRandom(family);
        var isDeputy = i == 0;
        var isArchivist = i == 1;
        var u = new User
        {
            FullName = $"{first} {fam}", SecondName = f.PickRandom(givenM), FamilyName = fam,
            Gender = male ? Gender.Male : Gender.Female,
            NationalId = rnd.Bool(0.7f) ? rnd.ReplaceNumbers("##########") : null,
            Email = $"user{users.Count + bulk.Count:D3}@archiving.local",
            Phone = rnd.Bool(0.8f) ? rnd.ReplaceNumbers("05########") : null,
            JobTitle = isDeputy ? $"نائب مدير {dept.Name}" : isArchivist ? "أمين أرشيف" : f.PickRandom(jobTitles),
            OrgUnitId = dept.Id,
            PasswordHash = pwd,
            // A deputy needs to read what their head reads; ordinary staff sit at or below
            // Internal, so the confidential correspondence in the register stays out of reach.
            Clearance = isDeputy ? ConfidentialityLevel.Confidential
                : isArchivist ? ConfidentialityLevel.Confidential
                : f.PickRandom(ConfidentialityLevel.Public, ConfidentialityLevel.Internal),
            IsActive = rnd.Bool(0.92f),
            LastLoginAt = rnd.Bool(0.75f) ? DateTime.UtcNow.AddDays(-rnd.Int(0, 200)) : null,
            MfaEnabled = rnd.Bool(0.15f),
            MfaMethod = rnd.Bool(0.15f) ? MfaMethod.Totp : MfaMethod.None,
        };
        bulk.Add(u);
        mine.Add(u);
        pendingRoles.Add((u,
            isDeputy ? managerRole : isArchivist ? archiveRole : employeeRole,
            DateTime.UtcNow.AddDays(-rnd.Int(1, 400))));
    }
    staffByDept[dept.Id] = mine;
}
db.Users.AddRange(bulk);
await db.SaveChangesAsync();
// Ids exist only after the insert above, so the grants are written in a second pass.
foreach (var (u, roleId, at) in pendingRoles)
    db.UserRoles.Add(new UserRole { UserId = u.Id, RoleId = roleId, AssignedAt = at });
users.AddRange(bulk);
await db.SaveChangesAsync();

// The deputy actually takes the deputy seat, which is what a delegation can later run through.
foreach (var dept in staffedUnits)
{
    var deputy = staffByDept[dept.Id][0];
    var seat = deputySeats[dept.Id];
    seat.CurrentOccupantUserId = deputy.Id;
    db.PositionAssignments.Add(new PositionAssignment { PositionId = seat.Id, UserId = deputy.Id, StartDate = DateTime.UtcNow.AddDays(-rnd.Int(30, 900)), IsCurrent = true });
}
for (var i = 0; i + 1 < Math.Min(positions.Count, 10); i += 2)
    db.PositionDelegations.Add(new PositionDelegation
    {
        FromPositionId = positions[i].Id, ToPositionId = positions[i + 1].Id,
        StartDate = DateTime.UtcNow.AddDays(-10), EndDate = DateTime.UtcNow.AddDays(20),
        Scope = f.PickRandom<DelegationScope>(), IsActive = true, Reason = "إجازة سنوية",
    });

// Refresh tokens: live, expired and revoked.
foreach (var u in users.Take(40))
    for (var i = 0; i < 3; i++)
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = u.Id, Token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            ExpiresAt = i == 1 ? DateTime.UtcNow.AddDays(-3) : DateTime.UtcNow.AddDays(7),
            RevokedAt = i == 2 ? DateTime.UtcNow.AddDays(-1) : null,
            CreatedByIp = f.Internet.Ip(),
        });
foreach (var u in users.Take(25))
    db.UserTablePrefs.Add(new UserTablePref { UserId = u.Id, TableKey = "documents", ConfigJson = """{"columns":["title","type","status"],"order":"asc"}""" });
foreach (var u in users.Take(30))
    foreach (var n in new[] { "مهم", "قيد المتابعة", "أرشيف شخصي" })
        db.Folders.Add(new Folder { UserId = u.Id, Name = n });
await db.SaveChangesAsync();

// ═══ 5 · CATEGORIES / TYPES / CUSTOM FIELDS ═════════════════════════════════════════════════
var cats = new List<DocumentCategory>();
foreach (var name in new[] { "مالية", "إدارية", "قانونية", "فنية", "موارد بشرية" })
{
    var c = new DocumentCategory { Name = name, Code = rnd.AlphaNumeric(4).ToUpperInvariant(), IsActive = true };
    db.DocumentCategories.Add(c);
    cats.Add(c);
}
await db.SaveChangesAsync();
foreach (var parent in cats.Take(2))
    db.DocumentCategories.Add(new DocumentCategory { ParentId = parent.Id, Name = $"{parent.Name} — فرعي", IsActive = true });
await db.SaveChangesAsync();

var types = new List<DocumentType>();
var typeNames = new[] { "عقد", "فاتورة", "تقرير", "قرار إداري", "محضر اجتماع", "كشف رواتب", "مراسلة واردة", "مراسلة صادرة", "شهادة", "طلب", "مذكرة", "سجل" };
foreach (var (name, i) in typeNames.Select((n, i) => (n, i)))
{
    var t = new DocumentType
    {
        Name = name, NameEn = f.Lorem.Word(), Code = $"DT-{i + 1:D2}",
        CategoryId = f.PickRandom(cats).Id,
        OrgUnitId = i % 3 == 0 ? null : f.PickRandom(units).Id,     // every third type is عام
        Number = i + 1, Note = i % 4 == 0 ? "ملاحظة تجريبية" : null,
        DefaultConfidentiality = f.PickRandom<ConfidentialityLevel>(),
        RetentionValue = f.PickRandom(1, 3, 5, 10, 25), RetentionUnit = f.PickRandom<RetentionUnit>(),
        RetentionMonths = f.PickRandom(12, 36, 60, 120, 300),
        RequiresApproval = rnd.Bool(0.4f),
        RequiresPhysicalArchiving = rnd.Bool(0.5f),
        AllowedUploadSources = f.PickRandom(UploadSource.All, UploadSource.Scanner, UploadSource.Pdf),
        IsActive = i != typeNames.Length - 1,
    };
    db.DocumentTypes.Add(t);
    types.Add(t);
}
await db.SaveChangesAsync();

var fields = new List<CustomFieldDefinition>
{
    new() { EntityType = "Document", FieldKey = "amount", Label = "المبلغ", FieldType = CustomFieldType.Number, DocumentTypeId = types[1].Id, Searchable = true, SortOrder = 1, IsActive = true },
    new() { EntityType = "Document", FieldKey = "party", Label = "الطرف الثاني", FieldType = CustomFieldType.Text, DocumentTypeId = types[0].Id, Searchable = true, SortOrder = 2, IsActive = true },
    new() { EntityType = "Document", FieldKey = "sign_date", Label = "تاريخ التوقيع", FieldType = CustomFieldType.Date, SortOrder = 3, IsActive = true },
    new() { EntityType = "Document", FieldKey = "status_choice", Label = "الحالة", FieldType = CustomFieldType.Choice, Options = "جديد|قيد التنفيذ|منتهٍ", SortOrder = 4, IsActive = true },
    new() { EntityType = "Document", FieldKey = "inactive_field", Label = "حقل معطّل", FieldType = CustomFieldType.Text, SortOrder = 5, IsActive = false },
};
db.CustomFieldDefinitions.AddRange(fields);
await db.SaveChangesAsync();

// ═══ 6 · PHYSICAL STORAGE — buildings → rooms → cabinets → shelves → boxes ══════════════════
Console.WriteLine("• physical locations …");
var boxes = new List<Box>();
var locBoxes = new List<PhysicalLocation>();
for (var b = 0; b < 3; b++)
{
    var building = new Building { NameAr = $"المبنى {(b == 0 ? "الرئيسي" : b == 1 ? "الشرقي" : "الغربي")}", NameEn = $"Building {b + 1}", Code = $"B{b + 1}", Address = "العنوان التجريبي", IsActive = true };
    db.Buildings.Add(building);
    var locB = new PhysicalLocation { Name = building.NameAr, Type = PhysicalLocationType.Building, Code = building.Code, IsActive = true };
    db.PhysicalLocations.Add(locB);
    await db.SaveChangesAsync();

    for (var r = 0; r < 2; r++)
    {
        var room = new Room { BuildingId = building.Id, NameAr = $"غرفة الأرشيف {r + 1}", RoomNumber = $"{b + 1}0{r + 1}", Floor = $"{r}", IsActive = true };
        db.Rooms.Add(room);
        var locR = new PhysicalLocation { ParentId = locB.Id, Name = room.NameAr, Type = PhysicalLocationType.Room, Code = room.RoomNumber, IsActive = true };
        db.PhysicalLocations.Add(locR);
        await db.SaveChangesAsync();

        var cab = new Cabinet { RoomId = room.Id, NameAr = $"خزانة {r + 1}", IsActive = true };
        db.Cabinets.Add(cab);
        var locC = new PhysicalLocation { ParentId = locR.Id, Name = cab.NameAr, Type = PhysicalLocationType.Cabinet, IsActive = true };
        db.PhysicalLocations.Add(locC);
        await db.SaveChangesAsync();

        for (var s = 0; s < 2; s++)
        {
            var shelf = new Shelf { CabinetId = cab.Id, ShelfNumber = $"رف {s + 1}", Capacity = 12, IsActive = true };
            db.Shelves.Add(shelf);
            var locS = new PhysicalLocation { ParentId = locC.Id, Name = shelf.ShelfNumber, Type = PhysicalLocationType.Shelf, IsActive = true };
            db.PhysicalLocations.Add(locS);
            await db.SaveChangesAsync();

            for (var x = 0; x < 3; x++)
            {
                var box = new Box { ShelfId = shelf.Id, BoxCode = $"BX-{boxes.Count + 1:D3}", Barcode = $"BC{boxes.Count + 1:D6}", Capacity = 50, CurrentCount = 0, IsActive = true };
                db.Boxes.Add(box);
                boxes.Add(box);
                var locX = new PhysicalLocation { ParentId = locS.Id, Name = box.BoxCode, Type = PhysicalLocationType.Box, Code = box.BoxCode, IsActive = true };
                db.PhysicalLocations.Add(locX);
                locBoxes.Add(locX);
            }
            await db.SaveChangesAsync();
        }
    }
}
var allRooms = await db.Rooms.ToListAsync();
for (var i = 0; i + 1 < allRooms.Count; i += 2)
    db.RoomConnections.Add(new RoomConnection { RoomId = allRooms[i].Id, ConnectedRoomId = allRooms[i + 1].Id, ConnectionType = "باب" });
await db.SaveChangesAsync();

// ═══ 7 · DOCUMENTS ══════════════════════════════════════════════════════════════════════════
Console.WriteLine($"• documents ({DocumentCount}) …");
var docs = new List<Document>();
var today = DateOnly.FromDateTime(DateTime.UtcNow);
for (var i = 0; i < DocumentCount; i++)
{
    var type = f.PickRandom(types);
    // Dates deliberately span far past → today → future so lifecycle screens have something at
    // every stage: long expired, expiring inside the alert window, and decades away.
    var docDate = i % 40 == 0 ? new DateOnly(2005, 1, 1)
        : i % 37 == 0 ? today
        : DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-rnd.Int(0, 2200)));
    var expiry = i % 23 == 0 ? today.AddDays(-rnd.Int(1, 400))
        : i % 19 == 0 ? today.AddDays(rnd.Int(1, 25))
        : docDate.AddMonths(Math.Min(type.RetentionMonths, 600));
    /* A document belongs to a department and was registered by somebody in it. Picking the two
       independently produced files whose owning department had never heard of their author,
       which makes every "my department's documents" screen read as noise. */
    var owner = f.PickRandom(staffedUnits);
    var author = f.PickRandom(staffByDept[owner.Id]);
    var d = new Document
    {
        DocumentNumber = $"DOC-{docDate.Year}-{i + 1:D5}",
        Title = i % 50 == 0 ? "و" : i % 49 == 0 ? new string('ط', 240) : $"{f.PickRandom(docTitles)} — {owner.Name}",
        Description = i % 7 == 0 ? null : Body(),
        DocumentTypeId = type.Id,
        CategoryId = rnd.Bool(0.8f) ? f.PickRandom(cats).Id : null,
        OwningOrgUnitId = owner.Id,
        CreatedBy = author.Id,
        OwnerPositionId = positions.FirstOrDefault(p => p.OrgUnitId == owner.Id)?.Id,
        Confidentiality = f.PickRandom<ConfidentialityLevel>(),
        Status = f.PickRandom<DocumentStatus>(),
        Keywords = i % 9 == 0 ? null : f.PickRandom(subjects),
        RetentionMonths = type.RetentionMonths,
        DocumentDate = docDate,
        ExpiryDate = expiry,
        Version = 1,
        IsLatestVersion = true,
        BoxId = rnd.Bool(0.45f) ? f.PickRandom(boxes).Id : null,
        ArchiveMode = ArchiveMode.Manual,
        IsDeleted = i % 33 == 0,                                        // soft-deleted rows exist
        IsTombstone = i % 61 == 0,                                      // content destroyed, metadata kept
    };
    if (d.IsTombstone) d.DestroyedAtUtc = DateTime.UtcNow.AddDays(-rnd.Int(1, 300));
    db.Documents.Add(d);
    docs.Add(d);
    if (i % 100 == 99) await db.SaveChangesAsync();
}
await db.SaveChangesAsync();

// A few versioned documents, so the version chain is exercised.
var versions = new List<Document>();
foreach (var parent in docs.Take(15))
{
    parent.IsLatestVersion = false;
    versions.Add(new Document
    {
        DocumentNumber = $"{parent.DocumentNumber}-v2", Title = $"{parent.Title} (نسخة 2)",
        DocumentTypeId = parent.DocumentTypeId, OwningOrgUnitId = parent.OwningOrgUnitId,
        Confidentiality = parent.Confidentiality, Status = DocumentStatus.Active,
        RetentionMonths = parent.RetentionMonths, DocumentDate = parent.DocumentDate,
        ExpiryDate = parent.ExpiryDate, Version = 2, ParentDocumentId = parent.Id, IsLatestVersion = true,
        ArchiveMode = ArchiveMode.Manual,
    });
}
db.Documents.AddRange(versions);
await db.SaveChangesAsync();
docs.AddRange(versions);

var atts = new List<DocumentAttachment>();
foreach (var d in docs.Where((_, i) => i % 2 == 0))
{
    for (var k = 0; k < rnd.Int(1, 3); k++)
    {
        var ext = f.PickRandom("pdf", "docx", "xlsx", "jpg", "png");
        atts.Add(new DocumentAttachment
        {
            DocumentId = d.Id, FileName = $"مرفق-{d.Id}-{k + 1}.{ext}",
            ContentType = ext == "pdf" ? "application/pdf" : ext == "jpg" ? "image/jpeg" : "application/octet-stream",
            FileExtension = ext, SizeBytes = rnd.Long(1024, 12_000_000),
            StorageKey = $"seed/{d.Id}/{Guid.NewGuid():N}.{ext}",
            Checksum = Convert.ToHexString(rnd.Bytes(32)), ChecksumAlgorithm = "SHA256",
            IsScanned = rnd.Bool(0.3f), ContentDestroyed = d.IsTombstone,
            ExtractionStatus = f.PickRandom<TextExtractionStatus>(),
            Kind = k == 0 ? AttachmentKind.Original : f.PickRandom<AttachmentKind>(),
        });
    }
}
db.DocumentAttachments.AddRange(atts);
await db.SaveChangesAsync();

foreach (var d in docs.Where((_, i) => i % 5 == 0))
    db.DocumentFavorites.Add(new DocumentFavorite { UserId = f.PickRandom(users).Id, DocumentId = d.Id });
foreach (var d in docs.Where((_, i) => i % 6 == 0))
    db.DocumentShares.Add(new DocumentShare { DocumentId = d.Id, SharedWithUserId = f.PickRandom(users).Id, SharedByUserId = f.PickRandom(users).Id, CanEdit = rnd.Bool(), IsRead = rnd.Bool() });
foreach (var d in docs.Where((_, i) => i % 4 == 0))
    db.DocumentNotes.Add(new DocumentNote { DocumentId = d.Id, UserId = f.PickRandom(users).Id, Content = Body(1) });
foreach (var (d, i) in docs.Select((d, i) => (d, i)).Where(x => x.i % 3 == 0))
    db.PhysicalArchiveItems.Add(new PhysicalArchiveItem
    {
        DocumentId = d.Id, PhysicalLocationId = f.PickRandom(locBoxes).Id,
        BoxNumber = $"BX-{rnd.Int(1, 36):D3}", FileNumber = $"F-{i:D4}",
        ArchivedAt = DateTime.UtcNow.AddDays(-rnd.Int(1, 500)), ArchivedByUserId = f.PickRandom(users).Id,
        Notes = i % 10 == 0 ? "ملاحظة أرشفة" : null,
    });
foreach (var fld in fields.Where(x => x.IsActive))
    foreach (var d in docs.Take(60))
        db.CustomFieldValues.Add(new CustomFieldValue
        {
            FieldId = fld.Id, EntityType = "Document", EntityId = d.Id,
            Value = fld.FieldType switch
            {
                CustomFieldType.Number => rnd.Int(0, 500000).ToString(CultureInfo.InvariantCulture),
                CustomFieldType.Date => DateTime.UtcNow.AddDays(-rnd.Int(0, 900)).ToString("yyyy-MM-dd"),
                CustomFieldType.Choice => f.PickRandom("جديد", "قيد التنفيذ", "منتهٍ"),
                _ => f.PickRandom(orgNames),
            },
        });
await db.SaveChangesAsync();

// ═══ 8 · EXTERNAL DIRECTORY ═════════════════════════════════════════════════════════════════
var extOrgs = new List<ExternalOrganization>();
foreach (var (name, i) in orgNames.Concat(Enumerable.Range(0, 20).Select(x => $"جهة خارجية {x + 1}")).Select((n, i) => (n, i)))
{
    var o = new ExternalOrganization
    {
        Name = name, NameEn = f.Company.CompanyName(), Type = f.PickRandom<ExternalOrgType>(),
        Code = $"EX-{i + 1:D3}", IsActive = i % 11 != 0, Notes = i % 5 == 0 ? "ملاحظة" : null,
    };
    db.ExternalOrganizations.Add(o);
    extOrgs.Add(o);
}
await db.SaveChangesAsync();
var contacts = new List<ExternalContact>();
foreach (var o in extOrgs)
    for (var c = 0; c < rnd.Int(1, 3); c++)
        contacts.Add(new ExternalContact { OrganizationId = o.Id, Name = ArabicName(), Title = f.PickRandom(jobTitles), IsActive = true });
db.ExternalContacts.AddRange(contacts);
await db.SaveChangesAsync();
foreach (var c in contacts)
{
    db.ContactChannels.Add(new ContactChannel { ContactId = c.Id, Type = ContactChannelType.Email, Value = f.Internet.Email(), IsPrimary = true });
    db.ContactChannels.Add(new ContactChannel { ContactId = c.Id, Type = ContactChannelType.Mobile, Value = rnd.ReplaceNumbers("05########"), IsPrimary = false });
}
await db.SaveChangesAsync();

// ═══ 9 · MAIL REGISTERS ═════════════════════════════════════════════════════════════════════
Console.WriteLine($"• mail registers (diwan {DiwanCount} · internal {InternalCount}) …");
var mailCats = new List<MailCategory>();
foreach (var (n, i) in new[] { "عام", "مالي", "إداري", "قانوني", "فني", "عاجل", "شكاوى", "طلبات" }.Select((n, i) => (n, i)))
{
    var c = new MailCategory { InstitutionId = institution.Id, Name = n, NameEn = f.Lorem.Word(), Scope = f.PickRandom<MailCategoryScope>(), IsActive = true, SortOrder = i };
    db.MailCategories.Add(c);
    mailCats.Add(c);
}
await db.SaveChangesAsync();

var diwan = new List<DiwanMail>();
for (var i = 0; i < DiwanCount; i++)
{
    var incoming = rnd.Bool();
    var mailDate = DateTime.UtcNow.AddDays(-rnd.Int(0, 700));
    var org = f.PickRandom(extOrgs);
    // Registered by a real clerk in a real department: the register is scoped by who logged the
    // letter and which bureau it was routed to, so an entry with no author belongs to nobody.
    var regDept = f.PickRandom(staffedUnits);
    var clerk = f.PickRandom(staffByDept[regDept.Id]);
    var m = new DiwanMail
    {
        CreatedBy = clerk.Id,
        InstitutionId = institution.Id,
        RegisterNumber = $"{(incoming ? "IN" : "OUT")}-{mailDate.Year}-{i + 1:D5}",
        ReferenceNumber = rnd.Bool(0.6f) ? $"REF/{rnd.Int(100, 999)}" : null,
        Direction = incoming ? DiwanDirection.Incoming : DiwanDirection.Outgoing,
        ExternalOrgId = org.Id, ExternalOrgName = org.Name,
        AddresseeName = rnd.Bool(0.7f) ? ArabicName() : null,
        AddresseeTitle = rnd.Bool(0.5f) ? f.PickRandom(jobTitles) : null,
        Subject = i % 45 == 0 ? "م" : f.PickRandom(subjects),
        Body = i % 8 == 0 ? null : $"<p>{Body()}</p>",
        Keywords = rnd.Bool(0.7f) ? "أرشفة مراسلات" : null,
        MailDate = mailDate, RegistrationDate = mailDate.AddHours(rnd.Int(1, 48)),
        IssueDate = rnd.Bool(0.6f) ? mailDate.AddDays(-1) : null,
        SentDate = incoming ? null : mailDate.AddDays(rnd.Int(0, 3)),
        ResponseDeadline = i % 6 == 0 ? DateTime.UtcNow.AddDays(-rnd.Int(1, 30))
            : i % 5 == 0 ? DateTime.UtcNow.AddDays(rnd.Int(1, 20)) : null,
        ResponsePeriodDays = rnd.Bool(0.5f) ? rnd.Int(3, 30) : null,
        RemindOnNoReply = rnd.Bool(0.4f),
        CategoryId = f.PickRandom(mailCats).Id,
        ActionType = rnd.Bool(0.8f) ? f.PickRandom<CorrespondenceActionType>() : null,
        Classification = f.PickRandom<ConfidentialityLevel>(),
        Priority = f.PickRandom<Priority>(),
        Status = f.PickRandom<CorrespondenceStatus>(),
        FileNumber = rnd.Bool(0.4f) ? $"F/{rnd.Int(1, 999)}" : null,
        IsArchived = rnd.Bool(0.25f),
        IsDeleted = i % 41 == 0,
    };
    if (m.IsArchived) { m.ArchivedAt = mailDate.AddDays(3); m.ArchivedDocumentId = f.PickRandom(docs).Id; }
    db.DiwanMails.Add(m);
    diwan.Add(m);
    if (i % 100 == 99) await db.SaveChangesAsync();
}
await db.SaveChangesAsync();

foreach (var m in diwan)
{
    foreach (var u in f.PickRandom(staffedUnits, rnd.Int(1, 3)))
        db.DiwanBureaus.Add(new DiwanBureau { DiwanMailId = m.Id, OrgUnitId = u.Id });
    if (rnd.Bool(0.55f))
        db.DiwanAttachments.Add(new DiwanAttachment
        {
            DiwanMailId = m.Id, StorageKey = $"seed/diwan/{m.Id}.pdf", FileName = $"{m.RegisterNumber}.pdf",
            ContentType = "application/pdf", SizeBytes = rnd.Long(20_000, 4_000_000), Version = 1,
            Checksum = Convert.ToHexString(rnd.Bytes(32)), OcrStatus = f.PickRandom<CorrespondenceOcrStatus>(),
            OcrText = rnd.Bool(0.4f) ? Body(1) : null,
        });
}
for (var i = 7; i < diwan.Count; i += 7)
{
    diwan[i].ReplyToId = diwan[i - 1].Id;
    diwan[i].ParentId = diwan[i - 1].Id;
}
await db.SaveChangesAsync();

var internals = new List<InternalMail>();
for (var i = 0; i < InternalCount; i++)
{
    var sender = f.PickRandom(users);
    var mailDate = DateTime.UtcNow.AddDays(-rnd.Int(0, 500));
    db.InternalMails.Add(new InternalMail
    {
        InstitutionId = institution.Id,
        MailNumber = $"INT-{mailDate.Year}-{i + 1:D5}",
        SenderUserId = sender.Id, SenderName = sender.FullName, SenderOrgUnitId = sender.OrgUnitId,
        Subject = f.PickRandom(subjects),
        Body = i % 9 == 0 ? null : $"<p>{Body()}</p>",
        MailDate = mailDate, RegistrationDate = mailDate,
        ResponseDeadline = i % 6 == 0 ? DateTime.UtcNow.AddDays(-rnd.Int(1, 20)) : null,
        CategoryId = f.PickRandom(mailCats).Id,
        ActionType = rnd.Bool(0.8f) ? f.PickRandom<CorrespondenceActionType>() : null,
        Classification = f.PickRandom<ConfidentialityLevel>(),
        Priority = f.PickRandom<Priority>(),
        Status = f.PickRandom<CorrespondenceStatus>(),
        IsArchived = rnd.Bool(0.2f),
        IsDeleted = i % 47 == 0,
    });
}
await db.SaveChangesAsync();
internals = await db.InternalMails.IgnoreQueryFilters().ToListAsync();

foreach (var m in internals)
{
    /* Most internal traffic stays inside a department, and some of it crosses. Drawing recipients
       from the whole institution produced almost none of the former, so a head's departmental
       view — the thing that distinguishes them from their staff — came out nearly empty. */
    var sameDept = m.SenderOrgUnitId is { } sd && staffByDept.TryGetValue(sd, out var colleagues)
        ? colleagues : null;
    var pool = sameDept is not null && rnd.Bool(0.6f) ? sameDept : users;
    // The first addressee always goes on the To line: a message that exists only as carbon
    // copies has nobody it was actually addressed to, which no real correspondence does.
    var firstRecipient = true;
    foreach (var u in f.PickRandom(pool, Math.Min(rnd.Int(1, 4), pool.Count)))
    {
        db.InternalRecipients.Add(new InternalRecipient
        {
            InternalMailId = m.Id,
            Addressing = firstRecipient ? CorrespondenceAddressing.To : f.PickRandom<CorrespondenceAddressing>(),
            TargetKind = RecipientKind.Employee, TargetId = u.Id, TargetName = u.FullName,
        });
        firstRecipient = false;
        var status = f.PickRandom<CorrespondenceReadStatus>();
        db.InternalReads.Add(new InternalRead
        {
            InternalMailId = m.Id, UserId = u.Id, ReadStatus = status,
            DeliveredAt = status == CorrespondenceReadStatus.Undelivered ? null : m.MailDate.AddMinutes(5),
            FirstReadAt = status == CorrespondenceReadStatus.Read ? m.MailDate.AddHours(2) : null,
            LastReadAt = status == CorrespondenceReadStatus.Read ? m.MailDate.AddHours(3) : null,
        });
    }
    if (rnd.Bool(0.5f))
        db.InternalAttachments.Add(new InternalAttachment
        {
            InternalMailId = m.Id, StorageKey = $"seed/internal/{m.Id}.pdf", FileName = $"{m.MailNumber}.pdf",
            ContentType = "application/pdf", SizeBytes = rnd.Long(10_000, 2_000_000), Version = 1,
            OcrStatus = f.PickRandom<CorrespondenceOcrStatus>(),
        });
}
await db.SaveChangesAsync();

var groups = new List<CorrespondenceGroup>();
foreach (var n in new[] { "مدراء الإدارات", "لجنة الأرشيف", "فريق التقنية" })
{
    var g = new CorrespondenceGroup { InstitutionId = institution.Id, Name = n, Description = "مجموعة تجريبية", IsActive = true };
    db.CorrespondenceGroups.Add(g);
    groups.Add(g);
}
await db.SaveChangesAsync();
foreach (var g in groups)
{
    foreach (var u in f.PickRandom(users, 5)) db.CorrespondenceGroupMembers.Add(new CorrespondenceGroupMember { GroupId = g.Id, MemberType = GroupMemberType.User, UserId = u.Id });
    foreach (var u in f.PickRandom(staffedUnits, 2)) db.CorrespondenceGroupMembers.Add(new CorrespondenceGroupMember { GroupId = g.Id, MemberType = GroupMemberType.OrgUnit, OrgUnitId = u.Id });
}
await db.SaveChangesAsync();

// ═══ 10 · WORKFLOW ══════════════════════════════════════════════════════════════════════════
Console.WriteLine("• workflow · retention · disposition …");
var defs = await db.WorkflowDefinitions.ToListAsync();
var stages = await db.WorkflowStages.ToListAsync();
var instances = new List<WorkflowInstance>();
foreach (var d in docs.Where((_, i) => i % 6 == 0).Take(80))
{
    var def = f.PickRandom(defs);
    var defStages = stages.Where(s => s.WorkflowDefinitionId == def.Id).ToList();
    var status = f.PickRandom<WorkflowStatus>();
    instances.Add(new WorkflowInstance
    {
        WorkflowDefinitionId = def.Id, EntityType = "Document", EntityId = d.Id,
        CurrentStageId = defStages.FirstOrDefault()?.Id,
        Status = status, StartedAt = DateTime.UtcNow.AddDays(-rnd.Int(1, 120)),
        DueAt = DateTime.UtcNow.AddDays(rnd.Int(-10, 20)),
        CompletedAt = status == WorkflowStatus.Completed ? DateTime.UtcNow.AddDays(-rnd.Int(0, 10)) : null,
        InitiatedByUserId = f.PickRandom(users).Id,
    });
}
db.WorkflowInstances.AddRange(instances);
await db.SaveChangesAsync();
foreach (var inst in instances)
    for (var k = 0; k < rnd.Int(1, 3); k++)
        db.WorkflowTasks.Add(new WorkflowTask
        {
            WorkflowInstanceId = inst.Id, StageId = inst.CurrentStageId ?? stages[0].Id,
            AssignedToUserId = f.PickRandom(users).Id,
            AssignedToPositionId = f.PickRandom(positions).Id,
            Status = f.PickRandom<WorkflowTaskStatus>(),
            DueAt = inst.DueAt ?? DateTime.UtcNow.AddDays(3),
            ActionTaken = f.PickRandom<WorkflowActionType>(),
            CompletedAt = rnd.Bool(0.4f) ? DateTime.UtcNow.AddDays(-rnd.Int(1, 20)) : null,
            IsEscalated = rnd.Bool(0.1f),
        });
foreach (var s in stages.Take(3))
    db.EscalationRules.Add(new EscalationRule
    {
        WorkflowDefinitionId = s.WorkflowDefinitionId, WorkflowStageId = s.Id, EscalationLevel = 1,
        OverdueMinutes = 1440, Action = f.PickRandom<EscalationAction>(),
        EscalateToPositionId = f.PickRandom(positions).Id, NotifyOriginator = true, IsActive = true,
    });
await db.SaveChangesAsync();

var rules = await db.EscalationRules.ToListAsync();
foreach (var inst in instances.Take(12))
    db.EscalationLogs.Add(new EscalationLog
    {
        WorkflowInstanceId = inst.Id, WorkflowStageId = inst.CurrentStageId ?? stages[0].Id,
        EscalationRuleId = rules[0].Id, EscalationLevel = 1,
        EscalatedToPositionId = f.PickRandom(positions).Id,
        ActionTaken = EscalationAction.NotifyOnly, EscalatedAt = DateTime.UtcNow.AddDays(-rnd.Int(1, 30)),
    });
await db.SaveChangesAsync();

// ═══ 11 · RETENTION / DISPOSITION / DESTRUCTION ═════════════════════════════════════════════
foreach (var t in types)
    db.RetentionPolicies.Add(new RetentionPolicy
    {
        DocumentTypeId = t.Id, RetentionMonths = t.RetentionMonths,
        DisposalAction = f.PickRandom<DisposalAction>(), RequiresApproval = rnd.Bool(),
        Description = "سياسة حفظ تجريبية", TriggerType = f.PickRandom<RetentionTriggerType>(),
        DefaultAction = f.PickRandom<RetentionDefaultAction>(), RequiresLegalApprovalForRenewal = rnd.Bool(0.3f),
    });
await db.SaveChangesAsync();
var policies = await db.RetentionPolicies.ToListAsync();

foreach (var d in docs.Where((_, i) => i % 3 == 0))
    db.DocumentRetentions.Add(new DocumentRetention
    {
        DocumentId = d.Id, RetentionPolicyId = f.PickRandom(policies).Id,
        TriggerDate = d.DocumentDate ?? today, ExpiryDate = d.ExpiryDate ?? today.AddYears(5),
        Status = f.PickRandom<DocumentRetentionStatus>(),
    });
foreach (var d in docs.Where((_, i) => i % 11 == 0))
    db.RetentionAlerts.Add(new RetentionAlert
    {
        DocumentId = d.Id, Stage = f.PickRandom<RetentionAlertStage>(),
        DueDate = today.AddDays(rnd.Int(-60, 90)),
        NotifiedAt = rnd.Bool(0.6f) ? DateTime.UtcNow.AddDays(-rnd.Int(1, 30)) : null,
    });
foreach (var d in docs.Where((_, i) => i % 17 == 0))
    db.DisposalRequests.Add(new DisposalRequest
    {
        DocumentId = d.Id, Action = f.PickRandom<DisposalAction>(), Status = f.PickRandom<DisposalRequestStatus>(),
        RequestedByUserId = f.PickRandom(users).Id, RequestedAt = DateTime.UtcNow.AddDays(-rnd.Int(1, 200)),
        Justification = "انتهاء المدة النظامية للحفظ",
    });
await db.SaveChangesAsync();

var dispositions = new List<DispositionRequest>();
foreach (var d in docs.Where((_, i) => i % 13 == 0).Take(40))
{
    var status = f.PickRandom<DispositionStatus>();
    dispositions.Add(new DispositionRequest
    {
        DocumentId = d.Id, RequestedAtUtc = DateTime.UtcNow.AddDays(-rnd.Int(1, 180)),
        RequestedAction = f.PickRandom<DispositionAction>(), Reason = "انتهاء مدة الحفظ النظامية",
        RequestedByUserId = f.PickRandom(users).Id, Status = status,
        Method = f.PickRandom<DestructionMethod>(),
        VerifiedByUserId = status != DispositionStatus.PendingVerification ? f.PickRandom(users).Id : null,
        VerifiedAtUtc = status != DispositionStatus.PendingVerification ? DateTime.UtcNow.AddDays(-rnd.Int(1, 60)) : null,
        RejectedByUserId = status == DispositionStatus.Rejected ? f.PickRandom(users).Id : null,
        RejectionReason = status == DispositionStatus.Rejected ? "الوثيقة مطلوبة لقضية قائمة" : null,
        NewExpiryDate = status == DispositionStatus.Approved ? today.AddYears(3) : null,
    });
}
db.DispositionRequests.AddRange(dispositions);
await db.SaveChangesAsync();
foreach (var r in dispositions.Where(x => x.Status == DispositionStatus.Completed))
    db.DispositionCertificates.Add(new DispositionCertificate
    {
        DispositionRequestId = r.Id, CertificateNumber = $"CERT-DISP-{r.Id:D5}",
        DocumentIds = r.DocumentId.ToString(), DestructionMethod = r.Method.ToString(),
        VerifiedByUserId = r.VerifiedByUserId, GeneratedAtUtc = DateTime.UtcNow.AddDays(-rnd.Int(1, 30)),
    });
await db.SaveChangesAsync();

var destructions = new List<DestructionRequest>();
for (var i = 0; i < 12; i++)
{
    var status = f.PickRandom<DestructionStatus>();
    destructions.Add(new DestructionRequest
    {
        Status = status, Reason = "إتلاف دوري وفق الجدول الزمني",
        RequestedByUserId = f.PickRandom(users).Id, RequestedAtUtc = DateTime.UtcNow.AddDays(-rnd.Int(5, 300)),
        ReviewedByUserId = status >= DestructionStatus.PendingApproval ? f.PickRandom(users).Id : null,
        ApprovedByUserId = status >= DestructionStatus.Approved ? f.PickRandom(users).Id : null,
        ScheduledForUtc = status == DestructionStatus.Scheduled ? DateTime.UtcNow.AddDays(rnd.Int(1, 30)) : null,
        DecisionNote = status == DestructionStatus.Rejected ? "مرفوض — حفظ دائم" : null,
    });
}
db.DestructionRequests.AddRange(destructions);
await db.SaveChangesAsync();
foreach (var r in destructions)
{
    foreach (var d in f.PickRandom(docs, rnd.Int(2, 6)))
        db.DestructionItems.Add(new DestructionItem
        {
            DestructionRequestId = r.Id, DocumentId = d.Id, Method = f.PickRandom<DestructionMethod>(),
            ChecksumBefore = Convert.ToHexString(rnd.Bytes(32)),
            Outcome = r.Status == DestructionStatus.Completed ? "تم الإتلاف" : null,
        });
    if (r.Status == DestructionStatus.Completed)
        db.DestructionCertificates.Add(new DestructionCertificate
        {
            DestructionRequestId = r.Id, CertificateNumber = $"CERT-DEST-{r.Id:D5}",
            IssuedAtUtc = DateTime.UtcNow.AddDays(-rnd.Int(1, 60)),
        });
}
foreach (var d in docs.Take(8))
    db.LegalHolds.Add(new LegalHold
    {
        Reason = "قضية منظورة أمام المحكمة", Scope = LegalHoldScope.Document, DocumentId = d.Id,
        PlacedByUserId = f.PickRandom(users).Id, PlacedAtUtc = DateTime.UtcNow.AddDays(-rnd.Int(10, 200)),
        ReleasedAtUtc = rnd.Bool(0.3f) ? DateTime.UtcNow.AddDays(-1) : null,
    });
await db.SaveChangesAsync();

// ═══ 13 · PRESERVATION (OAIS) ═══════════════════════════════════════════════════════════════
db.PreservationPolicies.Add(new PreservationPolicy
{
    Name = "سياسة الحفظ الرقمي", Description = "PDF/A-2b + تحقق دوري",
    TargetPdfAConformance = "PDF/A-2b", AutoNormalizeOnIngest = true,
    FixityAlgorithm = "SHA256", FixityCadenceDays = 90, AllowedPreservationFormats = "PDF/A,TIFF",
});
foreach (var n in new[] { "الموظفون", "المدققون الخارجيون", "الباحثون" })
    db.DesignatedCommunities.Add(new DesignatedCommunity { Name = n, Description = "فئة مستفيدة", RenderingExpectations = "PDF/A" });
foreach (var d in docs.Where((_, i) => i % 7 == 0))
    db.InformationPackages.Add(new InformationPackage
    {
        DocumentId = d.Id, Type = f.PickRandom<PackageType>(), FileCount = rnd.Int(1, 5),
        TotalBytes = rnd.Long(10_000, 50_000_000),
        Manifest = JsonSerializer.Serialize(new { files = new[] { "content.pdf", "metadata.xml" }, checksum = "SHA256" }),
    });
foreach (var a in atts.Where((_, i) => i % 4 == 0))
{
    var ok = rnd.Bool(0.95f);
    db.FixityChecks.Add(new FixityCheck
    {
        DocumentAttachmentId = a.Id, Algorithm = "SHA256", ExpectedHash = a.Checksum,
        ActualHash = ok ? a.Checksum : Convert.ToHexString(rnd.Bytes(32)),
        Result = ok ? FixityResult.Verified : FixityResult.Failed,
        CheckedAt = DateTime.UtcNow.AddDays(-rnd.Int(1, 90)), CheckedByUserId = f.PickRandom(users).Id,
    });
    db.RepresentationInfos.Add(new RepresentationInfo
    {
        DocumentAttachmentId = a.Id, FormatName = a.FileExtension.ToUpperInvariant(),
        MimeType = a.ContentType, PronomPuid = "fmt/276", RenderingNote = "يفتح بأي قارئ PDF",
    });
}
foreach (var d in docs.Take(120))
{
    db.RecordAgents.Add(new RecordAgent { RecordType = "Document", RecordId = d.Id, AgentKind = AgentKind.User, AgentId = f.PickRandom(users).Id, Role = f.PickRandom<AgentRole>() });
    db.RecordAgents.Add(new RecordAgent { RecordType = "Document", RecordId = d.Id, AgentKind = AgentKind.OrgUnit, AgentId = d.OwningOrgUnitId, Role = AgentRole.Owner });
}
for (var i = 0; i + 1 < 60; i += 2)
    db.RecordRelationships.Add(new RecordRelationship
    {
        SourceType = "Document", SourceId = docs[i].Id, TargetType = "Document", TargetId = docs[i + 1].Id,
        Type = f.PickRandom<RecordRelationshipType>(),
    });
await db.SaveChangesAsync();

// ═══ 14 · OPERATIONS ════════════════════════════════════════════════════════════════════════
Console.WriteLine("• notifications · audit · metrics …");
var notifs = new List<Notification>();
foreach (var u in users.Take(60))
    for (var k = 0; k < rnd.Int(1, 5); k++)
    {
        var read = rnd.Bool(0.5f);
        notifs.Add(new Notification
        {
            RecipientUserId = u.Id, Title = f.PickRandom(subjects), Body = Body(1),
            Type = f.PickRandom<NotificationType>(),
            Channels = NotificationChannel.InApp | (rnd.Bool(0.4f) ? NotificationChannel.Email : NotificationChannel.None),
            EntityType = "Document", EntityId = f.PickRandom(docs).Id,
            IsRead = read, ReadAt = read ? DateTime.UtcNow.AddDays(-rnd.Int(0, 20)) : null,
            IsEscalation = rnd.Bool(0.15f),
        });
    }
db.Notifications.AddRange(notifs);
await db.SaveChangesAsync();
foreach (var n in notifs.Where((_, i) => i % 3 == 0))
{
    var ok = rnd.Bool(0.85f);
    db.NotificationDeliveries.Add(new NotificationDelivery
    {
        NotificationId = n.Id, Channel = NotificationChannel.Email, Succeeded = ok,
        SentAt = DateTime.UtcNow.AddDays(-rnd.Int(0, 20)),
        Error = ok ? null : "SMTP timeout",
    });
}
await db.SaveChangesAsync();

// Audit log: hash-chained exactly the way AuditWriter does it, so chain verification passes.
string? prevHash = null;
var auditActions = new[] { "Created", "Edited", "Deleted", "Viewed", "Printed", "Archived", "Approved", "Login", "Restored" };
for (var i = 0; i < 600; i++)
{
    var d = f.PickRandom(docs);
    var e = new AuditLog
    {
        UserId = f.PickRandom(users).Id, Action = f.PickRandom(auditActions),
        EntityType = "Document", EntityId = d.Id, EntityTitle = d.Title,
        IpAddress = f.Internet.Ip(), UserAgent = f.Internet.UserAgent(), MachineName = "SEED",
        CreatedAt = AuditHash.Truncate(DateTime.UtcNow.AddMinutes(-(600 - i) * 7)),
        PreviousHash = prevHash,
    };
    e.Hash = AuditHash.Compute(e, prevHash);
    prevHash = e.Hash;
    db.AuditLogs.Add(e);
}
for (var i = 0; i < 40; i++)
    db.EventLogs.Add(new EventLog
    {
        EventType = f.PickRandom("DocumentCreated", "MailRegistered", "WorkflowStarted", "BackupCompleted"),
        EventData = JsonSerializer.Serialize(new { id = rnd.Int(1, 400), at = DateTime.UtcNow }),
        TriggeredByUserId = f.PickRandom(users).Id.ToString(),
        OccurredAt = DateTime.UtcNow.AddDays(-rnd.Int(0, 60)),
        ProcessedAt = rnd.Bool(0.8f) ? DateTime.UtcNow : null,
    });
for (var day = 0; day < 90; day++)
    foreach (var metric in new[] { "DocumentsCreated", "MailsRegistered", "StorageBytes" })
        db.DailyMetricSnapshots.Add(new DailyMetricSnapshot
        {
            Date = DateTime.UtcNow.Date.AddDays(-day), InstitutionId = institution.Id,
            MetricType = metric, Value = rnd.Int(0, 500),
            Breakdown = JsonSerializer.Serialize(new { byUnit = units.Take(3).Select(u => u.Name) }),
        });
foreach (var (entity, pattern) in new[] { ("Document", "DOC-{yyyy}-{seq:00000}"), ("DiwanMail", "IN-{yyyy}-{seq:00000}"), ("InternalMail", "INT-{yyyy}-{seq:00000}") })
    db.NumberingTemplates.Add(new NumberingTemplate
    {
        EntityType = entity, InstitutionId = institution.Id, Pattern = pattern,
        ResetFrequency = ResetFrequency.Yearly, CurrentSequence = rnd.Int(100, 900),
        CurrentYear = DateTime.UtcNow.Year, IsActive = true,
    });
await db.SaveChangesAsync();

var hooks = new List<IntegrationHook>();
foreach (var (n, ev) in new[] { ("مزامنة الوثائق", "DocumentCreated"), ("إشعار البريد", "MailRegistered") })
    hooks.Add(new IntegrationHook
    {
        Name = n, EventType = ev, CallbackUrl = "https://example.test/hook", HttpMethod = "POST",
        // Inactive on purpose: the callback host does not resolve, and an active hook would retry
        // against it forever and fill the log with DNS failures.
        SecretKey = Guid.NewGuid().ToString("N"), IsActive = false, MaxRetries = 3, RetryDelaySeconds = 60,
        InstitutionId = institution.Id,
    });
db.IntegrationHooks.AddRange(hooks);
await db.SaveChangesAsync();
foreach (var h in hooks)
    for (var k = 0; k < 6; k++)
    {
        var st = f.PickRandom<DeliveryStatus>();
        db.WebhookDeliveries.Add(new WebhookDelivery
        {
            IntegrationHookId = h.Id, EventType = h.EventType,
            PayloadJson = JsonSerializer.Serialize(new { id = rnd.Int(1, 400) }),
            AttemptNumber = k + 1, HttpStatusCode = st == DeliveryStatus.Delivered ? 200 : 500,
            Status = st, DeliveredAt = st == DeliveryStatus.Delivered ? DateTime.UtcNow : null,
            ErrorMessage = st == DeliveryStatus.Failed ? "connection refused" : null,
        });
    }
db.AutoBackupSettings.Add(new AutoBackupSettings
{
    Enabled = true, TargetPath = @"E:\Backups\archiving", IntervalHours = 24,
    LastRunAt = DateTime.UtcNow.AddHours(-6), LastRunStatus = "Success",
});

// ═══ 15 · PRINT DESIGNS ═════════════════════════════════════════════════════════════════════
var layout = """{"kind":"rich","html":"<h1>{{institutionName}}</h1><p>{{number}} — {{date}}</p><p>{{subject}}</p>","page":{"size":"A4"}}""";
var admin = users.First(u => u.Email == DbSeeder.AdminEmail);

/// <summary>The head a rescued personal design belongs to, matched on the name the seeder gives
/// them — the only handle that survives the truncate.</summary>
User? OwnerOf(string? designName) => designName is null
    ? null
    : deptHeads.Values.FirstOrDefault(h => designName == $"ترويسة {h.FullName}");

// The rescued designs come back first, so a department that already had a hand-drawn letterhead
// keeps it rather than being handed a generated one.
foreach (var r in rescued)
    db.ReportTemplates.Add(new ReportTemplate
    {
        InstitutionId = institution.Id,
        Scope = r.Scope,
        /* A personal design is re-attached to its owner by name. User ids do not survive a
           truncate, so matching on id was impossible and everything personal landed on the
           administrator — which meant each re-seed left the previous run's head letterheads
           piled up under the admin while fresh ones were made for the new heads.
           Anything that matches nobody still passes to the administrator, who can share it out
           or delete it; the alternative is a design visible to nobody. */
        UserId = r.Scope == ReportTemplateScope.User
            ? OwnerOf(r.Name)?.Id ?? admin.Id
            : null,
        OrgUnitId = r.Scope == ReportTemplateScope.OrgUnit
            ? units.FirstOrDefault(u => u.Name == r.UnitName)?.Id ?? depts[0].Id : null,
        Target = r.Target, Name = r.Name ?? "تصميم", LayoutJson = r.Layout, LogoBase64 = r.Logo,
        SlotsJson = r.Slots, Status = r.Status, IsShared = r.Shared, IsActive = true,
    });
await db.SaveChangesAsync();

var haveDefault = await db.ReportTemplates.AnyAsync(t => t.Scope == ReportTemplateScope.Default);
if (!haveDefault)
    db.ReportTemplates.Add(new ReportTemplate { InstitutionId = institution.Id, Scope = ReportTemplateScope.Default, Target = ReportTemplateTarget.All, Name = "التصميم الافتراضي", LayoutJson = layout, IsActive = true, Status = LetterheadStatus.Active });

// One letterhead per department, so every head has something of their own to open — and so the
// gallery demonstrates that they see theirs and not their neighbour's.
foreach (var u in staffedUnits)
    if (!await db.ReportTemplates.AnyAsync(t => t.Scope == ReportTemplateScope.OrgUnit && t.OrgUnitId == u.Id))
        db.ReportTemplates.Add(new ReportTemplate { InstitutionId = institution.Id, Scope = ReportTemplateScope.OrgUnit, OrgUnitId = u.Id, Target = ReportTemplateTarget.All, Name = $"ترويسة {u.Name}", LayoutJson = layout, IsActive = true, Status = LetterheadStatus.Active });

// A personal design for each department head — the case the visibility rule is strictest about,
// since a personal letterhead carries its owner's name and signature block.
// Guarded like the department ones: the rescued set already contains the personal designs from
// the previous run, and adding unconditionally meant every re-seed left another copy behind.
foreach (var head in deptHeads.Values)
    if (!await db.ReportTemplates.AnyAsync(t => t.Scope == ReportTemplateScope.User && t.UserId == head.Id))
        db.ReportTemplates.Add(new ReportTemplate { InstitutionId = institution.Id, Scope = ReportTemplateScope.User, UserId = head.Id, Target = ReportTemplateTarget.Correspondence, Name = $"ترويسة {head.FullName}", LayoutJson = layout, IsActive = true, Status = LetterheadStatus.Active });
await db.SaveChangesAsync();

/* One design published across the institution, so the feature is visible in the demo rather
   than only in the code: the archive department's letterhead, which is the one every department
   legitimately prints on when it files records. Only the administrator can set this. */
var archiveDeptId = depts[^1].Id;
var sharedDesign = await db.ReportTemplates
    .FirstOrDefaultAsync(t => t.Scope == ReportTemplateScope.OrgUnit && t.OrgUnitId == archiveDeptId);
if (sharedDesign is not null) sharedDesign.IsShared = true;

/* And one head covering a second department — the acting appointment the grant exists for.
   The head of الشؤون الإدارية also runs الموارد البشرية, so their staff list, their departmental
   mail and their monitoring log all span two units on a fresh demo database. */
var acting = deptHeads[depts[3].Id];
db.UserOrgUnitAccess.Add(new UserOrgUnitAccess
{
    UserId = acting.Id, OrgUnitId = depts[0].Id, GrantedByUserId = admin.Id,
    Note = "تكليف بالإشراف على الموارد البشرية خلال إجازة مديرها",
});
await db.SaveChangesAsync();
Console.WriteLine($"• designs: {rescued.Count} rescued, department + personal letterheads added, "
    + $"«{depts[^1].Name}» shared institution-wide");
Console.WriteLine($"• «{acting.FullName}» also covers «{depts[0].Name}»");

// ═══ SUMMARY ════════════════════════════════════════════════════════════════════════════════
sw.Stop();
Console.WriteLine($"\n✅ seeded in {sw.Elapsed.TotalSeconds:F1}s\n");
var all = await db.Database
    .SqlQuery<string>($"SELECT TABLE_NAME AS Value FROM information_schema.tables WHERE table_schema = DATABASE() AND table_type = 'BASE TABLE'")
    .ToListAsync();
long total = 0;
var empty = new List<string>();
Console.WriteLine($"{"TABLE",-34}{"ROWS",8}");
foreach (var t in all.OrderBy(x => x))
{
    var n = (await db.Database.SqlQueryRaw<long>($"SELECT COUNT(*) AS Value FROM `{t}`").ToListAsync())[0];
    total += n;
    if (n == 0) empty.Add(t);
    else Console.WriteLine($"{t,-34}{n,8}");
}
Console.WriteLine($"\n{"TOTAL",-34}{total,8} rows across {all.Count - empty.Count} tables");
if (empty.Count > 0) Console.WriteLine($"\n⚠ still empty ({empty.Count}): {string.Join(", ", empty)}");
Console.WriteLine($"""

DEPARTMENT HEADS — password: {DbSeeder.DepartmentManagerPassword}
  Each one heads their department: they see its staff, its correspondence and its activity log,
  and nothing of any other department's.
{string.Join("\n", DbSeeder.DefaultDepartments.Select(d => $"  {d.ManagerEmail,-40}{d.Name}"))}

  The head of {DbSeeder.DefaultDepartments[3].Name} also covers {DbSeeder.DefaultDepartments[0].Name} —
  sign in as them to see a manager whose scope spans two departments.

STAFF — password: password123
  {StaffPerDepartment} per unit, as user000@archiving.local … upward, across the five departments
  and الإدارة العامة. In each: one deputy (Manager role, holds the deputy seat), one archivist,
  and ordinary employees who see only their own work.

DEMO PERSONAS — password on the login page's "try it as…" buttons
  سارة المدير heads الإدارة العامة, so she has staff, correspondence and a populated dashboard.
  خالد الأرشيفي is in قسم الأرشيف; ليان الموظفة is an ordinary employee.

TEST ACCOUNTS — password: password123
  admin@test.com      System Administrator
  manager@test.com    Manager
  archivist@test.com  Archive Officer
  employee@test.com   Employee
  min@test.com        1-char name          max@test.com      190-char name
  emoji@test.com      emoji + ZWJ name     special@test.com  quotes / HTML / SQL chars
  inactive@test.com   IsActive = false     noorg@test.com    no department

ADMINISTRATOR
  {DbSeeder.AdminEmail} / {DbSeeder.AdminPassword} — the only account that sees the institution.
""");

static string? ArgValue(string[] a, string key)
{
    var i = Array.IndexOf(a, key);
    return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
}

static string Mask(string cs) =>
    System.Text.RegularExpressions.Regex.Replace(cs, "(?i)(pwd|password)=[^;]*", "$1=***");

static string ReadConnectionString()
{
    var candidates = new[]
    {
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Archiving.Api/appsettings.Development.json")),
        Path.GetFullPath("backend/src/Archiving.Api/appsettings.Development.json"),
        Path.GetFullPath("src/Archiving.Api/appsettings.Development.json"),
    };
    foreach (var p in candidates)
    {
        if (!File.Exists(p)) continue;
        using var doc = JsonDocument.Parse(File.ReadAllText(p));
        if (doc.RootElement.TryGetProperty("ConnectionStrings", out var csEl) &&
            csEl.TryGetProperty("Default", out var def))
            return def.GetString()!;
    }
    throw new InvalidOperationException("No connection string found — pass --connection \"…\".");
}
