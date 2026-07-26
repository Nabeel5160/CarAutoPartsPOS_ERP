# Car Auto Parts Web (Blazor WASM)

Blazor WebAssembly UI for the Car Auto Parts ERP API — Bootstrap 5, CSS depth motion, and customizable themes.

## Prerequisites

- .NET 8 SDK
- API running at `http://localhost:5280` (see `CarAutoParts.Api`)

## Configure

Edit [`wwwroot/appsettings.json`](wwwroot/appsettings.json):

```json
{ "ApiBaseUrl": "http://localhost:5280" }
```

## Run

```bash
# Terminal 1 — API
dotnet run --project src/CarAutoParts.Api --launch-profile http

# Terminal 2 — Blazor UI
dotnet run --project src/CarAutoParts.Web --launch-profile http
```

Open **http://localhost:5156**

Login: `admin` / `admin123`

## Features

- JWT auth against `/api/auth/*`
- Permission-gated sidebar (same codes as WPF)
- Dark/light modes + Amber/Cyan/Emerald/Rose accents (localStorage)
- Pages for every API module: catalog, inventory, POS, purchases, sales, reports, users, backup, FBR, barcodes, etc.
