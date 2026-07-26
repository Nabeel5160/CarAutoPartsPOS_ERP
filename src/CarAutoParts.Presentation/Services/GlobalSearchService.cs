using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Products;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Presentation.ViewModels;

namespace CarAutoParts.Presentation.Services;

public class GlobalSearchService : IGlobalSearchService
{
    private readonly IProductService _productService;
    private readonly ICustomerService _customerService;
    private readonly ISupplierService _supplierService;
    private readonly IPurchaseOrderService _purchaseOrderService;
    private readonly ISalesService _salesService;

    public GlobalSearchService(
        IProductService productService,
        ICustomerService customerService,
        ISupplierService supplierService,
        IPurchaseOrderService purchaseOrderService,
        ISalesService salesService)
    {
        _productService = productService;
        _customerService = customerService;
        _supplierService = supplierService;
        _purchaseOrderService = purchaseOrderService;
        _salesService = salesService;
    }

    public async Task<IReadOnlyList<GlobalSearchResult>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<GlobalSearchResult>();

        var term = query.Trim();
        var results = new List<GlobalSearchResult>();

        var products = await _productService.GetProductsAsync(new ProductQueryDto
        {
            Search = term,
            Page = 1,
            PageSize = 5
        }, ct);
        results.AddRange(products.Items.Select(p => new GlobalSearchResult(
            p.Name,
            $"Product · SKU: {p.Sku}",
            typeof(ProductsViewModel),
            term,
            GlobalSearchResultKind.Product)));

        var customers = await _customerService.GetCustomersAsync(new QuerySpec { Search = term, PageSize = 3 }, ct);
        results.AddRange(customers.Items.Select(c => new GlobalSearchResult(
            c.Name,
            $"Customer · {c.Phone ?? c.Email ?? c.CustomerType.ToString()}",
            typeof(CustomersViewModel),
            term,
            GlobalSearchResultKind.Customer)));

        var suppliers = await _supplierService.GetSuppliersAsync(new QuerySpec { Search = term, PageSize = 3 }, ct);
        results.AddRange(suppliers.Items.Select(s => new GlobalSearchResult(
            s.Name,
            $"Supplier · {s.City ?? s.Phone ?? "Vendor"}",
            typeof(SuppliersViewModel),
            term,
            GlobalSearchResultKind.Supplier)));

        var purchaseOrders = await _purchaseOrderService.GetOrdersAsync(new QuerySpec { Search = term, PageSize = 3 }, ct);
        results.AddRange(purchaseOrders.Items.Select(o => new GlobalSearchResult(
            o.OrderNumber,
            $"Purchase order · {o.SupplierName} · {o.Status}",
            typeof(PurchaseOrdersViewModel),
            term,
            GlobalSearchResultKind.PurchaseOrder)));

        var invoices = await _salesService.GetInvoicesAsync(new QuerySpec { Search = term, PageSize = 3 }, ct);
        results.AddRange(invoices.Items.Select(i => new GlobalSearchResult(
            i.InvoiceNumber,
            $"Sales invoice · {i.CustomerName ?? "Walk-in"} · {i.GrandTotal:N2}",
            typeof(SalesInvoicesViewModel),
            term,
            GlobalSearchResultKind.SalesInvoice)));

        return results;
    }
}
