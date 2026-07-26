using System.Collections.ObjectModel;
using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Products;
using CarAutoParts.Application.DTOs.Transfers;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Enums;
using CarAutoParts.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CarAutoParts.Presentation.ViewModels;

public record TransferStatusFilterOption(TransferStatus? Status, string Label);

public partial class TransferLineEditModel : ObservableObject
{
    public int ProductId { get; set; }
    public string? ProductName { get; set; }

    [ObservableProperty]
    private decimal _quantity = 1;
}

public partial class TransfersViewModel : EntityModuleViewModelBase<TransferListDto>
{
    private readonly ITransferService _service;
    private readonly IWarehouseService _warehouseService;
    private readonly IProductService _productService;
    private int? _editingId;

    public TransfersViewModel(
        ITransferService service,
        IWarehouseService warehouseService,
        IProductService productService,
        IExceptionHandler exceptionHandler,
        ISnackbarService snackbarService,
        IDialogService dialogService)
        : base("Stock Transfers", "Move inventory between warehouses", exceptionHandler, snackbarService, dialogService)
    {
        _service = service;
        _warehouseService = warehouseService;
        _productService = productService;
        ShowPaging = true;
        StatusFilters.Add(new TransferStatusFilterOption(null, "All statuses"));
        foreach (TransferStatus status in Enum.GetValues(typeof(TransferStatus)))
            StatusFilters.Add(new TransferStatusFilterOption(status, status.ToString()));
        SelectedStatusFilter = StatusFilters[0];
    }

    public ObservableCollection<TransferStatusFilterOption> StatusFilters { get; } = new();
    public ObservableCollection<WarehouseDto> Warehouses { get; } = new();
    public ObservableCollection<TransferLineEditModel> EditLines { get; } = new();
    public ObservableCollection<ProductListDto> ProductOptions { get; } = new();

    [ObservableProperty]
    private TransferListDto? _selectedItem;

    [ObservableProperty]
    private TransferStatusFilterOption? _selectedStatusFilter;

    [ObservableProperty]
    private string _editorTitle = "Stock Transfer";

    [ObservableProperty]
    private TransferStatus _editStatus = TransferStatus.Draft;

    [ObservableProperty]
    private WarehouseDto? _editFromWarehouse;

    [ObservableProperty]
    private WarehouseDto? _editToWarehouse;

    [ObservableProperty]
    private string? _editNotes;

    [ObservableProperty]
    private ProductListDto? _selectedProduct;

    [ObservableProperty]
    private string _productSearchText = string.Empty;

    public bool IsDraftEditable => EditStatus == TransferStatus.Draft;
    public bool EditLinesReadOnly => !IsDraftEditable;
    public bool CanSaveDraft => IsDraftEditable && _editingId is null;
    public bool CanApprove => _editingId is not null && EditStatus == TransferStatus.Draft;
    public bool CanComplete => _editingId is not null && EditStatus is TransferStatus.Approved or TransferStatus.InTransit;

    partial void OnEditStatusChanged(TransferStatus value)
    {
        OnPropertyChanged(nameof(IsDraftEditable));
        OnPropertyChanged(nameof(CanSaveDraft));
        OnPropertyChanged(nameof(CanApprove));
        OnPropertyChanged(nameof(CanComplete));
    }

    partial void OnSelectedStatusFilterChanged(TransferStatusFilterOption? value)
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

        if (SelectedStatusFilter?.Status is TransferStatus status)
            query.Filters["Status"] = status;

        var result = await _service.GetTransfersAsync(query);
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
        EditorTitle = "New Transfer";
        EditStatus = TransferStatus.Draft;
        EditFromWarehouse = Warehouses.FirstOrDefault(w => w.IsDefault) ?? Warehouses.FirstOrDefault();
        EditToWarehouse = Warehouses.Skip(1).FirstOrDefault() ?? Warehouses.FirstOrDefault();
        EditNotes = null;
        EditLines.Clear();
        SelectedProduct = null;
        ProductSearchText = string.Empty;
    }

    private void LoadDetail(TransferDetailDto detail)
    {
        _editingId = detail.Id;
        EditorTitle = $"Transfer {detail.TransferNumber}";
        EditStatus = detail.Status;
        EditFromWarehouse = Warehouses.FirstOrDefault(w => w.Id == detail.FromWarehouseId);
        EditToWarehouse = Warehouses.FirstOrDefault(w => w.Id == detail.ToWarehouseId);
        EditNotes = detail.Notes;
        EditLines.Clear();
        foreach (var line in detail.Lines)
        {
            EditLines.Add(new TransferLineEditModel
            {
                ProductId = line.ProductId,
                ProductName = line.ProductName,
                Quantity = line.Quantity
            });
        }

        OnPropertyChanged(nameof(IsDraftEditable));
        OnPropertyChanged(nameof(EditLinesReadOnly));
        OnPropertyChanged(nameof(CanSaveDraft));
        OnPropertyChanged(nameof(CanApprove));
        OnPropertyChanged(nameof(CanComplete));
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
        }, "New transfer");
    }

    [RelayCommand]
    private async Task OpenAsync(TransferListDto? item)
    {
        if (item is null)
            return;

        await ExceptionHandler.ExecuteAsync(async () =>
        {
            await LoadLookupDataAsync();
            var detail = await _service.GetByIdAsync(item.Id);
            if (detail is null)
            {
                SnackbarService.Show("Transfer not found.");
                return;
            }

            LoadDetail(detail);
            IsEditorOpen = true;
        }, "Open transfer");
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
            SnackbarService.Show("Product already on this transfer.");
            return;
        }

        EditLines.Add(new TransferLineEditModel
        {
            ProductId = SelectedProduct.Id,
            ProductName = SelectedProduct.Name,
            Quantity = 1
        });
        SelectedProduct = null;
    }

    [RelayCommand]
    private void RemoveLine(TransferLineEditModel? line)
    {
        if (line is not null && IsDraftEditable)
            EditLines.Remove(line);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (EditFromWarehouse is null || EditToWarehouse is null)
        {
            SnackbarService.Show("Source and destination warehouses are required.");
            return;
        }

        if (EditFromWarehouse.Id == EditToWarehouse.Id)
        {
            SnackbarService.Show("Source and destination must differ.");
            return;
        }

        if (EditLines.Count == 0)
        {
            SnackbarService.Show("Add at least one line item.");
            return;
        }

        var dto = new TransferCreateDto(
            EditFromWarehouse.Id,
            EditToWarehouse.Id,
            string.IsNullOrWhiteSpace(EditNotes) ? null : EditNotes.Trim(),
            EditLines.Select(l => new TransferLineDto(l.ProductId, l.ProductName, l.Quantity)).ToList());

        await ExceptionHandler.ExecuteAsync(async () =>
        {
            var result = await _service.CreateAsync(dto);
            if (!result.Succeeded)
            {
                SnackbarService.Show(result.Error ?? "Create failed.");
                return;
            }

            LoadDetail(result.Data!);
            SnackbarService.Show("Transfer created.");
            await RefreshAsync();
        }, "Save transfer");
    }

    [RelayCommand]
    private async Task ApproveAsync()
    {
        if (_editingId is not int id)
            return;

        var confirmed = await DialogService.ConfirmAsync("Approve transfer", "Approve this stock transfer?");
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

            EditStatus = TransferStatus.Approved;
            SnackbarService.Show("Transfer approved.");
            await RefreshAsync();
        }, "Approve transfer");
    }

    [RelayCommand]
    private async Task CompleteAsync()
    {
        if (_editingId is not int id)
            return;

        var confirmed = await DialogService.ConfirmAsync("Complete transfer", "Complete this transfer and move stock?");
        if (!confirmed)
            return;

        await ExceptionHandler.ExecuteAsync(async () =>
        {
            var result = await _service.CompleteAsync(id);
            if (!result.Succeeded)
            {
                SnackbarService.Show(result.Error ?? "Complete failed.");
                return;
            }

            EditStatus = TransferStatus.Completed;
            SnackbarService.Show("Transfer completed — inventory updated.");
            await RefreshAsync();
        }, "Complete transfer");
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
