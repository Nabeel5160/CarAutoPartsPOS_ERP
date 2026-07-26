using System.IO;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.DTOs.Settings;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace CarAutoParts.Presentation.ViewModels;

public partial class BackupViewModel : EntityModuleViewModelBase<BackupHistoryDto>
{
    private readonly IBackupService _service;
    private readonly ICurrentUserService _currentUser;

    public BackupViewModel(
        IBackupService service,
        ICurrentUserService currentUser,
        IExceptionHandler exceptionHandler,
        ISnackbarService snackbarService,
        IDialogService dialogService)
        : base("Backup & Restore", "Database backup history and restore", exceptionHandler, snackbarService, dialogService)
    {
        _service = service;
        _currentUser = currentUser;
        PrimaryActionText = "Create Backup";
        ShowPrimaryAction = _currentUser.HasPermission(Permissions.BackupManage);
    }

    [ObservableProperty]
    private BackupHistoryDto? _selectedItem;

    public bool CanRestore => _currentUser.HasPermission(Permissions.BackupManage);

    protected override async Task LoadItemsAsync()
    {
        var history = await _service.GetHistoryAsync();
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? history
            : history.Where(h =>
                Path.GetFileName(h.FilePath).Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var item in filtered.OrderByDescending(h => h.BackupDate))
            Items.Add(item);
    }

    [RelayCommand]
    private async Task NewAsync() => await CreateBackupAsync();

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        if (!_currentUser.HasPermission(Permissions.BackupManage))
        {
            SnackbarService.Show("You do not have permission to create backups.");
            return;
        }

        await ExceptionHandler.ExecuteAsync(async () =>
        {
            IsBusy = true;
            var result = await _service.CreateBackupAsync(false);
            if (result.Succeeded)
                SnackbarService.Show($"Backup created: {Path.GetFileName(result.Data)}");
            else
                SnackbarService.Show(result.Error ?? "Backup failed.");

            await RefreshAsync();
            IsBusy = false;
        }, "Create backup");
    }

    [RelayCommand]
    private async Task RestoreAsync(BackupHistoryDto? item)
    {
        if (item is null)
            return;

        if (!_currentUser.HasPermission(Permissions.BackupManage))
        {
            SnackbarService.Show("You do not have permission to restore backups.");
            return;
        }

        if (!item.IsSuccessful)
        {
            SnackbarService.Show("Cannot restore a failed backup.");
            return;
        }

        var confirmed = await DialogService.ConfirmAsync(
            "Restore database",
            $"Restore from backup dated {item.BackupDate:yyyy-MM-dd HH:mm}? This will overwrite current data.");
        if (!confirmed)
            return;

        await ExceptionHandler.ExecuteAsync(async () =>
        {
            IsBusy = true;
            var result = await _service.RestoreBackupAsync(item.FilePath);
            if (result.Succeeded)
                SnackbarService.Show("Database restored successfully. Restart the application.");
            else
                SnackbarService.Show(result.Error ?? "Restore failed.");

            IsBusy = false;
        }, "Restore backup");
    }

    [RelayCommand]
    private async Task RestoreFromFileAsync()
    {
        if (!CanRestore)
        {
            SnackbarService.Show("You do not have permission to restore backups.");
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Select backup file",
            Filter = "Backup files (*.bak;*.zip)|*.bak;*.zip|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
            return;

        var confirmed = await DialogService.ConfirmAsync(
            "Restore database",
            $"Restore from {Path.GetFileName(dialog.FileName)}? This will overwrite current data.");
        if (!confirmed)
            return;

        await ExceptionHandler.ExecuteAsync(async () =>
        {
            IsBusy = true;
            var result = await _service.RestoreBackupAsync(dialog.FileName);
            if (result.Succeeded)
                SnackbarService.Show("Database restored successfully. Restart the application.");
            else
                SnackbarService.Show(result.Error ?? "Restore failed.");

            IsBusy = false;
        }, "Restore backup");
    }
}
