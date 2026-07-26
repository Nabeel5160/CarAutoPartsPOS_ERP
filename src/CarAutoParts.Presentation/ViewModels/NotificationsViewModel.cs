using CarAutoParts.Application.DTOs.System;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CarAutoParts.Presentation.ViewModels;

public partial class NotificationsViewModel : EntityModuleViewModelBase<NotificationDto>
{
    private readonly INotificationService _service;

    public NotificationsViewModel(
        INotificationService service,
        IExceptionHandler exceptionHandler,
        ISnackbarService snackbarService,
        IDialogService dialogService)
        : base("Notifications", "In-app alerts and messages", exceptionHandler, snackbarService, dialogService)
    {
        _service = service;
        ShowPrimaryAction = false;
    }

    [ObservableProperty]
    private bool _unreadOnly;

    partial void OnUnreadOnlyChanged(bool value)
    {
        if (SuppressFilterRefresh)
            return;

        _ = ApplyUnreadFilterAsync();
    }

    [ObservableProperty]
    private NotificationDto? _selectedItem;

    protected override async Task LoadItemsAsync()
    {
        var items = await _service.GetNotificationsAsync(UnreadOnly);
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? items
            : items.Where(n =>
                n.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                n.Message.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var item in filtered)
            Items.Add(item);
    }

    [RelayCommand]
    private async Task MarkReadAsync(NotificationDto? item)
    {
        if (item is null || item.IsRead)
            return;

        await ExceptionHandler.ExecuteAsync(async () =>
        {
            await _service.MarkAsReadAsync(item.Id);
            SnackbarService.Show("Marked as read.");
            await RefreshAsync();
        }, "Mark notification read");
    }

    [RelayCommand]
    private async Task MarkAllReadAsync()
    {
        await ExceptionHandler.ExecuteAsync(async () =>
        {
            var unread = await _service.GetNotificationsAsync(unreadOnly: true);
            foreach (var item in unread)
                await _service.MarkAsReadAsync(item.Id);

            SnackbarService.Show(unread.Count == 0 ? "No unread notifications." : "All notifications marked read.");
            await RefreshAsync();
        }, "Mark all read");
    }

    [RelayCommand]
    private async Task ApplyUnreadFilterAsync() => await RefreshAsync();
}
