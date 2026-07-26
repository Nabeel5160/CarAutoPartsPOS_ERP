using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.System;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CarAutoParts.Presentation.ViewModels;

public partial class AuditLogsViewModel : EntityModuleViewModelBase<AuditLogDto>
{
    private readonly IAuditService _service;

    public AuditLogsViewModel(
        IAuditService service,
        IExceptionHandler exceptionHandler,
        ISnackbarService snackbarService,
        IDialogService dialogService)
        : base("Audit Logs", "System activity and change history", exceptionHandler, snackbarService, dialogService)
    {
        _service = service;
        ShowPaging = true;
        ShowPrimaryAction = false;
        FilterFromDate = DateTime.Today.AddDays(-7);
        FilterToDate = DateTime.Today;
    }

    [ObservableProperty]
    private DateTime? _filterFromDate;

    [ObservableProperty]
    private DateTime? _filterToDate;

    [ObservableProperty]
    private string? _filterUserName;

    protected override async Task LoadItemsAsync()
    {
        var query = new QuerySpec
        {
            Search = string.IsNullOrWhiteSpace(FilterUserName) ? SearchText : FilterUserName.Trim(),
            Page = CurrentPage,
            PageSize = 50
        };

        if (FilterFromDate is DateTime from)
            query.Filters["FromDate"] = from.Date;

        if (FilterToDate is DateTime to)
            query.Filters["ToDate"] = to.Date.AddDays(1).AddTicks(-1);

        var result = await _service.GetAuditLogsAsync(query);
        foreach (var item in result.Items)
            Items.Add(item);

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
