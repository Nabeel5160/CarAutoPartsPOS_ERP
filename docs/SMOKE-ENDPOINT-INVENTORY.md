# API endpoint inventory

Generated from `*Controller.cs` under `src/CarAutoParts.Api/Controllers`. Controllers that also declare `/api/v1/...` are marked **v1 dual-route**.

Non-controller health: `GET /health`, `GET /health/live`, `GET /health/ready` — anonymous.

| METHOD | Path | Controller | Auth notes |
|--------|------|------------|------------|
| GET | `/api/analytics` | AnalyticsController | Policy:Permissions.AnalyticsView |
| GET | `/api/app-config` | AppConfigController | Policy:Permissions.SettingsView |
| PUT | `/api/app-config` | AppConfigController | Policy:Permissions.SettingsManage |
| GET | `/api/app-config/public` | AppConfigController | AllowAnonymous |
| GET | `/api/approvals/pending` | ApprovalsController | Policy:Permissions.ApprovalsView; v1 dual-route |
| POST | `/api/approvals/pending/{id:int}/decide` | ApprovalsController | Policy:Permissions.ApprovalsDecide; v1 dual-route |
| GET | `/api/approvals/policies` | ApprovalsController | Policy:Permissions.ApprovalsManage; v1 dual-route |
| POST | `/api/approvals/policies` | ApprovalsController | Policy:Permissions.ApprovalsManage; v1 dual-route |
| DELETE | `/api/approvals/policies/{id:int}` | ApprovalsController | Policy:Permissions.ApprovalsManage; v1 dual-route |
| PUT | `/api/approvals/policies/{id:int}` | ApprovalsController | Policy:Permissions.ApprovalsManage; v1 dual-route |
| POST | `/api/approvals/void/journals/{id:int}` | ApprovalsController | Policy:Permissions.FinanceVoid; v1 dual-route |
| POST | `/api/approvals/void/purchase-invoices/{id:int}` | ApprovalsController | Policy:Permissions.FinanceVoid; v1 dual-route |
| POST | `/api/approvals/void/sales-invoices/{id:int}` | ApprovalsController | Policy:Permissions.FinanceVoid; v1 dual-route |
| GET | `/api/audit-logs` | AuditController | Policy:Permissions.AuditView |
| POST | `/api/auth/change-password` | AuthController | Authorize; v1 dual-route |
| POST | `/api/auth/login` | AuthController | AllowAnonymous; v1 dual-route |
| POST | `/api/auth/logout` | AuthController | Authorize; v1 dual-route |
| GET | `/api/auth/me` | AuthController | Authorize; v1 dual-route |
| POST | `/api/auth/mfa/disable` | AuthController | Authorize; v1 dual-route |
| POST | `/api/auth/mfa/enroll/begin` | AuthController | Authorize; v1 dual-route |
| POST | `/api/auth/mfa/enroll/confirm` | AuthController | Authorize; v1 dual-route |
| POST | `/api/auth/mfa/reset/{userId:int}` | AuthController | Authorize; v1 dual-route |
| GET | `/api/auth/mfa/status` | AuthController | Authorize; v1 dual-route |
| POST | `/api/auth/mfa/verify` | AuthController | AllowAnonymous; v1 dual-route |
| GET | `/api/backups` | BackupsController | Policy:Permissions.BackupView |
| POST | `/api/backups` | BackupsController | Policy:Permissions.BackupManage |
| POST | `/api/backups/restore` | BackupsController | Policy:Permissions.BackupManage |
| GET | `/api/brands` | BrandsController | Policy:Permissions.BrandsView |
| POST | `/api/brands` | BrandsController | Policy:Permissions.BrandsManage |
| DELETE | `/api/brands/{id:int}` | BrandsController | Policy:Permissions.BrandsManage |
| PUT | `/api/brands/{id:int}` | BrandsController | Policy:Permissions.BrandsManage |
| GET | `/api/categories` | CategoriesController | Policy:Permissions.CategoriesView |
| POST | `/api/categories` | CategoriesController | Policy:Permissions.CategoriesManage |
| DELETE | `/api/categories/{id:int}` | CategoriesController | Policy:Permissions.CategoriesManage |
| PUT | `/api/categories/{id:int}` | CategoriesController | Policy:Permissions.CategoriesManage |
| GET | `/api/customers` | CustomersController | Policy:Permissions.CustomersView |
| POST | `/api/customers` | CustomersController | Policy:Permissions.CustomersManage |
| DELETE | `/api/customers/{id:int}` | CustomersController | Policy:Permissions.CustomersManage |
| GET | `/api/customers/{id:int}` | CustomersController | Policy:Permissions.CustomersView |
| PUT | `/api/customers/{id:int}` | CustomersController | Policy:Permissions.CustomersManage |
| GET | `/api/customers/{id:int}/ledger` | CustomersController | Policy:Permissions.CustomersView |
| GET | `/api/dashboard` | DashboardController | Policy:Permissions.DashboardView |
| GET | `/api/enterprise/account-mappings` | EnterpriseController | Policy:Permissions.FinanceManage; v1 dual-route |
| POST | `/api/enterprise/account-mappings` | EnterpriseController | Policy:Permissions.FinanceManage; v1 dual-route |
| DELETE | `/api/enterprise/account-mappings/{id:int}` | EnterpriseController | Policy:Permissions.FinanceManage; v1 dual-route |
| PUT | `/api/enterprise/account-mappings/{id:int}` | EnterpriseController | Policy:Permissions.FinanceManage; v1 dual-route |
| GET | `/api/enterprise/aging/customers` | EnterpriseController | Policy:Permissions.FinanceView; v1 dual-route |
| GET | `/api/enterprise/aging/suppliers` | EnterpriseController | Policy:Permissions.FinanceView; v1 dual-route |
| GET | `/api/enterprise/ap-invoices` | EnterpriseController | Policy:Permissions.ApInvoiceManage; v1 dual-route |
| POST | `/api/enterprise/ap-invoices` | EnterpriseController | Policy:Permissions.ApInvoiceManage; v1 dual-route |
| POST | `/api/enterprise/ap-invoices/{id:int}/match` | EnterpriseController | Policy:Permissions.ApInvoiceManage; v1 dual-route |
| POST | `/api/enterprise/ap-invoices/{id:int}/post` | EnterpriseController | Policy:Permissions.ApInvoiceManage; v1 dual-route |
| GET | `/api/enterprise/credit-check/{customerId:int}` | EnterpriseController | Policy:Permissions.CustomersView; v1 dual-route |
| GET | `/api/enterprise/cycle-counts` | EnterpriseController | Policy:Permissions.CycleCountManage; v1 dual-route |
| POST | `/api/enterprise/cycle-counts` | EnterpriseController | Policy:Permissions.CycleCountManage; v1 dual-route |
| POST | `/api/enterprise/cycle-counts/{id:int}/complete` | EnterpriseController | Policy:Permissions.CycleCountManage; v1 dual-route |
| GET | `/api/enterprise/deliveries` | EnterpriseController | Policy:Permissions.DeliveriesManage; v1 dual-route; RequireFeature(ConfigKeys.ModSalesDeliveries) |
| POST | `/api/enterprise/deliveries` | EnterpriseController | Policy:Permissions.DeliveriesManage; v1 dual-route; RequireFeature(ConfigKeys.ModSalesDeliveries) |
| POST | `/api/enterprise/deliveries/{id:int}/confirm-pick` | EnterpriseController | Policy:Permissions.DeliveriesManage; v1 dual-route; RequireFeature(ConfigKeys.ModSalesDeliveries) |
| POST | `/api/enterprise/deliveries/{id:int}/create-invoice` | EnterpriseController | Policy:Permissions.SalesView; v1 dual-route; RequireFeature(ConfigKeys.ModSalesInvoices) |
| POST | `/api/enterprise/deliveries/{id:int}/ship` | EnterpriseController | Policy:Permissions.DeliveriesManage; v1 dual-route; RequireFeature(ConfigKeys.ModSalesDeliveries) |
| GET | `/api/enterprise/fbr/metrics` | EnterpriseController | Policy:Permissions.PosCheckout; v1 dual-route; RequireFeature(ConfigKeys.ModSalesFbr) |
| GET | `/api/enterprise/fbr/submissions` | EnterpriseController | Policy:Permissions.PosCheckout; v1 dual-route; RequireFeature(ConfigKeys.ModSalesFbr) |
| GET | `/api/enterprise/grn` | EnterpriseController | Policy:Permissions.GrnManage; v1 dual-route |
| POST | `/api/enterprise/grn` | EnterpriseController | Policy:Permissions.GrnManage; v1 dual-route |
| POST | `/api/enterprise/grn/{id:int}/post` | EnterpriseController | Policy:Permissions.GrnManage; v1 dual-route |
| POST | `/api/enterprise/grn/{id:int}/release-qc` | EnterpriseController | Policy:Permissions.GrnManage; v1 dual-route |
| GET | `/api/enterprise/kits` | EnterpriseController | Policy:Permissions.KitsManage; v1 dual-route |
| POST | `/api/enterprise/kits` | EnterpriseController | Policy:Permissions.KitsManage; v1 dual-route |
| POST | `/api/enterprise/payments/customer-receipt` | EnterpriseController | Policy:Permissions.FinancePost; v1 dual-route |
| POST | `/api/enterprise/payments/supplier-payment` | EnterpriseController | Policy:Permissions.FinancePost; v1 dual-route |
| GET | `/api/enterprise/price` | EnterpriseController | Policy:Permissions.SalesView; v1 dual-route |
| GET | `/api/enterprise/price-lists` | EnterpriseController | Policy:Permissions.PriceListsManage; v1 dual-route |
| POST | `/api/enterprise/price-lists` | EnterpriseController | Policy:Permissions.PriceListsManage; v1 dual-route |
| PUT | `/api/enterprise/price-lists/{id:int}/items` | EnterpriseController | Policy:Permissions.PriceListsManage; v1 dual-route |
| GET | `/api/enterprise/quotations` | EnterpriseController | Policy:Permissions.QuotationsManage; v1 dual-route; RequireFeature(ConfigKeys.ModSalesQuotations) |
| POST | `/api/enterprise/quotations` | EnterpriseController | Policy:Permissions.QuotationsManage; v1 dual-route; RequireFeature(ConfigKeys.ModSalesQuotations) |
| POST | `/api/enterprise/quotations/{id:int}/convert` | EnterpriseController | Policy:Permissions.QuotationsManage; v1 dual-route; RequireFeature(ConfigKeys.ModSalesQuotations) |
| GET | `/api/enterprise/reports/balance-sheet` | EnterpriseController | Policy:Permissions.FinanceView; v1 dual-route |
| GET | `/api/enterprise/reports/profit-loss` | EnterpriseController | Policy:Permissions.FinanceView; v1 dual-route |
| GET | `/api/enterprise/reports/trial-balance` | EnterpriseController | Policy:Permissions.FinanceView; v1 dual-route |
| GET | `/api/enterprise/reservations` | EnterpriseController | Policy:Permissions.InventoryAdjust; v1 dual-route |
| POST | `/api/enterprise/reservations` | EnterpriseController | Policy:Permissions.InventoryAdjust; v1 dual-route |
| POST | `/api/enterprise/reservations/{id:int}/release` | EnterpriseController | Policy:Permissions.InventoryAdjust; v1 dual-route |
| GET | `/api/enterprise/sales-orders` | EnterpriseController | Policy:Permissions.SalesView; v1 dual-route; RequireFeature(ConfigKeys.ModSalesOrders) |
| POST | `/api/enterprise/sales-orders/{id:int}/create-delivery` | EnterpriseController | Policy:Permissions.DeliveriesManage; v1 dual-route; RequireFeature(ConfigKeys.ModSalesDeliveries) |
| POST | `/api/enterprise/sales-orders/{id:int}/create-invoice` | EnterpriseController | Policy:Permissions.SalesView; v1 dual-route; RequireFeature(ConfigKeys.ModSalesInvoices) |
| GET | `/api/enterprise/supersessions` | EnterpriseController | Policy:Permissions.KitsManage; v1 dual-route |
| POST | `/api/enterprise/supersessions` | EnterpriseController | Policy:Permissions.KitsManage; v1 dual-route |
| POST | `/api/fbr/invoices` | FbrController | Policy:Permissions.PosCheckout |
| GET | `/api/finance/bank-statements` | FinanceController | Policy:Permissions.FinanceView; v1 dual-route |
| POST | `/api/finance/bank-statements` | FinanceController | Policy:Permissions.FinanceManage; v1 dual-route |
| POST | `/api/finance/bank-statements/{id:int}/lines` | FinanceController | Policy:Permissions.FinanceManage; v1 dual-route |
| GET | `/api/finance/bank-statements/{id:int}/report` | FinanceController | Policy:Permissions.FinanceView; v1 dual-route |
| POST | `/api/finance/bank-statements/lines/{lineId:int}/match` | FinanceController | Policy:Permissions.FinancePost; v1 dual-route |
| POST | `/api/finance/bank-statements/lines/{lineId:int}/unclear` | FinanceController | Policy:Permissions.FinanceManage; v1 dual-route |
| GET | `/api/finance/bank-statements/uncleared-gl` | FinanceController | Policy:Permissions.FinanceView; v1 dual-route |
| GET | `/api/finance/coa` | FinanceController | Policy:Permissions.FinanceView; v1 dual-route |
| POST | `/api/finance/coa` | FinanceController | Policy:Permissions.FinanceManage; v1 dual-route |
| GET | `/api/finance/companies` | FinanceController | Policy:Permissions.PlatformView; v1 dual-route |
| GET | `/api/finance/companies/{companyId:int}/branches` | FinanceController | Policy:Permissions.PlatformView; v1 dual-route |
| GET | `/api/finance/journals` | FinanceController | Policy:Permissions.FinanceView; v1 dual-route |
| POST | `/api/finance/journals` | FinanceController | Policy:Permissions.FinanceManage; v1 dual-route |
| POST | `/api/finance/journals/{id:int}/post` | FinanceController | Policy:Permissions.FinancePost; v1 dual-route |
| GET | `/api/finance/number-sequences/next` | FinanceController | Policy:Permissions.FinanceView; v1 dual-route |
| GET | `/api/finance/opening-balances` | FinanceController | Policy:Permissions.FinanceView; v1 dual-route |
| POST | `/api/finance/opening-balances` | FinanceController | Policy:Permissions.FinanceManage; v1 dual-route |
| GET | `/api/finance/periods` | FinanceController | Policy:Permissions.FinanceView; v1 dual-route |
| POST | `/api/finance/periods/{id:int}/close` | FinanceController | Policy:Permissions.FinancePost; v1 dual-route |
| GET | `/api/finance/periods/{id:int}/close-checklist` | FinanceController | Policy:Permissions.FinanceView; v1 dual-route |
| POST | `/api/finance/periods/{id:int}/reopen` | FinanceController | Policy:Permissions.FinancePost; v1 dual-route |
| GET | `/api/inventory` | InventoryController | Policy:Permissions.InventoryView |
| POST | `/api/inventory/adjust` | InventoryController | Policy:Permissions.InventoryAdjust |
| GET | `/api/inventory/alerts/low-stock` | InventoryController | Policy:Permissions.InventoryView |
| GET | `/api/inventory/alerts/overstock` | InventoryController | Policy:Permissions.InventoryView |
| GET | `/api/inventory/atp` | InventoryController | Policy:Permissions.InventoryView |
| POST | `/api/inventory/deduct` | InventoryController | Policy:Permissions.InventoryAdjust |
| GET | `/api/inventory/movements` | InventoryController | Policy:Permissions.InventoryView |
| POST | `/api/inventory/receive` | InventoryController | Policy:Permissions.InventoryReceive |
| POST | `/api/inventory/return-stock` | InventoryController | Policy:Permissions.InventoryAdjust |
| GET | `/api/inventory/value` | InventoryController | Policy:Permissions.InventoryView |
| GET | `/api/notifications` | NotificationsController | Authorize |
| POST | `/api/notifications` | NotificationsController | Authorize |
| POST | `/api/notifications/{id:int}/read` | NotificationsController | Authorize |
| GET | `/api/notifications/unread-count` | NotificationsController | Authorize |
| POST | `/api/onboarding/complete` | OnboardingController | Policy:Permissions.SettingsManage; v1 dual-route |
| GET | `/api/onboarding/status` | OnboardingController | Policy:Permissions.SettingsView; v1 dual-route |
| POST | `/api/pos/checkout` | PosController | Policy:Permissions.PosCheckout; v1 dual-route |
| GET | `/api/pos/fitment-options` | PosController | Policy:Permissions.PosCheckout; v1 dual-route |
| GET | `/api/pos/holds` | PosController | Policy:Permissions.PosHold; v1 dual-route |
| POST | `/api/pos/holds` | PosController | Policy:Permissions.PosHold; v1 dual-route |
| POST | `/api/pos/holds/{id:int}/discard` | PosController | Policy:Permissions.PosHold; v1 dual-route |
| POST | `/api/pos/holds/{id:int}/recall` | PosController | Policy:Permissions.PosHold; v1 dual-route |
| GET | `/api/pos/products` | PosController | Policy:Permissions.PosCheckout; v1 dual-route |
| GET | `/api/pos/receipts/{invoiceId:int}` | PosController | Policy:Permissions.PosCheckout; v1 dual-route |
| POST | `/api/pos/shifts/{id:int}/close` | PosController | Policy:Permissions.PosShift; v1 dual-route |
| GET | `/api/pos/shifts/{id:int}/safe-drops` | PosController | Policy:Permissions.PosShift; v1 dual-route |
| POST | `/api/pos/shifts/{id:int}/safe-drops` | PosController | Policy:Permissions.PosShift; v1 dual-route |
| GET | `/api/pos/shifts/{id:int}/x-report` | PosController | Policy:Permissions.PosShift; v1 dual-route |
| GET | `/api/pos/shifts/{id:int}/z-report` | PosController | Policy:Permissions.PosShift; v1 dual-route |
| GET | `/api/pos/shifts/current` | PosController | Policy:Permissions.PosShift; v1 dual-route |
| POST | `/api/pos/shifts/open` | PosController | Policy:Permissions.PosShift; v1 dual-route |
| GET | `/api/pos/shifts/x-report` | PosController | Policy:Permissions.PosShift; v1 dual-route |
| GET | `/api/pos/tills` | PosController | Policy:Permissions.PosShift; v1 dual-route |
| POST | `/api/pos/tills` | PosController | Policy:Permissions.SettingsManage; v1 dual-route |
| GET | `/api/products` | ProductsController | Policy:Permissions.ProductsView |
| POST | `/api/products` | ProductsController | Policy:Permissions.ProductsCreate |
| DELETE | `/api/products/{id:int}` | ProductsController | Policy:Permissions.ProductsDelete |
| GET | `/api/products/{id:int}` | ProductsController | Policy:Permissions.ProductsView |
| PUT | `/api/products/{id:int}` | ProductsController | Policy:Permissions.ProductsUpdate |
| GET | `/api/products/export` | ProductsController | Policy:Permissions.ProductsExport |
| GET | `/api/products/fitment-options` | ProductsController | Policy:Permissions.ProductsView |
| POST | `/api/products/import` | ProductsController | Policy:Permissions.ProductsImport |
| POST | `/api/products/import-oem-fitment` | ProductsController | Policy:Permissions.ProductsImport |
| GET | `/api/purchase-orders` | PurchaseOrdersController | Policy:Permissions.PurchasesView |
| POST | `/api/purchase-orders` | PurchaseOrdersController | Policy:Permissions.PurchasesCreate |
| GET | `/api/purchase-orders/{id:int}` | PurchaseOrdersController | Policy:Permissions.PurchasesView |
| PUT | `/api/purchase-orders/{id:int}` | PurchaseOrdersController | Policy:Permissions.PurchasesCreate |
| POST | `/api/purchase-orders/{id:int}/approve` | PurchaseOrdersController | Policy:Permissions.PurchasesApprove |
| POST | `/api/purchase-orders/{id:int}/cancel` | PurchaseOrdersController | Policy:Permissions.PurchasesCreate |
| POST | `/api/purchase-orders/{id:int}/receive` | PurchaseOrdersController | Policy:Permissions.PurchasesReceive |
| GET | `/api/purchase-requisitions` | PurchaseRequisitionsController | Policy:Permissions.PurchasesRequisition; v1 dual-route |
| POST | `/api/purchase-requisitions` | PurchaseRequisitionsController | Policy:Permissions.PurchasesRequisition; v1 dual-route |
| GET | `/api/purchase-requisitions/{id:int}` | PurchaseRequisitionsController | Policy:Permissions.PurchasesRequisition; v1 dual-route |
| POST | `/api/purchase-requisitions/{id:int}/approve` | PurchaseRequisitionsController | Policy:Permissions.PurchasesApprove; v1 dual-route |
| POST | `/api/purchase-requisitions/{id:int}/convert-to-po` | PurchaseRequisitionsController | Policy:Permissions.PurchasesCreate; v1 dual-route |
| POST | `/api/purchase-requisitions/{id:int}/reject` | PurchaseRequisitionsController | Policy:Permissions.PurchasesApprove; v1 dual-route |
| POST | `/api/purchase-requisitions/{id:int}/submit` | PurchaseRequisitionsController | Policy:Permissions.PurchasesRequisition; v1 dual-route |
| POST | `/api/reorder/create-pr` | ReorderController | Policy:Permissions.PurchasesRequisition; v1 dual-route |
| GET | `/api/reorder/suggestions` | ReorderController | Policy:Permissions.PurchasesRequisition; v1 dual-route |
| GET | `/api/reports/aging` | ReportsController | Policy:Permissions.ReportsExport |
| GET | `/api/reports/analytics-export` | ReportsController | Policy:Permissions.ReportsExport |
| GET | `/api/reports/daily-sales` | ReportsController | Policy:Permissions.ReportsExport |
| GET | `/api/reports/fbr` | ReportsController | Policy:Permissions.ReportsExport |
| GET | `/api/reports/inventory` | ReportsController | Policy:Permissions.ReportsExport |
| GET | `/api/reports/movements` | ReportsController | Policy:Permissions.ReportsExport |
| GET | `/api/reports/profit` | ReportsController | Policy:Permissions.ReportsExport |
| GET | `/api/reports/profit-dim` | ReportsController | Policy:Permissions.ReportsExport |
| GET | `/api/reports/purchases` | ReportsController | Policy:Permissions.ReportsExport |
| GET | `/api/reports/purchasing-pipeline` | ReportsController | Policy:Permissions.ReportsExport |
| GET | `/api/reports/sales` | ReportsController | Policy:Permissions.ReportsExport |
| GET | `/api/reports/sales-dim` | ReportsController | Policy:Permissions.ReportsExport |
| GET | `/api/reports/sales-returns` | ReportsController | Policy:Permissions.ReportsExport |
| GET | `/api/reports/sales-staff` | ReportsController | Policy:Permissions.ReportsExport |
| GET | `/api/reports/sku-margin` | ReportsController | Policy:Permissions.ReportsExport |
| GET | `/api/reports/stock-aging` | ReportsController | Policy:Permissions.ReportsExport |
| GET | `/api/reports/tax` | ReportsController | Policy:Permissions.ReportsExport |
| GET | `/api/reports/z-shifts` | ReportsController | Policy:Permissions.ReportsExport |
| POST | `/api/returns/purchases` | ReturnsController | Policy:Permissions.ReturnsManage |
| POST | `/api/returns/purchases/{id:int}/apply` | ReturnsController | Policy:Permissions.ReturnsManage |
| GET | `/api/returns/sales` | ReturnsController | Policy:Permissions.ReturnsManage |
| POST | `/api/returns/sales` | ReturnsController | Policy:Permissions.ReturnsManage |
| POST | `/api/returns/sales/{id:int}/apply` | ReturnsController | Policy:Permissions.ReturnsManage |
| GET | `/api/roles` | RolesController | Policy:Permissions.UsersView |
| GET | `/api/sales/invoices` | SalesController | Policy:Permissions.SalesView |
| GET | `/api/sales/invoices/{id:int}` | SalesController | Policy:Permissions.SalesView |
| GET | `/api/sales/orders` | SalesController | Policy:Permissions.SalesView |
| GET | `/api/serial-numbers` | SerialNumbersController | Policy:Permissions.SerialNumbersView |
| POST | `/api/serial-numbers` | SerialNumbersController | Policy:Permissions.SerialNumbersManage |
| GET | `/api/serial-numbers/{id:int}/history` | SerialNumbersController | Policy:Permissions.SerialNumbersView |
| GET | `/api/settings` | SettingsController | Policy:Permissions.SettingsView |
| PUT | `/api/settings` | SettingsController | Policy:Permissions.SettingsManage |
| GET | `/api/suppliers` | SuppliersController | Policy:Permissions.SuppliersView |
| POST | `/api/suppliers` | SuppliersController | Policy:Permissions.SuppliersManage |
| DELETE | `/api/suppliers/{id:int}` | SuppliersController | Policy:Permissions.SuppliersManage |
| GET | `/api/suppliers/{id:int}` | SuppliersController | Policy:Permissions.SuppliersView |
| PUT | `/api/suppliers/{id:int}` | SuppliersController | Policy:Permissions.SuppliersManage |
| GET | `/api/suppliers/{id:int}/ledger` | SuppliersController | Policy:Permissions.SuppliersView |
| GET | `/api/transfers` | TransfersController | Policy:Permissions.TransfersView |
| POST | `/api/transfers` | TransfersController | Policy:Permissions.TransfersCreate |
| GET | `/api/transfers/{id:int}` | TransfersController | Policy:Permissions.TransfersView |
| POST | `/api/transfers/{id:int}/approve` | TransfersController | Policy:Permissions.TransfersApprove |
| POST | `/api/transfers/{id:int}/complete` | TransfersController | Policy:Permissions.TransfersApprove |
| POST | `/api/transfers/{id:int}/confirm-pick` | TransfersController | Policy:Permissions.TransfersApprove |
| POST | `/api/transfers/{id:int}/ship` | TransfersController | Policy:Permissions.TransfersApprove |
| GET | `/api/users` | UsersController | Policy:Permissions.UsersView |
| POST | `/api/users` | UsersController | Policy:Permissions.UsersManage |
| DELETE | `/api/users/{id:int}` | UsersController | Policy:Permissions.UsersManage |
| PUT | `/api/users/{id:int}` | UsersController | Policy:Permissions.UsersManage |
| GET | `/api/warehouses/{warehouseId:int}/locations` | WarehouseLocationsController | Policy:Permissions.WarehousesView |
| POST | `/api/warehouses/{warehouseId:int}/locations` | WarehouseLocationsController | Policy:Permissions.WarehousesManage |
| DELETE | `/api/warehouses/{warehouseId:int}/locations/{locationId:int}` | WarehouseLocationsController | Policy:Permissions.WarehousesManage |
| PUT | `/api/warehouses/{warehouseId:int}/locations/{locationId:int}` | WarehouseLocationsController | Policy:Permissions.WarehousesManage |
| GET | `/api/warehouses/{warehouseId:int}/locations/balances` | WarehouseLocationsController | Policy:Permissions.WarehousesView |
| GET | `/api/warehouses` | WarehousesController | Policy:Permissions.WarehousesView |
| POST | `/api/warehouses` | WarehousesController | Policy:Permissions.WarehousesManage |
| DELETE | `/api/warehouses/{id:int}` | WarehousesController | Policy:Permissions.WarehousesManage |
| PUT | `/api/warehouses/{id:int}` | WarehousesController | Policy:Permissions.WarehousesManage |

**Total endpoints listed:** 228

