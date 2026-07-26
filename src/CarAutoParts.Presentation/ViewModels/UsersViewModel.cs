using System.Collections.ObjectModel;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.DTOs.Auth;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CarAutoParts.Presentation.ViewModels;

public partial class RoleSelectionItem : ObservableObject
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;

    [ObservableProperty]
    private bool _isSelected;
}

public partial class UsersViewModel : EntityModuleViewModelBase<UserDto>
{
    private readonly IUserService _service;
    private readonly ICurrentUserService _currentUser;
    private int? _editingId;

    public UsersViewModel(
        IUserService service,
        ICurrentUserService currentUser,
        IExceptionHandler exceptionHandler,
        ISnackbarService snackbarService,
        IDialogService dialogService)
        : base("Users", "Manage application users and roles", exceptionHandler, snackbarService, dialogService)
    {
        _service = service;
        _currentUser = currentUser;
        ShowPrimaryAction = _currentUser.HasPermission(Permissions.UsersManage);
    }

    public ObservableCollection<RoleSelectionItem> RoleOptions { get; } = new();

    [ObservableProperty]
    private UserDto? _selectedItem;

    [ObservableProperty]
    private string _editUsername = string.Empty;

    [ObservableProperty]
    private string? _editPassword;

    [ObservableProperty]
    private string _editDisplayName = string.Empty;

    [ObservableProperty]
    private string? _editEmail;

    [ObservableProperty]
    private bool _editIsActive = true;

    public bool CanManageUsers => _currentUser.HasPermission(Permissions.UsersManage);

    protected override async Task LoadItemsAsync()
    {
        var users = await _service.GetUsersAsync();
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? users
            : users.Where(u =>
                u.Username.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                u.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var item in filtered)
            Items.Add(item);
    }

    private async Task LoadRolesAsync(IReadOnlyList<string>? selectedRoleNames = null)
    {
        RoleOptions.Clear();
        var roles = await _service.GetRolesAsync();
        selectedRoleNames ??= Array.Empty<string>();
        foreach (var role in roles)
        {
            RoleOptions.Add(new RoleSelectionItem
            {
                Id = role.Id,
                Name = role.Name,
                IsSelected = selectedRoleNames.Contains(role.Name)
            });
        }
    }

    [RelayCommand]
    private async Task NewAsync()
    {
        if (!CanManageUsers)
            return;

        _editingId = null;
        EditUsername = string.Empty;
        EditPassword = null;
        EditDisplayName = string.Empty;
        EditEmail = null;
        EditIsActive = true;
        await LoadRolesAsync();
        IsEditorOpen = true;
    }

    [RelayCommand]
    private async Task EditAsync(UserDto? item)
    {
        if (item is null || !CanManageUsers)
            return;

        _editingId = item.Id;
        EditUsername = item.Username;
        EditPassword = null;
        EditDisplayName = item.DisplayName;
        EditEmail = item.Email;
        EditIsActive = item.IsActive;
        await LoadRolesAsync(item.Roles);
        IsEditorOpen = true;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!CanManageUsers)
        {
            SnackbarService.Show("You do not have permission to manage users.");
            return;
        }

        if (string.IsNullOrWhiteSpace(EditUsername) || string.IsNullOrWhiteSpace(EditDisplayName))
        {
            SnackbarService.Show("Username and display name are required.");
            return;
        }

        if (_editingId is null && string.IsNullOrWhiteSpace(EditPassword))
        {
            SnackbarService.Show("Password is required for new users.");
            return;
        }

        var roleIds = RoleOptions.Where(r => r.IsSelected).Select(r => r.Id).ToList();
        var dto = new UserCreateDto(
            EditUsername.Trim(),
            string.IsNullOrWhiteSpace(EditPassword) ? null : EditPassword,
            EditDisplayName.Trim(),
            string.IsNullOrWhiteSpace(EditEmail) ? null : EditEmail.Trim(),
            EditIsActive,
            roleIds);

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
                SnackbarService.Show("User updated.");
            }
            else
            {
                var result = await _service.CreateAsync(dto);
                if (!result.Succeeded)
                {
                    SnackbarService.Show(result.Error ?? "Create failed.");
                    return;
                }
                SnackbarService.Show("User created.");
            }

            IsEditorOpen = false;
            await RefreshAsync();
        }, "Save user");
    }

    [RelayCommand]
    private void CancelEdit() => IsEditorOpen = false;

    [RelayCommand]
    private async Task DeleteAsync(UserDto? item)
    {
        if (item is null)
            return;

        if (!_currentUser.HasPermission(Permissions.UsersManage))
        {
            SnackbarService.Show("You do not have permission to delete users.");
            return;
        }

        var confirmed = await DialogService.ConfirmAsync("Delete user", $"Delete user {item.Username}?");
        if (!confirmed)
            return;

        await ExceptionHandler.ExecuteAsync(async () =>
        {
            var result = await _service.DeleteAsync(item.Id);
            if (!result.Succeeded)
            {
                SnackbarService.Show(result.Error ?? "Delete failed.");
                return;
            }
            SnackbarService.Show("User deleted.");
            await RefreshAsync();
        }, "Delete user");
    }
}
