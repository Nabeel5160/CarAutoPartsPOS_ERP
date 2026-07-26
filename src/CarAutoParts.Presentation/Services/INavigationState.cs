namespace CarAutoParts.Presentation.Services;



public interface INavigationState

{

    string? ProductSearch { get; set; }

    string? CustomerSearch { get; set; }

    string? SupplierSearch { get; set; }

    string? PurchaseOrderSearch { get; set; }

    string? SalesInvoiceSearch { get; set; }

    bool InventoryLowStockOnly { get; set; }

}

