# COA seed / go-live notes

Demo company **CAP — Car Auto Parts** seeds a Pakistan-friendly chart:

| Code | Name | Type |
|------|------|------|
| 1000 | Assets | header |
| 1100 | Cash in Hand | Asset |
| 1110 | Bank | Asset |
| 1200 | Accounts Receivable | Asset |
| 1300 | Inventory Asset | Asset |
| 2000 | Liabilities | header |
| 2100 | Accounts Payable | Liability |
| 2200 | Sales Tax Payable | Liability |
| 3000 | Equity | header |
| 3100 | Owner Equity | Equity |
| 4000 | Revenue | header |
| 4100 | Sales Revenue | Revenue |
| 5000 | COGS | header |
| 5100 | COGS | CostOfGoods |
| 6000 | Expenses | header |
| 6100 | Operating Expense | Expense |

Account mappings link SalesInvoice / PurchaseInvoice / Grn / Payment document keys to these codes.

Fiscal year follows July–June (Pakistan typical). Twelve monthly periods are opened at seed.
