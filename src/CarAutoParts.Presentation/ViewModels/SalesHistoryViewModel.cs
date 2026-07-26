using System.Collections.ObjectModel;

using CarAutoParts.Application.Common;

using CarAutoParts.Application.DTOs.Sales;

using CarAutoParts.Application.Interfaces;

using CarAutoParts.Domain.Enums;

using CarAutoParts.Presentation.Services;

using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;



namespace CarAutoParts.Presentation.ViewModels;



public record SalesOrderStatusFilterOption(SalesOrderStatus? Status, string Label);



public partial class SalesHistoryViewModel : EntityModuleViewModelBase<SalesOrderListDto>

{

    private readonly ISalesService _service;



    public SalesHistoryViewModel(

        ISalesService service,

        IExceptionHandler exceptionHandler,

        ISnackbarService snackbarService,

        IDialogService dialogService)

        : base("Sales History", "Read-only sales orders and status", exceptionHandler, snackbarService, dialogService)

    {

        _service = service;

        ShowPaging = true;

        ShowPrimaryAction = false;

        StatusFilters.Add(new SalesOrderStatusFilterOption(null, "All statuses"));

        foreach (SalesOrderStatus status in Enum.GetValues(typeof(SalesOrderStatus)))

            StatusFilters.Add(new SalesOrderStatusFilterOption(status, status.ToString()));

        SelectedStatusFilter = StatusFilters[0];

    }



    public ObservableCollection<SalesOrderStatusFilterOption> StatusFilters { get; } = new();



    [ObservableProperty]

    private SalesOrderStatusFilterOption? _selectedStatusFilter;



    partial void OnSelectedStatusFilterChanged(SalesOrderStatusFilterOption? value)
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



        if (SelectedStatusFilter?.Status is SalesOrderStatus status)

            query.Filters["Status"] = status;



        var result = await _service.GetOrdersAsync(query);

        foreach (var item in result.Items)

            Items.Add(item);



        TotalPages = Math.Max(1, (int)Math.Ceiling(result.TotalCount / (double)result.PageSize));

    }



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

