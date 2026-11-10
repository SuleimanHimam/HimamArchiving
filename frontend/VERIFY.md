# Verification harness

Spec-required checks (§14.5 WCAG 2.1 AA, §17.4 DoD "axe passes in both locales").
All need the dev server running (`npm run dev`) and drive a real browser via Playwright.
They seed a session in `localStorage`, so the **API does not need to be up** — these
check the shell and static screens, not data-populated tables.

| Script | What it does |
|---|---|
| `npm run verify:a11y` | axe-core over Login, Dashboard, Audit viewer, Settings — **both locales**. Light theme. |
| `THEME=dark npm run verify:a11y` | Same, dark theme. (bash/git-bash; on PowerShell: `$env:THEME='dark'; npm run verify:a11y`) |
| `npm run verify:shell` | Renders the authed shell as Employee vs System Admin, EN + AR. Confirms the §14.3 unfiltered-sidebar rule: System Admin with zero permissions still sees every nav item. |
| `npm run scan:i18n` | Counts hardcoded-Arabic lines (§14.2: every UI string must be a key). |

## Current status

- **axe: 0 violations** across light/dark × EN/AR on the four verified screens.
- **§14.3: verified** — System Admin (0 perms) sees all 11 nav items; Employee (1 perm) sees 2.
- **i18n debt: ~1,265 hardcoded-Arabic lines across 79 files.** The infrastructure is
  done (i18n wired, `dir` app-level, dual-calendar in `lib/datetime.ts`); the remaining
  work is a per-screen localization pass — a real project, not a cleanup. `scan:i18n`
  tracks it. Biggest offenders: `DocumentDetailPage`, `UsersAdmin`, `lib/users.ts`.

## Not yet covered

These run against **empty data** (API down). Table-row contrast, keyboard paths inside
populated grids, and any API-driven state are unverified. Bring the API up (see
`docs/local-dev.md`) and extend the route list in `verify-a11y.mjs` to close that gap.
