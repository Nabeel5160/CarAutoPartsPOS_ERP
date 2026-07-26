# Deployment

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server LocalDB (included with Visual Studio) or a SQL Server instance
- Windows 10/11 for the WPF desktop client

## Configuration

Connection strings and logging are in `src/CarAutoParts.Presentation/appsettings.json`. The default profile uses LocalDB:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=CarAutoPartsERP;Trusted_Connection=True;TrustServerCertificate=True"
}
```

Update `DefaultConnection` if you use a named SQL Server instance.

## Database

From the repository root, apply migrations (first run or after schema changes):

```bash
dotnet ef database update --project src/CarAutoParts.Infrastructure/CarAutoParts.Infrastructure.csproj --startup-project src/CarAutoParts.Presentation/CarAutoParts.Presentation.csproj
```

The app also seeds an admin user and reference data on first startup when the database is empty.

**Default login:** `admin` / `admin123`

## Run (development)

```bash
dotnet run --project src/CarAutoParts.Presentation/CarAutoParts.Presentation.csproj
```

Or build then run the assembly directly (Debug build disables the apphost for Smart App Control compatibility):

```bash
dotnet build src/CarAutoParts.Presentation/CarAutoParts.Presentation.csproj -c Debug
dotnet src/CarAutoParts.Presentation/bin/Debug/net8.0-windows/CarAutoParts.Presentation.dll
```

## Build (release)

Self-contained single-file publish for Windows x64:

```bash
dotnet publish src/CarAutoParts.Presentation/CarAutoParts.Presentation.csproj -c Release
```

Output: `src/CarAutoParts.Presentation/bin/Release/net8.0-windows/win-x64/publish/`

## Tests

```bash
dotnet test tests/CarAutoParts.Application.Tests/CarAutoParts.Application.Tests.csproj
```

## Backups

Database backups created from the **Backup** module are stored under:

`%LocalAppData%\CarAutoPartsERP\Backups`

Restore requires the `backup.manage` permission and replaces the current database — restart the application after a successful restore.
