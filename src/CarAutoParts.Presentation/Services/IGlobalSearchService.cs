namespace CarAutoParts.Presentation.Services;



public enum GlobalSearchResultKind

{

    Product,

    Customer,

    Supplier,

    PurchaseOrder,

    SalesInvoice

}



public record GlobalSearchResult(

    string Title,

    string Subtitle,

    Type ViewModelType,

    string? SearchHint,

    GlobalSearchResultKind Kind = GlobalSearchResultKind.Product);



public interface IGlobalSearchService

{

    Task<IReadOnlyList<GlobalSearchResult>> SearchAsync(string query, CancellationToken ct = default);

}

