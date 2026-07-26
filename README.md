# POS Terminal (WPF) with FBR Digital Invoicing

A Windows desktop Point-of-Sale screen built with **WPF (.NET 8, MVVM)** that posts
each sale to Pakistan's **FBR Digital Invoicing (DI)** REST API and prints the
returned FBR Invoice Number as a QR code on the receipt panel.

## Features

- Touch-friendly product grid with live search.
- Cart with quantity +/-, per-line tax, subtotal / sales-tax / grand-total.
- Buyer name + optional NTN/CNIC (registered vs. unregistered handling).
- Expandable **FBR Buyer & Tax Details** panel:
  - Registration type (Registered / Unregistered)
  - Buyer province & address
  - Sandbox scenario ID (`SN001`–`SN010`) when `UseSandbox` is true
  - SRO schedule no. & SRO item serial no.
  - Sale type (standard rate, exempt, 3rd schedule, etc.)
- One-tap **Checkout & Send to FBR** which:
  - builds the FBR DI JSON payload from the cart,
  - posts it with a Bearer token to the sandbox or production endpoint,
  - shows the returned FBR Invoice Number and a verification QR code.
- **Receipt printing**: after a successful checkout the Windows print dialog opens
  automatically; use **Print Receipt** to reprint the last sale (includes QR + FBR IRN).
- **Offline/Stub mode**: if no `BearerToken` is configured the app generates a
  `TEST-...` invoice number locally so the whole flow stays testable without credentials.

## Project structure

```
PosWpf/
  App.xaml(.cs)            App startup, config loading, styles & palette
  MainWindow.xaml(.cs)     The POS screen (3-pane layout)
  appsettings.json         FBR endpoints + token + seller profile
  Common/                  MVVM base classes, RelayCommand, value converters
  Models/                  Product, CartItem, AppSettings
  Models/Fbr/              FBR request/response DTOs (JSON schema)
  Services/                FbrService (API client), ProductCatalog, InvoiceBuilder, QrCodeHelper
  ViewModels/              MainViewModel (all POS logic)
```

## Configuration

Edit `PosWpf/appsettings.json`:

| Setting | Meaning |
| --- | --- |
| `Fbr.UseSandbox` | `true` posts to the `_sb` sandbox endpoint, `false` to production. |
| `Fbr.BearerToken` | Your FBR PRAL access token. **Leave blank to run in stub mode.** |
| `Fbr.TimeoutSeconds` | HTTP timeout. |
| `Seller.*` | Your registered NTN/CNIC, business name, province, address, POS id. |

The DI endpoints used:

- Sandbox: `https://gw.fbr.gov.pk/di_data/v1/di/postinvoicedata_sb`
- Production: `https://gw.fbr.gov.pk/di_data/v1/di/postinvoicedata`

## Run

```bash
cd pos-wpf
dotnet run --project PosWpf/PosWpf.csproj
```

> Requires the .NET 8 SDK on Windows (WPF is Windows-only).

## How the FBR call works

`Services/InvoiceBuilder.cs` maps the cart into `FbrInvoiceRequest`, then
`Services/FbrService.cs` serializes it, attaches `Authorization: Bearer <token>`,
POSTs to the configured endpoint, and parses `FbrInvoiceResponse`. A success
requires `validationResponse.statusCode == "00"` (or `status == "Valid"`).

To go live: paste your sandbox token into `appsettings.json`, set
`UseSandbox: true`, and run a test sale. Once validated by FBR, switch
`UseSandbox` to `false` with your production token.
