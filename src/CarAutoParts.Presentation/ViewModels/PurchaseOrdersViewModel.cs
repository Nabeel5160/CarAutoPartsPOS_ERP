using System.Collections.ObjectModel;
using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Partners;
using CarAutoParts.Application.DTOs.Products;
using CarAutoParts.Application.DTOs.Purchases;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Enums;
using CarAutoParts.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CarAutoParts.Presentation.ViewModels;

public record PurchaseOrderStatusFilterOption(PurchaseOrderStatus? Status, string Label);

public partial class PurchaseOrderLineEditModel : ObservableObject
{
    public int? LineId { get; set; }
    public int ProductId { get; set; }
    public string? ProductName { get; set; }

    [ObservableProperty]
    private decimal _quantityOrdered;

    [ObservableProperty]
    private decimal _quantityReceived;

    [ObservableProperty]
    private decimal _receiveQuantity;

    [ObservableProperty]
    private string? _batchNumber;

    [ObservableProperty]
    private decimal _unitPrice;

    [ObservableProperty]
    private decimal _taxRate;

    [ObservableProperty]
    private decimal _discountAmount;

    public decimal QuantityRemaining => Math.Max(0, QuantityOrdered - QuantityReceived);

    public static PurchaseOrderLineEditModel FromDto(PurchaseOrderLineDto dto) => new()
    {
        LineId = dto.Id,
        ProductId = dto.ProductId,
        ProductName = dto.ProductName,
        QuantityOrdered = dto.QuantityOrdered,
        QuantityReceived = dto.QuantityReceived,
        UnitPrice = dto.UnitPrice,
        TaxRate = dto.TaxRate,
        DiscountAmount = dto.DiscountAmount
    };
}

public partial class PurchaseOrdersViewModel : EntityModuleViewModelBase<PurchaseOrderListDto>
{
    private readonly IPurchaseOrderService _service;
    private readonly ISupplierService _supplierService;
    private readonly IWarehouseService _warehouseService;
    private readonly IProductService _productService;
    private readonly INavigationState _navigationState;
    private int? _editingId;

    public PurchaseOrdersViewModel(
        IPurchaseOrderService service,
        ISupplierService supplierService,
        IWarehouseService warehouseService,
        IProductService productService,
        INavigationState navigationState,
        IExceptionHandler exceptionHandler,
        ISnackbarService snackbarService,
        IDialogService dialogService)
        : base("Purchase Orders", "Create and manage supplier purchase orders", exceptionHandler, snackbarService, dialogService)
    {
        _service = service;
        _supplierService = supplierService;
        _warehouseService = warehouseService;
        _productService = productService;
        _navigationState = navigationState;
        ShowPaging = true;
        StatusFilters.Add(new PurchaseOrderStatusFilterOption(null, "All statuses"));
        foreach (PurchaseOrderStatus status in Enum.GetValues(typeof(PurchaseOrderStatus)))
            StatusFilters.Add(new PurchaseOrderStatusFilterOption(status, status.ToString()));
        SelectedStatusFilter = StatusFilters[0];
    }

    public override async Task InitializeAsync()
    {
        IsModuleReady = false;
        SuppressFilterRefresh = true;
        try
        {
            if (!string.IsNullOrWhiteSpace(_navigationState.PurchaseOrderSearch))
            {
                SearchText = _navigationState.PurchaseOrderSearch;
                _navigationState.PurchaseOrderSearch = null;
            }

            await RefreshAsync();
        }
        finally
        {
            SuppressFilterRefresh = false;
            IsModuleReady = true;
        }
    }

    public ObservableCollection<PurchaseOrderStatusFilterOption> StatusFilters { get; } = new();
    public ObservableCollection<SupplierDto> Suppliers { get; } = new();
    public ObservableCollection<WarehouseDto> Warehouses { get; } = new();
    public ObservableCollection<PurchaseOrderLineEditModel> EditLines { get; } = new();
    public ObservableCollection<ProductListDto> ProductOptions { get; } = new();

    [ObservableProperty]
    private PurchaseOrderListDto? _selectedItem;

    [ObservableProperty]
    private PurchaseOrderStatusFilterOption? _selectedStatusFilter;

    [ObservableProperty]
    private string _editorTitle = "Purchase Order";

    [ObservableProperty]
    private string? _editOrderNumber;

    [ObservableProperty]
    private PurchaseOrderStatus _editStatus = PurchaseOrderStatus.Draft;

    [ObservableProperty]
    private SupplierDto? _editSupplier;

    [ObservableProperty]
    private WarehouseDto? _editWarehouse;

    [ObservableProperty]
    private DateTime? _editExpectedDate;

    [ObservableProperty]
    private string? _editNotes;

    [ObservableProperty]
    private decimal _editDiscountAmount;

    [ObservableProperty]
    private decimal _editGrandTotal;

    [ObservableProperty]
    private bool _isReceiveMode;

    [ObservableProperty]
    private string? _receiveNotes;

    [ObservableProperty]
    private ProductListDto? _selectedProduct;

    [ObservableProperty]
    private string _productSearchText = string.Empty;

    public bool IsDraftEditable => EditStatus == PurchaseOrderStatus.Draft && !IsReceiveMode;
    public bool EditLinesReadOnly => !IsDraftEditable || IsReceiveMode;
    public bool CanSaveDraft => IsDraftEditable;
    public bool CanApprove => _editingId is not null && EditStatus == PurchaseOrderStatus.Draft && !IsReceiveMode;
    public bool CanReceive => _editingId is not null && EditStatus is PurchaseOrderStatus.Approved or PurchaseOrderStatus.PartiallyReceived;
    public bool CanCancelOrder => _editingId is not null && EditStatus is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Approved;

    partial void OnEditStatusChanged(PurchaseOrderStatus value)
    {
        OnPropertyChanged(nameof(IsDraftEditable));
        OnPropertyChanged(nameof(EditLinesReadOnly));
        OnPropertyChanged(nameof(CanSaveDraft));
        OnPropertyChanged(nameof(CanApprove));
        OnPropertyChanged(nameof(CanReceive));
        OnPropertyChanged(nameof(CanCancelOrder));
    }

    partial void OnIsReceiveModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsDraftEditable));
        OnPropertyChanged(nameof(EditLinesReadOnly));
        OnPropertyChanged(nameof(CanSaveDraft));
        OnPropertyChanged(nameof(CanApprove));
    }

    partial void OnSelectedStatusFilterChanged(PurchaseOrderStatusFilterOption? value)
    {
        if (value is null || SuppressFilterRefresh)
            return;

        _ = SearchAsync();
    }

    protected override async Task LoadItemsAsync()
    {
        var query = new QuerySpec
        {
            Search = SearchText,
            Page = CurrentPage,
            PageSize = 25
        };

        if (SelectedStatusFilter?.Status is PurchaseOrderStatus status)
            query.Filters["Status"] = status;

        var result = await _service.GetOrdersAsync(query);
        foreach (var item in result.Items)
            Items.Add(item);

        TotalPages = Math.Max(1, (int)Math.Ceiling(result.TotalCount / (double)result.PageSize));
    }

    private async Task LoadLookupDataAsync()
    {
        if (Suppliers.Count == 0)
        {
            var suppliers = await _supplierService.GetSuppliersAsync(new QuerySpec { PageSize = 200 });
            foreach (var supplier in suppliers.Items.Where(s => s.IsActive))
                Suppliers.Add(supplier);
        }

        if (Warehouses.Count == 0)
        {
            foreach (var warehouse in await _warehouseService.GetAllAsync())
                Warehouses.Add(warehouse);
        }
    }

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

    private void ResetEditor()
    {
        _editingId = null;
        EditorTitle = "New Purchase Order";
        EditOrderNumber = null;
        EditStatus = PurchaseOrderStatus.Draft;
        EditSupplier = null;
        EditWarehouse = Warehouses.FirstOrDefault(w => w.IsDefault) ?? Warehouses.FirstOrDefault();
        EditExpectedDate = DateTime.Today.AddDays(7);
        EditNotes = null;
        EditDiscountAmount = 0;
        EditGrandTotal = 0;
        IsReceiveMode = false;
        ReceiveNotes = null;
        EditLines.Clear();
        SelectedProduct = null;
        ProductSearchText = string.Empty;
    }

    private void LoadDetail(PurchaseOrderDetailDto detail)
    {
        _editingId = detail.Id;
        EditorTitle = $"PO {detail.OrderNumber}";
        EditOrderNumber = detail.OrderNumber;
        EditStatus = detail.Status;
        EditSupplier = Suppliers.FirstOrDefault(s => s.Id == detail.SupplierId);
        EditWarehouse = detail.WarehouseId is int wid ? Warehouses.FirstOrDefault(w => w.Id == wid) : null;
        EditExpectedDate = detail.ExpectedDate;
        EditNotes = detail.Notes;
        EditDiscountAmount = detail.DiscountAmount;
        EditGrandTotal = detail.GrandTotal;
        IsReceiveMode = false;
        ReceiveNotes = null;
        EditLines.Clear();
        foreach (var line in detail.Lines)
            EditLines.Add(PurchaseOrderLineEditModel.FromDto(line));
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
        }, "New purchase order");
    }

    [RelayCommand]
    private async Task OpenAsync(PurchaseOrderListDto? item)
    {
        if (item is null)
            return;

        await ExceptionHandler.ExecuteAsync(async () =>
        {
            await LoadLookupDataAsync();
            var detail = await _service.GetByIdAsync(item.Id);
            if (detail is null)
            {
                SnackbarService.Show("Purchase order not found.");
                return;
            }

            ResetEditor();
            LoadDetail(detail);
            await LoadProductOptionsAsync();
            IsEditorOpen = true;
        }, "Open purchase order");
    }

    [RelayCommand]
    private async Task SearchProductsAsync() => await LoadProductOptionsAsync();

    [RelayCommand]
    private void AddLine()
    {
        if (!IsDraftEditable)
            return;

        if (SelectedProduct is null)
        {
            SnackbarService.Show("Select a product to add.");
            return;
        }

        if (EditLines.Any(l => l.ProductId == SelectedProduct.Id))
        {
            SnackbarService.Show("Product already on this order.");
            return;
        }

        EditLines.Add(new PurchaseOrderLineEditModel
        {
            ProductId = SelectedProduct.Id,
            ProductName = SelectedProduct.Name,
            QuantityOrdered = 1,
            UnitPrice = SelectedProduct.CostPrice,
            TaxRate = 0
        });
        SelectedProduct = null;
    }

    [RelayCommand]
    private void RemoveLine(PurchaseOrderLineEditModel? line)
    {
        if (line is not null && IsDraftEditable)
            EditLines.Remove(line);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (EditSupplier is null)
        {
            SnackbarService.Show("Supplier is required.");
            return;
        }

        if (EditLines.Count == 0)
        {
            SnackbarService.Show("Add at least one line item.");
            return;
        }

        var dto = new PurchaseOrderCreateDto(
            EditSupplier.Id,
            EditExpectedDate,
            EditWarehouse?.Id,
            string.IsNullOrWhiteSpace(EditNotes) ? null : EditNotes.Trim(),
            EditDiscountAmount,
            EditLines.Select(l => new PurchaseOrderLineDto(
                l.LineId,
                l.ProductId,
                l.ProductName,
                l.QuantityOrdered,
                l.QuantityReceived,
                l.UnitPrice,
                l.TaxRate,
                l.DiscountAmount,
                0)).ToList());

        await ExceptionHandler.ExecuteAsync(async () =>
        {
            if (_editingId is int id)
            {
                var result = await _service.UpdateAsync(id, dto);
                if (!result.Succeeded)
                {
                    SnackbarService.Show(result.Error ?? "Update failed.");
                    return;
                }
                LoadDetail(result.Data!);
                SnackbarService.Show("Purchase order saved.");
            }
            else
            {
                var result = await _service.CreateAsync(dto);
                if (!result.Succeeded)
                {
                    SnackbarService.Show(result.Error ?? "Create failed.");
                    return;
                }
                LoadDetail(result.Data!);
                SnackbarService.Show("Purchase order created.");
            }

            await RefreshAsync();
        }, "Save purchase order");
    }

    [RelayCommand]
    private async Task ApproveAsync()
    {
        if (_editingId is not int id)
            return;

        var confirmed = await DialogService.ConfirmAsync("Approve order", "Approve this purchase order?");
        if (!confirmed)
            return;

        await ExceptionHandler.ExecuteAsync(async () =>
        {
            var result = await _service.ApproveAsync(id);
            if (!result.Succeeded)
            {
                SnackbarService.Show(result.Error ?? "Approve failed.");
                return;
            }

            var detail = await _service.GetByIdAsync(id);
            if (detail is not null)
                LoadDetail(detail);
            SnackbarService.Show("Purchase order approved.");
            await RefreshAsync();
        }, "Approve purchase order");
    }

    [RelayCommand]
    private void StartReceive()
    {
        if (!CanReceive)
            return;

        IsReceiveMode = true;
        foreach (var line in EditLines)
            line.ReceiveQuantity = line.QuantityRemaining;
    }

    [RelayCommand]
    private void CancelReceive() => IsReceiveMode = false;

    [RelayCommand]
    private async Task ConfirmReceiveAsync()
    {
        if (_editingId is not int id)
            return;

        var lines = EditLines
            .Where(l => l.LineId is int && l.ReceiveQuantity > 0)
            .Select(l => new ReceivePurchaseOrderLineDto(l.LineId!.Value, l.ReceiveQuantity, l.BatchNumber))
            .ToList();

        if (lines.Count == 0)
        {
            SnackbarService.Show("Enter quantity to receive for at least one line.");
            return;
        }

        await ExceptionHandler.ExecuteAsync(async () =>
        {
            var result = await _service.ReceiveAsync(id, new ReceivePurchaseOrderDto(lines, ReceiveNotes));
            if (!result.Succeeded)
            {
                SnackbarService.Show(result.Error ?? "Receive failed.");
                return;
            }

            var detail = await _service.GetByIdAsync(id);
            if (detail is not null)
                LoadDetail(detail);
            IsReceiveMode = false;
            SnackbarService.Show("Goods received — inventory updated.");
            await RefreshAsync();
        }, "Receive purchase order");
    }

    [RelayCommand]
    private async Task CancelOrderAsync()
    {
        if (_editingId is not int id)
            return;

        var confirmed = await DialogService.ConfirmAsync("Cancel order", "Cancel this purchase order?");
        if (!confirmed)
            return;

        await ExceptionHandler.ExecuteAsync(async () =>
        {
            var result = await _service.CancelAsync(id);
            if (!result.Succeeded)
            {
                SnackbarService.Show(result.Error ?? "Cancel failed.");
                return;
            }

            var detail = await _service.GetByIdAsync(id);
            if (detail is not null)
                LoadDetail(detail);
            SnackbarService.Show("Purchase order cancelled.");
            await RefreshAsync();
        }, "Cancel purchase order");
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
