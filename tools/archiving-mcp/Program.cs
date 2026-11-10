using Archiving.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// مراسيل — MCP server.
//
// Exposes the archive to an MCP client (Claude Desktop, Claude Code, or any other) as a set of
// read-only tools, so someone can ask questions of their correspondence and document registers in
// plain language instead of assembling the answer by hand from six screens.
//
// It speaks to the system over its REST API as a real signed-in user, which means it inherits that
// user's permissions, that user's department scope, and — the point — that user's audit trail. See
// ArchivingClient for why it is not allowed to touch the database directly.
//
// Configuration is three environment variables, set by the MCP client:
//   ARCHIVING_API_URL    http://archive.example.com:7070
//   ARCHIVING_USER       the account to sign in as
//   ARCHIVING_PASSWORD   its password
//
// Give it its own least-privileged account. It cannot write anything, but it can read everything
// the account it holds can read, and an MCP server signed in as the administrator is a way to read
// classified correspondence from a laptop.

var builder = Host.CreateApplicationBuilder(args);

// ── logging must never touch stdout ──────────────────────────────────────────
// stdout IS the MCP protocol channel: a single stray log line lands in the middle of a JSON-RPC
// frame and the client drops the connection with a parse error that points nowhere near the cause.
// The default console logger writes to stdout, so it is replaced rather than merely quietened.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.Services.AddSingleton(_ =>
{
    try
    {
        return ArchivingOptions.FromEnvironment();
    }
    catch (InvalidOperationException ex)
    {
        // Thrown before the transport is up, so this is the last moment a human-readable message
        // reaches anyone. Stderr is where MCP clients show server startup failures.
        Console.Error.WriteLine("Archiving MCP server cannot start.");
        Console.Error.WriteLine(ex.Message);
        throw;
    }
});

builder.Services.AddSingleton<ArchivingClient>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
