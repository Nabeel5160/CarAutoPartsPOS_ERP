using System.Collections.ObjectModel;
using CarAutoParts.Application.DTOs.Inventory;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Enums;
using CarAutoParts.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CarAutoParts.Presentation.ViewModels;

public record StockMovementListItem(
    DateTime MovementDate,
    StockMovementType MovementType,
    string ProductName,
    decimal Quantity,
    string Reference)
{
    public static StockMovementListItem FromDto(StockMovementDto dto) =>
        new(
            dto.MovementDate,
            dto.MovementType,
            dto.ProductName,
            dto.Quantity,
            FormatReference(dto.ReferenceType, dto.ReferenceId));

    private static string FormatReference(string? type, int? id) =>
        type is null ? "—" : id is int referenceId ? $"{type} #{referenceId}" : type;
}

public partial class StockMovementsViewModel : EntityModuleViewModelBase<StockMovementListItem>
{
    private readonly IInventoryService _service;

    public StockMovementsViewModel(
        IInventoryService service,
        IExceptionHandler exceptionHandler,
        ISnackbarService snackbarService,
        IDialogService dialogService)
        : base("Stock Movements", "Inventory transaction history", exceptionHandler, snackbarService, dialogService)
    {
        _service = service;
        ShowPaging = true;
        ShowPrimaryAction = false;
    }

    [ObservableProperty]
    private DateTime? _filterFromDate;

    [ObservableProperty]
    private DateTime? _filterToDate;

    protected override async Task LoadItemsAsync()
    {
        var result = await _service.GetMovementsAsync(new StockMovementQueryDto
        {
            FromDate = FilterFromDate,
            ToDate = FilterToDate?.Date.AddDays(1).AddTicks(-1),
            Page = CurrentPage,
            PageSize = 25
        });

        foreach (var item in result.Items)
            Items.Add(StockMovementListItem.FromDto(item));

        TotalPages = Math.Max(1, (int)Math.Ceiling(result.TotalCount / (double)result.PageSize));
    }

    [RelayCommand]
    private async Task ApplyFiltersAsync() => await SearchAsync();

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
