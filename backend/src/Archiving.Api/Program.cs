using System.Text;
using Microsoft.EntityFrameworkCore;
using Archiving.Api.Authorization;
using Archiving.Api.Common;
using Archiving.Application.Common.Interfaces;
using Archiving.Infrastructure;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Security;
using Archiving.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Lets the same binary run as a Windows Service (the deployment layout on Windows Server) with
// no separate host build: outside a service it is a no-op, so `dotnet run` is unaffected.
builder.Host.UseWindowsService(o => o.ServiceName = "Archiving");

builder.Services.AddControllers(o =>
    {
        o.Filters.Add<Archiving.Api.Common.AuditActionFilter>();
    })
    .AddJsonOptions(o =>
    {
        // Emit all timestamps as UTC (…Z) so browsers render them in the viewer's local time.
        o.JsonSerializerOptions.Converters.Add(new Archiving.Api.Common.UtcDateTimeConverter());
    });
builder.Services.AddOpenApi();

// Current-user resolution from the request.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// Data + security layer (MySQL via EF Core 10, auth services).
builder.Services.AddInfrastructure(builder.Configuration);

// JWT bearer authentication.
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<Archiving.Api.Common.IOnlineUserTracker, Archiving.Api.Common.OnlineUserTracker>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddHostedService<Archiving.Api.Common.AutoBackupHostedService>();

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    p.AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    if (builder.Environment.IsDevelopment())
        // Dev: reflect any origin so the Vite dev server works on whatever port it lands on (5173/5174/…).
        p.SetIsOriginAllowed(_ => true);
    else
        p.WithOrigins(corsOrigins);
}));

var app = builder.Build();

// Apply migrations + seed RBAC and the first admin (development).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await WaitForDatabaseAsync(db, app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup"));
    await db.Database.MigrateAsync();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    // Demo accounts only outside a real deployment: their passwords are handed to the login page.
    await DbSeeder.SeedAsync(db, hasher, includeDemoAccounts: app.Environment.IsDevelopment());

    // Map any legacy free-form correspondence designs onto the slot schema. Idempotent, and it
    // never deletes: what it cannot map is flagged for admin review with the original preserved.
    await scope.ServiceProvider.GetRequiredService<LetterheadMigrator>().RunAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                       // OpenAPI JSON at /openapi/v1.json
    app.MapScalarApiReference();            // API explorer UI at /scalar/v1
}
else
{
    app.UseHttpsRedirection();              // enforce HTTPS outside local dev
}

app.UseCors();

/* The built SPA, when it was published next to the API (wwwroot/index.html).
   One service then serves both the pages and the API on one port, which is what makes a Windows
   Server install a copy-and-start job with no IIS or second web server in front. In development
   the folder is absent and Vite serves the SPA, so none of this is registered. */
var spaIndex = Path.Combine(app.Environment.WebRootPath ?? "", "index.html");
var hasSpa = System.IO.File.Exists(spaIndex);
if (hasSpa)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<Archiving.Api.Common.LastSeenMiddleware>();
// Licence gate: after authentication, so /api/auth and the licence screens stay reachable on an
// unlicensed instance and everything else answers per License:Enforcement.
app.UseMiddleware<Archiving.Api.Common.LicenseEnforcementMiddleware>();
app.MapControllers();

/* Liveness probe. LicenseEnforcementMiddleware has always allow-listed "/health" but nothing ever
   mapped it, so the path 404'd and no load balancer or monitor could tell a wedged service from a
   healthy one. Anonymous and deliberately uninformative — it reports that the process is up and the
   object store is writable, and nothing about versions, paths or the database, because an unauthenticated
   endpoint is not the place to describe the deployment. Encryption and KMS state are reported by the
   authenticated /api/archive/encryption/health instead. */
app.MapGet("/health", (IFileStorage storage) =>
{
    var root = (storage as Archiving.Infrastructure.Storage.EncryptedFileServerStorage)?.Root;
    var storageReady = root is not null && Directory.Exists(root);
    return storageReady
        ? Results.Ok(new { status = "healthy" })
        : Results.Json(new { status = "degraded" }, statusCode: StatusCodes.Status503ServiceUnavailable);
}).AllowAnonymous();

// Client-side routing: anything that is not an API call or a real file is the SPA's own route
// (/app/documents, a bookmarked design…), so it gets index.html and the router takes over.
if (hasSpa) app.MapFallbackToFile("index.html");

app.Run();


/// <summary>
/// Blocks until the database answers, or gives up after <paramref name="timeout"/>.
///
/// This exists because of what happens at boot on a server. The service is set to start
/// automatically, and Windows starts it in parallel with MySQL/SQL Server rather than after it — so
/// on a cold boot the very first thing the app does, apply migrations, can hit a database engine
/// that has not finished opening its files. That throws before the web host is running, the process
/// exits, and the site is simply down until somebody notices and starts the service by hand. It
/// looks exactly like "it does not start automatically", which is the symptom it was reported as.
///
/// Waiting is the right shape of fix rather than retrying the migration: a database that is coming
/// up needs seconds, and a database that is genuinely misconfigured should still fail loudly rather
/// than have the app pretend for minutes. The deadline draws that line.
/// </summary>
static async Task WaitForDatabaseAsync(DbContext db, ILogger logger, TimeSpan? timeout = null)
{
    var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromMinutes(3));
    var delay = TimeSpan.FromSeconds(2);
    var attempt = 0;

    while (true)
    {
        attempt++;
        try
        {
            if (await db.Database.CanConnectAsync())
            {
                if (attempt > 1) logger.LogInformation("Database reachable after {Attempts} attempts.", attempt);
                return;
            }
        }
        catch (Exception ex) when (DateTime.UtcNow < deadline)
        {
            // Expected while the engine is still starting: connection refused, "server has gone
            // away", a half-open socket. Logged at Information because on a cold boot this is the
            // normal path, not a fault.
            logger.LogInformation("Waiting for the database (attempt {Attempt}): {Message}", attempt, ex.Message);
        }

        if (DateTime.UtcNow >= deadline)
        {
            logger.LogError("Database still unreachable after {Timeout}. Startup will fail.", timeout ?? TimeSpan.FromMinutes(3));
            return;   // fall through to MigrateAsync, whose exception carries the real diagnostic
        }

        await Task.Delay(delay);
        // Back off gently up to 10s: quick enough that a database ready in four seconds costs
        // nothing, slow enough that three minutes of waiting is not three minutes of log spam.
        delay = TimeSpan.FromSeconds(Math.Min(10, delay.TotalSeconds * 1.5));
    }
}
