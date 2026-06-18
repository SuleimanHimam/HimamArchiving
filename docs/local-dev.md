# Local development environment

Everything below is installed and running on this machine.

## MySQL 8.4 (the project database)

| Item | Value |
|---|---|
| Service | `MySQL84` (Windows service, auto-start) |
| Binaries | `C:\Program Files\MySQL\MySQL Server 8.4\bin` |
| Data dir | `C:\ProgramData\MySQL\MySQL Server 8.4\Data` |
| Config | `C:\ProgramData\MySQL\MySQL Server 8.4\my.ini` |
| Host / port | `localhost` : `3306` |
| Database | `archiving_db` (utf8mb4 / utf8mb4_unicode_ci) — **31 tables created** |

**Credentials (local dev only):**

| User | Password | Scope |
|---|---|---|
| `root` | `<your-root-password>` | full server |
| `archiver` | `<your-db-password>` | `archiving_db` only (used by the API) |

> ⚠️ Dev passwords. Change them (and move secrets to user-secrets / env vars) before any shared or production use.

Service control:
```powershell
Start-Service MySQL84 ; Stop-Service MySQL84 ; Get-Service MySQL84
```
CLI:
```bash
"/c/Program Files/MySQL/MySQL Server 8.4/bin/mysql.exe" -u archiver -p<your-db-password> archiving_db
```

## phpMyAdmin

| Item | Value |
|---|---|
| URL | http://localhost:8080 |
| Location | `tools/phpmyadmin` |
| Served by | PHP 8.2 built-in server (no Apache) |
| Login | cookie auth — use `root` / `archiver` MySQL credentials above |

Start it (if not running):
```bash
cd /e/Projects/Archiving/tools/phpmyadmin
php -d error_reporting=0 -S 127.0.0.1:8080 -t .
```

## Backend API

```bash
dotnet run --project backend/src/Archiving.Api
# Swagger at https://localhost:<port>/swagger  (port shown on startup)
```
Connection string lives in `backend/src/Archiving.Api/appsettings.json` → `ConnectionStrings:Default`.

## EF Core migrations

```bash
export PATH="$PATH:$HOME/.dotnet/tools"
cd backend
dotnet ef migrations add <Name> --project src/Archiving.Infrastructure --startup-project src/Archiving.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/Archiving.Infrastructure --startup-project src/Archiving.Api
```

## Frontend

```bash
cd frontend && npm run dev      # http://localhost:5173
```
