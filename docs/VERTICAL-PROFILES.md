# Vertical Profiles (Phase 11)

Per-install business profile: pick a vertical preset, then override modules, fields, behaviors, branding, and labels from **Settings → Business profile**.

## Presets

| Key | Display | Defaults |
|-----|---------|----------|
| `auto-parts` | Auto Parts | All modules on; OEM/part/HS/fitment visible; FBR + fitment search + supersession on |
| `bike-parts` | Bike Parts | Same as auto with bike-oriented fitment/OEM labels |
| `general-retail` | General Retail | Kits/serials/requisitions/FBR off; OEM/part/HS/fitment hidden; FBR + fitment search + supersession off |

Fresh installs default to `auto-parts` (no behavior change for existing deployments). Override at seed time with env `CAP_VERTICAL`.

## Key families

### Modules (`scope=module`)

Boolean. Gate nav items and (where applied) API via `RequireFeature`.

Examples: `catalog.kits`, `inventory.serials`, `purchasing.requisitions`, `sales.fbr`, `insights.analytics`, …

### Fields (`scope=field`)

JSON value: `{"visible":true,"required":false,"label":"OEM number"}`.

Keys: `product.oem`, `product.partNumber`, `product.hsCode`, `product.fitment`, `customer.ntn`.

### Behaviors (`scope=behavior`)

| Key | Effect |
|-----|--------|
| `fbr.enabled` | POS posts FBR after checkout; FBR nav/API available |
| `tax.enabled` | Line tax calculated (else 0) |
| `pos.fitmentSearch` | POS search includes make/model/year |
| `pos.supersession` | POS search follows product supersessions |
| `currency` | Display currency code (default PKR) |
| `decimals` | Money decimal places (default 2) |

### Brand (`scope=brand`)

`appName`, `shortName`, `accentWord`, `logoUrl`, `theme`, `accent` — login splash, sidebar, document title, receipt header.

### Labels (`scope=label`, culture `en`/`ur`)

Override any `LocaleService` key (e.g. `POS_SearchPlaceholder`, `POS_Checkout`, `Nav_POS`).

## APIs

| Method | Path | Auth |
|--------|------|------|
| GET | `/api/app-config/public` | Anonymous — branding + default labels only (no NTN/FBR token) |
| GET | `/api/app-config` | `settings.view` |
| PUT | `/api/app-config` | `settings.manage` — body `AppConfigUpdateRequest` |

`ApplyPresetDefaults: true` clears DB overrides and sets `CompanySettings.VerticalKey`.

## Resolution order

1. Preset defaults from `VerticalProfiles` for `CompanySettings.VerticalKey`
2. Overlay non-deleted `AppConfigEntries`
3. Branding `appName` / `logoUrl` prefer company settings when set

Cached 5 minutes in `IMemoryCache`; invalidated on update.
