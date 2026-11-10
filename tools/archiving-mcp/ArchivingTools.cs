using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace Archiving.Mcp;

/// <summary>
/// The tools this server offers.
///
/// <b>Every one of them reads.</b> Nothing here creates, edits, destroys, approves a disposition,
/// or releases a legal hold — and that is a design decision, not an unfinished corner. مراسيل is a
/// records system whose value rests on each change being attributable to a person who decided to
/// make it; a model that can approve its own destruction request breaks that on the first useful
/// day. Search, read and explain is the whole safe surface, and it is already most of what anyone
/// wants from an assistant over an archive. Adding a write tool is a deliberate act — see the
/// README before doing it.
///
/// Descriptions are written for a model that has never seen this system: they say what the thing
/// is in the organisation's own terms (a كتاب, a register entry, a retention rule) rather than
/// restating the endpoint, because the model chooses tools from these sentences alone.
/// </summary>
[McpServerToolType]
internal static class ArchivingTools
{
    // Page sizes are clamped here as well as server-side. A model asked to "check everything" will
    // otherwise request ten thousand rows and spend the context window on one answer.
    private const int MaxPageSize = 50;

    [McpServerTool(Name = "whoami")]
    [Description("Report which Archiving (مراسيل) account this server is signed in as, and the exact "
               + "permissions it holds. Call this first when a search returns nothing or a request is "
               + "refused — the usual cause is that this account lacks a permission or the record sits in "
               + "a department outside its scope, which no amount of rephrasing will change.")]
    public static async Task<string> WhoAmI(ArchivingClient client, CancellationToken ct)
    {
        // Goes to the wire rather than reading the cached login payload: permissions can be changed
        // by an administrator while this server is running, and a stale answer here would send the
        // model chasing an access problem that no longer exists (or miss one that now does).
        var me = await client.GetAsync("api/auth/me", ct: ct);
        return Json(me);
    }

    // ── documents ────────────────────────────────────────────────────────────

    [McpServerTool(Name = "search_documents")]
    [Description("Search the document register. Documents are the archived records themselves — "
               + "contracts, decisions, reports, scanned paper — as distinct from correspondence, which is "
               + "the letters that move between parties. Returns one page of matches with their id, title, "
               + "type, status and dates. Use get_document afterwards for the full record. All filters are "
               + "optional; with none it returns the most recent documents.")]
    public static async Task<string> SearchDocuments(
        ArchivingClient client,
        [Description("Free text matched against title, reference number and indexed content (including OCR "
                   + "text from scanned pages). Arabic and English both work.")] string? search = null,
        [Description("Restrict to one status, e.g. Draft, Active, Archived. Omit for any status.")] string? status = null,
        [Description("Restrict to one document type. Get valid ids from list_document_types.")] long? documentTypeId = null,
        [Description("Restrict to one department or unit. Get valid ids from list_org_units.")] long? owningOrgUnitId = null,
        [Description("Earliest document date, as YYYY-MM-DD.")] string? dateFrom = null,
        [Description("Latest document date, as YYYY-MM-DD.")] string? dateTo = null,
        [Description("1-based page number. Default 1.")] int page = 1,
        [Description("Results per page, 1 to 50. Default 20.")] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await client.GetAsync("api/documents", new Dictionary<string, string?>
        {
            ["search"] = search,
            ["status"] = status,
            ["documentTypeId"] = documentTypeId?.ToString(),
            ["owningOrgUnitId"] = owningOrgUnitId?.ToString(),
            ["dateFrom"] = dateFrom,
            ["dateTo"] = dateTo,
            ["page"] = Math.Max(1, page).ToString(),
            ["pageSize"] = Math.Clamp(pageSize, 1, MaxPageSize).ToString(),
        }, ct);
        return Json(result);
    }

    [McpServerTool(Name = "get_document")]
    [Description("Retrieve one document in full by its id: metadata, custom fields, security "
               + "classification, retention rule, attachments and physical location. Does not return file "
               + "contents — attachments are listed, not downloaded.")]
    public static async Task<string> GetDocument(
        ArchivingClient client,
        [Description("The document id, as returned by search_documents.")] long id,
        CancellationToken ct = default)
        => Json(await client.GetAsync($"api/documents/{id}", ct: ct));

    [McpServerTool(Name = "get_document_timeline")]
    [Description("The full chronological history of one document, merging every kind of event: audit "
               + "entries (who created, edited, viewed, downloaded or printed it), workflow steps and "
               + "approvals, correspondence that references it, and lifecycle changes. This is the tool to "
               + "use for any question about what happened to a record, who handled it, or where it stalled.")]
    public static async Task<string> GetDocumentTimeline(
        ArchivingClient client,
        [Description("The document id.")] long id,
        CancellationToken ct = default)
        => Json(await client.GetAsync($"api/documents/{id}/timeline", ct: ct));

    [McpServerTool(Name = "list_document_types")]
    [Description("List the configured document types with their ids. Types are defined by each "
               + "organisation rather than fixed by the system, so read this before filtering by type "
               + "instead of assuming a type exists.")]
    public static async Task<string> ListDocumentTypes(ArchivingClient client, CancellationToken ct = default)
        => Json(await client.GetAsync("api/documents/types", ct: ct));

    // ── correspondence ───────────────────────────────────────────────────────

    [McpServerTool(Name = "search_correspondence")]
    [Description("Search the correspondence register (الديوان) — incoming, outgoing and internal letters. "
               + "Note how this register works: incoming mail is never entered by hand, it is generated when "
               + "an outgoing letter is approved and sent to an internal recipient, so every incoming entry "
               + "is linked to the outgoing letter that produced it. Returns matches with id, subject, "
               + "number, direction and parties.")]
    public static async Task<string> SearchCorrespondence(
        ArchivingClient client,
        [Description("Free text matched against subject, letter number, and party names.")] string q,
        [Description("1-based page number. Default 1.")] int page = 1,
        [Description("Results per page, 1 to 50. Default 25.")] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await client.GetAsync("api/diwan/search", new Dictionary<string, string?>
        {
            ["q"] = q,
            ["page"] = Math.Max(1, page).ToString(),
            ["pageSize"] = Math.Clamp(pageSize, 1, MaxPageSize).ToString(),
        }, ct);
        return Json(result);
    }

    [McpServerTool(Name = "get_correspondence")]
    [Description("Retrieve one correspondence register entry in full by its id: subject, letter number, "
               + "sender and recipient, direction, confidentiality, status and attachments.")]
    public static async Task<string> GetCorrespondence(
        ArchivingClient client,
        [Description("The register entry id, as returned by search_correspondence.")] long id,
        CancellationToken ct = default)
        => Json(await client.GetAsync($"api/diwan/{id}", ct: ct));

    [McpServerTool(Name = "get_correspondence_thread")]
    [Description("The whole chain around one letter: the original, every reply to it, and every later "
               + "entry citing it as a reference (مرجع). Use this to answer whether something was ever "
               + "answered, or to reconstruct an exchange in order — a single entry rarely tells the story.")]
    public static async Task<string> GetCorrespondenceThread(
        ArchivingClient client,
        [Description("The id of any entry in the chain.")] long id,
        CancellationToken ct = default)
        => Json(await client.GetAsync($"api/diwan/{id}/thread", ct: ct));

    // ── organisation ─────────────────────────────────────────────────────────

    [McpServerTool(Name = "list_org_units")]
    [Description("List the organisational structure — departments, sections and units — with their ids "
               + "and parents. Read this before filtering any search by department, and to translate a "
               + "department name a person used into the id the other tools need.")]
    public static async Task<string> ListOrgUnits(ArchivingClient client, CancellationToken ct = default)
        => Json(await client.GetAsync("api/organization/org-units", ct: ct));

    // ── audit ────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "search_audit_log")]
    [Description("Query the audit log — the record of who did what to which record, and when. It captures "
               + "creation, modification, deletion, download and printing. Use it for access questions "
               + "('who opened this file', 'what did this user do last week'). Requires the Audit.View "
               + "permission; without it this returns a permission error, which is itself the answer.")]
    public static async Task<string> SearchAuditLog(
        ArchivingClient client,
        [Description("Restrict to one kind of record, e.g. Document, DiwanMail, User. Omit for all.")] string? entityType = null,
        [Description("Restrict to one action, e.g. Create, Update, Delete, Download, Print. Omit for all.")] string? action = null,
        [Description("Restrict to one user's actions, by user id.")] long? userId = null,
        [Description("Earliest timestamp, ISO-8601, e.g. 2026-01-31T00:00:00Z.")] string? from = null,
        [Description("Latest timestamp, ISO-8601.")] string? to = null,
        [Description("1-based page number. Default 1.")] int page = 1,
        [Description("Results per page, 1 to 50. Default 50.")] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await client.GetAsync("api/audit/logs", new Dictionary<string, string?>
        {
            ["entityType"] = entityType,
            ["action"] = action,
            ["userId"] = userId?.ToString(),
            ["from"] = from,
            ["to"] = to,
            ["page"] = Math.Max(1, page).ToString(),
            ["pageSize"] = Math.Clamp(pageSize, 1, MaxPageSize).ToString(),
        }, ct);
        return Json(result);
    }

    [McpServerTool(Name = "verify_audit_chain")]
    [Description("Verify the integrity of the audit log. Each entry is hash-linked to the one before it, "
               + "so tampering breaks the chain and this check reports where. Read-only: it inspects the "
               + "chain and never repairs or reseals it. Use it to answer whether the audit trail can still "
               + "be trusted — a broken chain is a serious finding to report plainly, not to work around.")]
    public static async Task<string> VerifyAuditChain(ArchivingClient client, CancellationToken ct = default)
        => Json(await client.GetAsync("api/audit/verify", ct: ct));

    /// <summary>
    /// Indented rather than compact on purpose. These payloads are read by a model, and the few extra
    /// tokens of whitespace buy correctly-attributed fields in deeply nested records — cheaper than a
    /// confident answer drawn from the wrong nesting level.
    /// </summary>
    private static string Json(JsonElement element) =>
        element.ValueKind == JsonValueKind.Undefined
            ? "(no content)"
            : JsonSerializer.Serialize(element, JsonOpts);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        // Arabic must survive as UTF-8, not as \uXXXX escapes. The default encoder escapes every
        // non-ASCII character, turning each Arabic letter into six and inflating the token cost of a
        // register listing several-fold — in a system whose content is mostly Arabic that is the
        // difference between a page of results fitting in the context window and not. Safe here
        // because this JSON goes down a pipe to a model, never into an HTML page.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
