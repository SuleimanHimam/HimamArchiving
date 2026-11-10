# مراسيل — MCP Server

Exposes the archive to an MCP client (Claude Desktop, Claude Code, or any other) as a set of
**read-only** tools, so someone can ask questions of the correspondence and document registers in
plain language instead of assembling the answer by hand across six screens.

```
MCP client  ──stdio──►  archiving-mcp  ──HTTPS──►  Archiving API  ──►  MySQL
   (Claude)                                        RBAC · dept scope · audit
```

## Why it talks to the API and not the database

Reading MySQL directly would be less code and considerably faster. It is also the one thing this
server must never do.

Everything that makes مراسيل a records system rather than a file share lives **above** the database:
the permission check on each controller action, the department scoping applied per caller, and the
hash-chained audit log — which is written by the API and would simply not record a read that went
around it. An MCP server on the database would be a way to read classified correspondence leaving no
trace, which is exactly what the product tells customers is impossible.

So it signs in over HTTP as a real user and inherits that user's permissions, that user's
department scope, and that user's audit trail. A `403` from the API is a real answer, surfaced to
the model as such.

## Read-only, on purpose

None of the eleven tools writes. Nothing creates, edits, deletes, approves a disposition, or
releases a legal hold.

That is a design decision, not an unfinished corner. The value of this system rests on every change
being attributable to a person who decided to make it. A model that can approve its own destruction
request breaks that on the first useful day — and "search, read, explain" is already most of what
anyone wants from an assistant over an archive.

If you do add a write tool later, three things are not optional: give it its own permission on a
separate account, make the tool description state plainly that it changes records, and keep
destruction, disposition and legal-hold release out of scope regardless. Those three are the ones an
auditor will ask about.

## The tools

| Tool | What it answers |
|---|---|
| `whoami` | Which account this server holds, and its exact permissions |
| `search_documents` | Find documents by text, type, department, status, date range |
| `get_document` | One document in full — metadata, classification, retention, location |
| `get_document_timeline` | Everything that ever happened to a document, merged and in order |
| `list_document_types` | The organisation's configured document types, with ids |
| `search_correspondence` | Find letters in the register (الديوان) |
| `get_correspondence` | One register entry in full |
| `get_correspondence_thread` | The original, its replies, and everything citing it as مرجع |
| `list_org_units` | The department tree, for turning a name into an id |
| `search_audit_log` | Who did what to which record, and when |
| `verify_audit_chain` | Whether the audit trail is still intact |

`get_document_timeline` and `get_correspondence_thread` are the two that earn their keep. Questions
people actually ask — *was this ever answered? where did this stall? who opened this file?* — are
reconstruction questions, and a single record never answers them.

## Setting it up

**1. Create an account for it.** Do not reuse a person's login, and do not give it the
administrator. It cannot write anything, but it can read everything its account can read, and an
MCP server signed in as the administrator is a way to read classified correspondence from a laptop.
Give it a role with the view permissions it needs and nothing else — add `Audit.View` only if you
want the audit tools to work.

**2. Build it.**

```bash
dotnet publish tools/archiving-mcp -c Release
```

Produces a single self-contained `archiving-mcp.exe` — no .NET runtime needed on the machine that
runs it, the same way the scan agent is distributed.

**3. Register it with the client.** For Claude Desktop, in `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "archiving": {
      "command": "C:\\Program Files\\Archiving\\archiving-mcp.exe",
      "env": {
        "ARCHIVING_API_URL": "http://archive.example.com:7070",
        "ARCHIVING_USER": "mcp-reader@example.com",
        "ARCHIVING_PASSWORD": "…"
      }
    }
  }
}
```

For Claude Code: `claude mcp add archiving --env ARCHIVING_API_URL=… --env ARCHIVING_USER=… --env ARCHIVING_PASSWORD=… -- C:\path\archiving-mcp.exe`

All three variables are required; the server refuses to start without them and says which are
missing on stderr.

## Notes

- **Logging goes to stderr, never stdout.** stdout *is* the JSON-RPC channel — one stray log line
  lands mid-frame and the client drops the connection with a parse error pointing nowhere near the
  cause. `Program.cs` replaces the default console logger for this reason; keep it that way.
- **Tokens refresh themselves.** The client signs in on first use and again on any `401`, so a
  server left running overnight recovers by itself after an API restart.
- **Page sizes are clamped to 50** in the tools as well as server-side — a model asked to "check
  everything" will otherwise request ten thousand rows and spend the whole context window on one
  answer.
- **Attachments are listed, not downloaded.** `get_document` returns the attachment metadata; file
  contents are deliberately out of scope.
- Sits behind whatever the API sits behind. Over anything other than a trusted LAN, point
  `ARCHIVING_API_URL` at HTTPS.
