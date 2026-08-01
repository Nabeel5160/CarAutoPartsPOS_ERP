using CarAutoParts.Api.Filters;
using CarAutoParts.Application.Common;
using CarAutoParts.Application.Config;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.Enterprise;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/enterprise")]
[Route("api/v1/enterprise")]
public class EnterpriseController : ApiControllerBase
{
    private readonly IEnterpriseInventoryService _inventory;
    private readonly IEnterprisePurchaseService _purchase;
    private readonly IEnterpriseSalesService _sales;
    private readonly IMasterDataService _master;
    private readonly IFinancialReportService _reports;
    private readonly IFbrOutboxService _fbrOutbox;
    private readonly IAccountMappingService _accountMappings;
    private readonly IPaymentPostingService _payments;

    public EnterpriseController(
        IEnterpriseInventoryService inventory,
        IEnterprisePurchaseService purchase,
        IEnterpriseSalesService sales,
        IMasterDataService master,
        IFinancialReportService reports,
        IFbrOutboxService fbrOutbox,
        IAccountMappingService accountMappings,
        IPaymentPostingService payments)
    {
        _inventory = inventory;
        _purchase = purchase;
        _sales = sales;
        _master = master;
        _reports = reports;
        _fbrOutbox = fbrOutbox;
        _accountMappings = accountMappings;
        _payments = payments;
    }

    [HttpGet("grn")]
    [Authorize(Policy = Permissions.GrnManage)]
    public async Task<IActionResult> GetGrns([FromQuery] QuerySpec query, CancellationToken ct) =>
        Ok(await _inventory.GetGrnsAsync(query, ct));

    [HttpPost("reservations")]
    [Authorize(Policy = Permissions.InventoryAdjust)]
    public async Task<IActionResult> Reserve([FromBody] ReserveStockRequest request, CancellationToken ct) =>
        FromResult(await _inventory.ReserveStockAsync(request, ct));

    [HttpPost("reservations/{id:int}/release")]
    [Authorize(Policy = Permissions.InventoryAdjust)]
    public async Task<IActionResult> Release(int id, CancellationToken ct) =>
        FromResult(await _inventory.ReleaseReservationAsync(id, ct));

    [HttpGet("reservations")]
    [Authorize(Policy = Permissions.InventoryAdjust)]
    public async Task<IActionResult> GetReservations(CancellationToken ct) =>
        Ok(await _inventory.GetReservationsAsync(ct));

    [HttpPost("grn")]
    [Authorize(Policy = Permissions.GrnManage)]
    public async Task<IActionResult> CreateGrn([FromBody] CreateGrnRequest request, CancellationToken ct) =>
        FromResult(await _inventory.CreateGrnAsync(request, ct));

    [HttpPost("grn/{id:int}/post")]
    [Authorize(Policy = Permissions.GrnManage)]
    public async Task<IActionResult> PostGrn(int id, CancellationToken ct) =>
        FromResult(await _inventory.PostGrnAsync(id, ct));

    [HttpPost("grn/{id:int}/release-qc")]
    [Authorize(Policy = Permissions.GrnManage)]
    public async Task<IActionResult> ReleaseQc(int id, CancellationToken ct) =>
        FromResult(await _inventory.ReleaseQcAsync(id, ct));

    [HttpPost("cycle-counts")]
    [Authorize(Policy = Permissions.CycleCountManage)]
    public async Task<IActionResult> CreateCycleCount([FromBody] CreateCycleCountRequest request, CancellationToken ct) =>
        FromResult(await _inventory.CreateCycleCountAsync(request, ct));

    [HttpPost("cycle-counts/{id:int}/complete")]
    [Authorize(Policy = Permissions.CycleCountManage)]
    public async Task<IActionResult> CompleteCycleCount(int id, CancellationToken ct) =>
        FromResult(await _inventory.CompleteCycleCountAsync(id, ct));

    [HttpGet("cycle-counts")]
    [Authorize(Policy = Permissions.CycleCountManage)]
    public async Task<IActionResult> GetCycleCounts(CancellationToken ct) =>
        Ok(await _inventory.GetCycleCountsAsync(ct));

    [HttpGet("ap-invoices")]
    [Authorize(Policy = Permissions.ApInvoiceManage)]
    public async Task<IActionResult> GetApInvoices([FromQuery] QuerySpec query, CancellationToken ct) =>
        Ok(await _purchase.GetPurchaseInvoicesAsync(query, ct));

    [HttpPost("ap-invoices")]
    [Authorize(Policy = Permissions.ApInvoiceManage)]
    public async Task<IActionResult> CreateApInvoice([FromBody] CreatePurchaseInvoiceRequest request, CancellationToken ct) =>
        FromResult(await _purchase.CreatePurchaseInvoiceAsync(request, ct));

    [HttpPost("ap-invoices/{id:int}/match")]
    [Authorize(Policy = Permissions.ApInvoiceManage)]
    public async Task<IActionResult> MatchAp(int id, CancellationToken ct) =>
        FromResult(await _purchase.MatchThreeWayAsync(id, ct));

    [HttpPost("ap-invoices/{id:int}/post")]
    [Authorize(Policy = Permissions.ApInvoiceManage)]
    public async Task<IActionResult> PostAp(int id, CancellationToken ct) =>
        FromResult(await _purchase.PostPurchaseInvoiceAsync(id, ct));

    [HttpGet("quotations")]
    [Authorize(Policy = Permissions.QuotationsManage)]
    [RequireFeature(ConfigKeys.ModSalesQuotations)]
    public async Task<IActionResult> GetQuotations([FromQuery] QuerySpec query, CancellationToken ct) =>
        Ok(await _sales.GetQuotationsAsync(query, ct));

    [HttpPost("quotations")]
    [Authorize(Policy = Permissions.QuotationsManage)]
    [RequireFeature(ConfigKeys.ModSalesQuotations)]
    public async Task<IActionResult> CreateQuote([FromBody] CreateQuotationRequest request, CancellationToken ct) =>
        FromResult(await _sales.CreateQuotationAsync(request, ct));

    [HttpPost("quotations/{id:int}/convert")]
    [Authorize(Policy = Permissions.QuotationsManage)]
    [RequireFeature(ConfigKeys.ModSalesQuotations)]
    public async Task<IActionResult> ConvertQuote(int id, CancellationToken ct) =>
        FromResult(await _sales.ConvertQuotationToSalesOrderAsync(id, ct));

    [HttpGet("sales-orders")]
    [Authorize(Policy = Permissions.SalesView)]
    [RequireFeature(ConfigKeys.ModSalesOrders)]
    public async Task<IActionResult> GetWholesaleSalesOrders([FromQuery] QuerySpec query, CancellationToken ct) =>
        Ok(await _sales.GetWholesaleSalesOrdersAsync(query, ct));

    [HttpPost("sales-orders/{id:int}/create-delivery")]
    [Authorize(Policy = Permissions.DeliveriesManage)]
    [RequireFeature(ConfigKeys.ModSalesDeliveries)]
    public async Task<IActionResult> CreateDeliveryFromSo(int id, [FromBody] CreateDeliveryFromSalesOrderRequest request, CancellationToken ct) =>
        FromResult(await _sales.CreateDeliveryFromSalesOrderAsync(id, request, ct));

    [HttpPost("sales-orders/{id:int}/create-invoice")]
    [Authorize(Policy = Permissions.SalesView)]
    [RequireFeature(ConfigKeys.ModSalesInvoices)]
    public async Task<IActionResult> CreateInvoiceFromSo(int id, [FromQuery] int? warehouseId, CancellationToken ct) =>
        FromResult(await _sales.CreateInvoiceFromSalesOrderAsync(id, warehouseId, ct));

    [HttpGet("deliveries")]
    [Authorize(Policy = Permissions.DeliveriesManage)]
    [RequireFeature(ConfigKeys.ModSalesDeliveries)]
    public async Task<IActionResult> GetDeliveries([FromQuery] QuerySpec query, [FromQuery] int? salesOrderId, CancellationToken ct)
    {
        if (salesOrderId is int so)
            query.Filters["salesOrderId"] = so;
        return Ok(await _sales.GetDeliveriesAsync(query, ct));
    }

    [HttpPost("deliveries")]
    [Authorize(Policy = Permissions.DeliveriesManage)]
    [RequireFeature(ConfigKeys.ModSalesDeliveries)]
    public async Task<IActionResult> CreateDelivery([FromBody] CreateDeliveryNoteRequest request, CancellationToken ct) =>
        FromResult(await _sales.CreateDeliveryNoteAsync(request, ct));

    [HttpPost("deliveries/{id:int}/confirm-pick")]
    [Authorize(Policy = Permissions.DeliveriesManage)]
    [RequireFeature(ConfigKeys.ModSalesDeliveries)]
    public async Task<IActionResult> ConfirmDeliveryPick(int id, [FromBody] ConfirmDeliveryPickRequest? request, CancellationToken ct) =>
        FromResult(await _sales.ConfirmDeliveryPickAsync(id, request, ct));

    [HttpPost("deliveries/{id:int}/ship")]
    [Authorize(Policy = Permissions.DeliveriesManage)]
    [RequireFeature(ConfigKeys.ModSalesDeliveries)]
    public async Task<IActionResult> ShipDelivery(int id, CancellationToken ct) =>
        FromResult(await _sales.ShipDeliveryAsync(id, ct));

    [HttpPost("deliveries/{id:int}/create-invoice")]
    [Authorize(Policy = Permissions.SalesView)]
    [RequireFeature(ConfigKeys.ModSalesInvoices)]
    public async Task<IActionResult> CreateInvoiceFromDelivery(int id, CancellationToken ct) =>
        FromResult(await _sales.CreateInvoiceFromDeliveryAsync(id, ct));

    [HttpGet("price")]
    [Authorize(Policy = Permissions.SalesView)]
    public async Task<IActionResult> GetPrice([FromQuery] int productId, [FromQuery] decimal quantity = 1, [FromQuery] int? customerId = null, CancellationToken ct = default) =>
        FromResult(await _sales.GetPriceForProductAsync(productId, customerId, quantity, ct));

    [HttpGet("credit-check/{customerId:int}")]
    [Authorize(Policy = Permissions.CustomersView)]
    public async Task<IActionResult> CreditCheck(int customerId, [FromQuery] decimal additionalAmount = 0, CancellationToken ct = default) =>
        FromResult(await _sales.CheckCreditLimitAsync(customerId, additionalAmount, ct));

    [HttpGet("account-mappings")]
    [Authorize(Policy = Permissions.FinanceManage)]
    public async Task<IActionResult> GetAccountMappings(CancellationToken ct) =>
        Ok(await _accountMappings.GetAccountMappingsAsync(ct));

    [HttpPost("account-mappings")]
    [Authorize(Policy = Permissions.FinanceManage)]
    public async Task<IActionResult> CreateAccountMapping([FromBody] CreateAccountMappingRequest request, CancellationToken ct) =>
        FromResult(await _accountMappings.CreateAccountMappingAsync(request, ct));

    [HttpPut("account-mappings/{id:int}")]
    [Authorize(Policy = Permissions.FinanceManage)]
    public async Task<IActionResult> UpdateAccountMapping(int id, [FromBody] UpdateAccountMappingRequest request, CancellationToken ct) =>
        FromResult(await _accountMappings.UpdateAccountMappingAsync(id, request, ct));

    [HttpDelete("account-mappings/{id:int}")]
    [Authorize(Policy = Permissions.FinanceManage)]
    public async Task<IActionResult> DeleteAccountMapping(int id, CancellationToken ct) =>
        FromResult(await _accountMappings.DeleteAccountMappingAsync(id, ct));

    [HttpGet("price-lists")]
    [Authorize(Policy = Permissions.PriceListsManage)]
    public async Task<IActionResult> GetPriceLists([FromQuery] QuerySpec query, CancellationToken ct) =>
        Ok(await _sales.GetPriceListsAsync(query, ct));

    [HttpPost("price-lists")]
    [Authorize(Policy = Permissions.PriceListsManage)]
    public async Task<IActionResult> CreatePriceList([FromBody] CreatePriceListRequest request, CancellationToken ct) =>
        FromResult(await _sales.CreatePriceListAsync(request, ct));

    [HttpPut("price-lists/{id:int}/items")]
    [Authorize(Policy = Permissions.PriceListsManage)]
    public async Task<IActionResult> UpdatePriceListItems(int id, [FromBody] UpdatePriceListItemsRequest request, CancellationToken ct) =>
        FromResult(await _sales.UpdatePriceListItemsAsync(id, request, ct));

    [HttpGet("fbr/submissions")]
    [Authorize(Policy = Permissions.PosCheckout)]
    [RequireFeature(ConfigKeys.ModSalesFbr)]
    public async Task<IActionResult> GetFbrSubmissions([FromQuery] QuerySpec query, CancellationToken ct) =>
        Ok(await _sales.GetFbrSubmissionsAsync(query, ct));

    [HttpGet("fbr/metrics")]
    [Authorize(Policy = Permissions.PosCheckout)]
    [RequireFeature(ConfigKeys.ModSalesFbr)]
    public async Task<IActionResult> GetFbrMetrics(CancellationToken ct) =>
        Ok(await _sales.GetFbrMetricsAsync(ct));

    [HttpGet("aging/customers")]
    [Authorize(Policy = Permissions.FinanceView)]
    public async Task<IActionResult> CustomerAging([FromQuery] DateTime? asOf = null, CancellationToken ct = default) =>
        FromResult(await _reports.CustomerAgingAsync(asOf, ct));

    [HttpGet("aging/suppliers")]
    [Authorize(Policy = Permissions.FinanceView)]
    public async Task<IActionResult> SupplierAging([FromQuery] DateTime? asOf = null, CancellationToken ct = default) =>
        FromResult(await _reports.SupplierAgingAsync(asOf, ct));

    [HttpPost("payments/customer-receipt")]
    [Authorize(Policy = Permissions.FinancePost)]
    public async Task<IActionResult> PostCustomerReceipt([FromBody] PostCustomerReceiptRequest request, CancellationToken ct) =>
        FromResult(await _payments.PostCustomerReceiptAsync(request, ct));

    [HttpPost("payments/supplier-payment")]
    [Authorize(Policy = Permissions.FinancePost)]
    public async Task<IActionResult> PostSupplierPayment([FromBody] PostSupplierPaymentRequest request, CancellationToken ct) =>
        FromResult(await _payments.PostSupplierPaymentAsync(request, ct));

    [HttpGet("kits")]
    [Authorize(Policy = Permissions.KitsManage)]
    public async Task<IActionResult> Kits([FromQuery] QuerySpec query, [FromQuery] int? parentProductId, CancellationToken ct) =>
        Ok(await _master.GetKitsAsync(query, parentProductId, ct));

    [HttpPost("kits")]
    [Authorize(Policy = Permissions.KitsManage)]
    public async Task<IActionResult> UpsertKit([FromBody] UpsertKitRequest request, CancellationToken ct) =>
        FromResult(await _master.UpsertKitAsync(request, ct));

    [HttpGet("supersessions")]
    [Authorize(Policy = Permissions.KitsManage)]
    public async Task<IActionResult> Supersessions([FromQuery] int? productId, CancellationToken ct) =>
        Ok(await _master.GetSupersessionsAsync(productId, ct));

    [HttpPost("supersessions")]
    [Authorize(Policy = Permissions.KitsManage)]
    public async Task<IActionResult> UpsertSupersession([FromBody] UpsertSupersessionRequest request, CancellationToken ct) =>
        FromResult(await _master.UpsertSupersessionAsync(request, ct));

    [HttpGet("reports/trial-balance")]
    [Authorize(Policy = Permissions.FinanceView)]
    public async Task<IActionResult> TrialBalance(
        [FromQuery] DateTime? asOf = null,
        [FromQuery] int? branchId = null,
        CancellationToken ct = default) =>
        FromResult(await _reports.TrialBalanceAsync(asOf ?? DateTime.UtcNow.Date, branchId, ct));

    [HttpGet("reports/profit-loss")]
    [Authorize(Policy = Permissions.FinanceView)]
    public async Task<IActionResult> ProfitLoss(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int? branchId = null,
        CancellationToken ct = default) =>
        FromResult(await _reports.ProfitAndLossAsync(from, to, branchId, ct));

    [HttpGet("reports/balance-sheet")]
    [Authorize(Policy = Permissions.FinanceView)]
    public async Task<IActionResult> BalanceSheet([FromQuery] DateTime? asOf = null, CancellationToken ct = default) =>
        FromResult(await _reports.BalanceSheetAsync(asOf ?? DateTime.UtcNow.Date, ct));

    [HttpPost("fbr/retry/{invoiceId:int}")]
    [Authorize(Policy = Permissions.PosCheckout)]
    [RequireFeature(ConfigKeys.ModSalesFbr)]
    public IActionResult RetryFbr(int invoiceId)
    {
        _fbrOutbox.EnqueueFbrRetry(invoiceId);
        return Accepted(new { message = "FBR retry enqueued.", invoiceId });
    }
}
