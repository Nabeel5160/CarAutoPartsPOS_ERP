using CarAutoParts.Web.Auth;
using CarAutoParts.Web.Models;

namespace CarAutoParts.Web.Services;

public enum WebGlobalSearchKind
{
    Product,
    Customer,
    Supplier,
    PurchaseOrder,
    SalesInvoice
}

public sealed record WebGlobalSearchHit(
    string Title,
    string Subtitle,
    string Href,
    WebGlobalSearchKind Kind);

public sealed class WebGlobalSearchService
{
    private readonly CapApiService _api;
    private readonly PermissionService _perms;

    public WebGlobalSearchService(CapApiService api, PermissionService perms)
    {
        _api = api;
        _perms = perms;
    }

    public async Task<IReadOnlyList<WebGlobalSearchHit>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<WebGlobalSearchHit>();

        var term = query.Trim();
        var results = new List<WebGlobalSearchHit>();
        var q3 = new QuerySpec { Search = term, Page = 1, PageSize = 3 };
        var q5 = new QuerySpec { Search = term, Page = 1, PageSize = 5 };

        if (await _perms.HasAsync(Permissions.ProductsView))
        {
            var (data, _, _) = await _api.GetProductsAsync(q5);
            if (data?.Items is { } items)
            {
                results.AddRange(items.Select(p => new WebGlobalSearchHit(
                    p.Name,
                    $"Product · SKU: {p.Sku}",
                    $"products?search={Uri.EscapeDataString(term)}",
                    WebGlobalSearchKind.Product)));
            }
        }

        if (await _perms.HasAsync(Permissions.CustomersView))
        {
            var (data, _, _) = await _api.GetCustomersAsync(q3);
            if (data?.Items is { } items)
            {
                results.AddRange(items.Select(c => new WebGlobalSearchHit(
                    c.Name,
                    $"Customer · {c.Phone ?? c.Email ?? "Account"}",
                    $"customers?search={Uri.EscapeDataString(term)}",
                    WebGlobalSearchKind.Customer)));
            }
        }

        if (await _perms.HasAsync(Permissions.SuppliersView))
        {
            var (data, _, _) = await _api.GetSuppliersAsync(q3);
            if (data?.Items is { } items)
            {
                results.AddRange(items.Select(s => new WebGlobalSearchHit(
                    s.Name,
                    $"Supplier · {s.City ?? s.Phone ?? "Vendor"}",
                    $"suppliers?search={Uri.EscapeDataString(term)}",
                    WebGlobalSearchKind.Supplier)));
            }
        }

        if (await _perms.HasAsync(Permissions.PurchasesView))
        {
            var (data, _, _) = await _api.GetPurchaseOrdersAsync(q3);
            if (data?.Items is { } items)
            {
                results.AddRange(items.Select(o => new WebGlobalSearchHit(
                    o.OrderNumber,
                    $"PO · {o.SupplierName} · {o.Status}",
                    $"purchases?search={Uri.EscapeDataString(term)}",
                    WebGlobalSearchKind.PurchaseOrder)));
            }
        }

        if (await _perms.HasAsync(Permissions.SalesView))
        {
            var (data, _, _) = await _api.GetInvoicesAsync(q3);
            if (data?.Items is { } items)
            {
                results.AddRange(items.Select(i => new WebGlobalSearchHit(
                    i.InvoiceNumber,
                    $"Invoice · {i.CustomerName ?? "Walk-in"} · {i.GrandTotal:N2}",
                    $"invoices?search={Uri.EscapeDataString(term)}",
                    WebGlobalSearchKind.SalesInvoice)));
            }
        }

        return results;
    }
}
