# Localization readiness (en / ur)

Resource keys are prepared for Blazor and shared UI strings. Wire `IStringLocalizer` when enabling full culture switching.

## Resource key catalog

| Key | English (en) | Urdu (ur) placeholder |
|-----|--------------|------------------------|
| `App.Title` | Car Auto Parts | کار آٹو پارٹس |
| `Nav.Dashboard` | Dashboard | ڈیش بورڈ |
| `Nav.Products` | Products | مصنوعات |
| `Nav.Inventory` | Inventory | انوینٹری |
| `Nav.Purchases` | Purchases | خریداری |
| `Nav.Sales` | Sales | فروخت |
| `Nav.Finance` | Finance | مالیات |
| `Nav.Company` | Company | کمپنی |
| `Nav.ChartOfAccounts` | Chart of Accounts | چارٹ آف اکاؤنٹس |
| `Nav.Journals` | Journals | جرنلز |
| `Nav.Periods` | Periods | ادوار |
| `Finance.PostJournal` | Post journal | جرنل پوسٹ کریں |
| `Finance.PeriodClosed` | Accounting period is closed | اکاؤنٹنگ مدت بند ہے |
| `Pos.Checkout` | Checkout | چیک آؤٹ |
| `Pos.Idempotency` | Duplicate checkout blocked | ڈپلیکیٹ چیک آؤٹ مسدود |
| `Common.Save` | Save | محفوظ کریں |
| `Common.Cancel` | Cancel | منسوخ |
| `Common.Search` | Search | تلاش |
| `Error.Unauthorized` | You are not authorized | آپ مجاز نہیں ہیں |
| `Error.Validation` | Please correct the highlighted fields | براہ کرم نشاندہی شدہ خانے درست کریں |

## Suggested files

- `src/CarAutoParts.Web/Resources/SharedResources.en.resx`
- `src/CarAutoParts.Web/Resources/SharedResources.ur.resx`

## Culture switch

1. Store preferred culture in local storage (`en` / `ur`)
2. Set `CultureInfo.DefaultThreadCurrentUICulture` at WASM startup
3. Prefer `dir="rtl"` on `<html>` when culture is `ur`
