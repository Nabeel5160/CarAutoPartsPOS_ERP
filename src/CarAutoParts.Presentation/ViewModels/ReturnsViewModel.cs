using System.Collections.ObjectModel;

using CarAutoParts.Application.Common;

using CarAutoParts.Application.DTOs.Partners;

using CarAutoParts.Application.DTOs.Products;

using CarAutoParts.Application.DTOs.Sales;

using CarAutoParts.Application.Interfaces;

using CarAutoParts.Domain.Enums;

using CarAutoParts.Presentation.Services;

using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;



namespace CarAutoParts.Presentation.ViewModels;



public partial class ReturnLineEditModel : ObservableObject

{

    public int ProductId { get; set; }

    public string? ProductName { get; set; }



    [ObservableProperty]

    private decimal _quantity = 1;



    [ObservableProperty]

    private decimal _unitPrice;

}



public partial class ReturnsViewModel : EntityModuleViewModelBase<SalesReturnDto>

{

    private readonly IReturnService _returnService;

    private readonly ISalesService _salesService;

    private readonly IWarehouseService _warehouseService;

    private readonly IProductService _productService;



    public ReturnsViewModel(

        IReturnService returnService,

        ISalesService salesService,

        IWarehouseService warehouseService,

        IProductService productService,

        IExceptionHandler exceptionHandler,

        ISnackbarService snackbarService,

        IDialogService dialogService)

        : base("Returns", "Sales returns and stock restoration", exceptionHandler, snackbarService, dialogService)

    {

        _returnService = returnService;

        _salesService = salesService;

        _warehouseService = warehouseService;

        _productService = productService;

        ShowPaging = true;

        PrimaryActionText = "New return";

        ReturnTypes.Add(ReturnType.Partial);

        ReturnTypes.Add(ReturnType.Full);

        EditReturnType = ReturnType.Partial;

    }



    public ObservableCollection<WarehouseDto> Warehouses { get; } = new();

    public ObservableCollection<ReturnLineEditModel> EditLines { get; } = new();

    public ObservableCollection<ProductListDto> ProductOptions { get; } = new();

    public ObservableCollection<ReturnType> ReturnTypes { get; } = new();

    public ObservableCollection<SalesInvoiceListDto> InvoiceOptions { get; } = new();



    [ObservableProperty]

    private SalesReturnDto? _selectedItem;



    [ObservableProperty]

    private string _editorTitle = "New return";



    [ObservableProperty]

    private WarehouseDto? _editWarehouse;



    [ObservableProperty]

    private ReturnType _editReturnType;



    [ObservableProperty]

    private SalesInvoiceListDto? _editInvoice;



    [ObservableProperty]

    private string? _editNotes;



    [ObservableProperty]

    private string _invoiceSearchText = string.Empty;



    [ObservableProperty]

    private string _productSearchText = string.Empty;



    [ObservableProperty]

    private ProductListDto? _selectedProduct;



    protected override async Task LoadItemsAsync()

    {

        var result = await _returnService.GetSalesReturnsAsync(new QuerySpec

        {

            Search = SearchText,

            Page = CurrentPage,

            PageSize = 25

        });



        foreach (var item in result.Items)

            Items.Add(item);



        TotalPages = Math.Max(1, (int)Math.Ceiling(result.TotalCount / (double)result.PageSize));

    }



    private async Task LoadLookupDataAsync()

    {

        if (Warehouses.Count == 0)

        {

            foreach (var warehouse in await _warehouseService.GetAllAsync())

                Warehouses.Add(warehouse);

        }

    }



    private void ResetEditor()

    {

        EditorTitle = "New return";

        EditWarehouse = Warehouses.FirstOrDefault(w => w.IsDefault) ?? Warehouses.FirstOrDefault();

        EditReturnType = ReturnType.Partial;

        EditInvoice = null;

        EditNotes = null;

        InvoiceSearchText = string.Empty;

        ProductSearchText = string.Empty;

        SelectedProduct = null;

        EditLines.Clear();

    }



    [RelayCommand]

    private async Task NewAsync()

    {

        await ExceptionHandler.ExecuteAsync(async () =>

        {

            await LoadLookupDataAsync();

            ResetEditor();

            await LoadProductOptionsAsync();

            IsEditorOpen = true;

        }, "New return");

    }



    [RelayCommand]

    private async Task SearchInvoicesAsync()

    {

        InvoiceOptions.Clear();

        var result = await _salesService.GetInvoicesAsync(new QuerySpec

        {

            Search = string.IsNullOrWhiteSpace(InvoiceSearchText) ? null : InvoiceSearchText.Trim(),

            PageSize = 20

        });

        foreach (var invoice in result.Items)

            InvoiceOptions.Add(invoice);

    }



    [RelayCommand]

    private async Task LoadInvoiceLinesAsync()

    {

        if (EditInvoice is null)

            return;



        await ExceptionHandler.ExecuteAsync(async () =>

        {

            var detail = await _salesService.GetInvoiceByIdAsync(EditInvoice.Id);

            if (detail is null)

            {

                SnackbarService.Show("Invoice not found.");

                return;

            }



            EditLines.Clear();

            foreach (var line in detail.Lines)

            {

                EditLines.Add(new ReturnLineEditModel

                {

                    ProductId = line.ProductId,

                    ProductName = line.ProductName,

                    Quantity = line.Quantity,

                    UnitPrice = line.UnitPrice

                });

            }



            EditReturnType = ReturnType.Full;

            SnackbarService.Show($"Loaded {EditLines.Count} line(s) from invoice.");

        }, "Load invoice lines");

    }



    [RelayCommand]

    private async Task SearchProductsAsync() => await LoadProductOptionsAsync();



    private async Task LoadProductOptionsAsync()

    {

        ProductOptions.Clear();

        var result = await _productService.GetProductsAsync(new ProductQueryDto

        {

            Search = string.IsNullOrWhiteSpace(ProductSearchText) ? null : ProductSearchText.Trim(),

            IsActive = true,

            PageSize = 50

        });

        foreach (var product in result.Items)

            ProductOptions.Add(product);

    }



    [RelayCommand]

    private void AddLine()

    {

        if (SelectedProduct is null)

        {

            SnackbarService.Show("Select a product to add.");

            return;

        }



        if (EditLines.Any(l => l.ProductId == SelectedProduct.Id))

        {

            SnackbarService.Show("Product already on this return.");

            return;

        }



        EditLines.Add(new ReturnLineEditModel

        {

            ProductId = SelectedProduct.Id,

            ProductName = SelectedProduct.Name,

            Quantity = 1,

            UnitPrice = SelectedProduct.SalePrice

        });

        SelectedProduct = null;

    }



    [RelayCommand]

    private void RemoveLine(ReturnLineEditModel? line)

    {

        if (line is not null)

            EditLines.Remove(line);

    }



    [RelayCommand]

    private async Task SaveAsync()

    {

        if (EditWarehouse is null)

        {

            SnackbarService.Show("Warehouse is required.");

            return;

        }



        if (EditLines.Count == 0)

        {

            SnackbarService.Show("Add at least one line item.");

            return;

        }



        await ExceptionHandler.ExecuteAsync(async () =>
        {
            int? customerId = null;
            if (EditInvoice is not null)
            {
                var invoiceDetail = await _salesService.GetInvoiceByIdAsync(EditInvoice.Id);
                customerId = invoiceDetail?.CustomerId;
            }

            var dto = new SalesReturnCreateDto(
                EditInvoice?.Id,
                customerId,
                EditReturnType,
                string.IsNullOrWhiteSpace(EditNotes) ? null : EditNotes.Trim(),
                EditWarehouse.Id,
                EditLines.Select(l => new SalesReturnLineDto(l.ProductId, l.Quantity, l.UnitPrice)).ToList());

            var result = await _returnService.CreateSalesReturnAsync(dto);

            if (!result.Succeeded)

            {

                SnackbarService.Show(result.Error ?? "Return failed.");

                return;

            }



            SnackbarService.Show($"Return {result.Data!.ReturnNumber} created.");

            IsEditorOpen = false;

            await RefreshAsync();

        }, "Create return");

    }



    [RelayCommand]

    private void CancelEdit() => IsEditorOpen = false;



    [RelayCommand]

    private async Task NextPageAsync()

    {

        if (CurrentPage < TotalPages)

        {

            CurrentPage++;

            await RefreshAsync();

        }

    }



    [RelayCommand]

    private async Task PreviousPageAsync()

    {

        if (CurrentPage > 1)

        {

            CurrentPage--;

            await RefreshAsync();

        }

    }

}

