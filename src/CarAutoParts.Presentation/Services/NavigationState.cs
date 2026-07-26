namespace CarAutoParts.Presentation.Services;



public class NavigationState : INavigationState

{

    public string? ProductSearch { get; set; }

    public string? CustomerSearch { get; set; }

    public string? SupplierSearch { get; set; }

    public string? PurchaseOrderSearch { get; set; }

    public string? SalesInvoiceSearch { get; set; }

    public bool InventoryLowStockOnly { get; set; }

}

