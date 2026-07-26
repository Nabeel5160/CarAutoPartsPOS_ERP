using System.Collections.ObjectModel;
using CarAutoParts.Application.DTOs.Products;
using CarAutoParts.Application.DTOs.Settings;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CarAutoParts.Presentation.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IWarehouseService _warehouseService;
    private readonly IThemeService _themeService;
    private readonly ISnackbarService _snackbarService;
    private readonly IExceptionHandler _exceptionHandler;
    private CompanySettingsDto? _current;

    public SettingsViewModel(
        ISettingsService settingsService,
        IWarehouseService warehouseService,
        IThemeService themeService,
        ISnackbarService snackbarService,
        IExceptionHandler exceptionHandler)
    {
        _settingsService = settingsService;
        _warehouseService = warehouseService;
        _themeService = themeService;
        _snackbarService = snackbarService;
        _exceptionHandler = exceptionHandler;
        ThemeOptions.Add("Light");
        ThemeOptions.Add("Dark");
    }

    public ObservableCollection<string> ThemeOptions { get; } = new();
    public ObservableCollection<WarehouseDto> Warehouses { get; } = new();

    [ObservableProperty] private string _companyName = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private string _phone = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _taxNumber = string.Empty;
    [ObservableProperty] private string _strn = string.Empty;
    [ObservableProperty] private string _posId = string.Empty;
    [ObservableProperty] private bool _fbrUseSandbox = true;
    [ObservableProperty] private string _theme = "Light";
    [ObservableProperty] private WarehouseDto? _defaultWarehouse;

    public override async Task InitializeAsync()
    {
        await _exceptionHandler.ExecuteAsync(async () =>
        {
            IsBusy = true;
            _current = await _settingsService.GetSettingsAsync();
            CompanyName = _current.CompanyName;
            Address = _current.Address ?? string.Empty;
            Phone = _current.Phone ?? string.Empty;
            Email = _current.Email ?? string.Empty;
            TaxNumber = _current.Ntn ?? string.Empty;
            Strn = _current.Strn ?? string.Empty;
            PosId = _current.PosId ?? string.Empty;
            FbrUseSandbox = _current.FbrUseSandbox;
            Theme = _current.Theme;
            _themeService.SetDark(string.Equals(Theme, "Dark", StringComparison.OrdinalIgnoreCase));

            Warehouses.Clear();
            foreach (var warehouse in await _warehouseService.GetAllAsync())
                Warehouses.Add(warehouse);
            DefaultWarehouse = Warehouses.FirstOrDefault(w => w.IsDefault) ?? Warehouses.FirstOrDefault();

            IsBusy = false;
        }, "Settings");
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_current is null)
            return;

        await _exceptionHandler.ExecuteAsync(async () =>
        {
            IsBusy = true;
            var dto = _current with
            {
                CompanyName = CompanyName,
                Address = Address,
                Phone = Phone,
                Email = Email,
                Ntn = TaxNumber,
                Strn = string.IsNullOrWhiteSpace(Strn) ? null : Strn.Trim(),
                PosId = string.IsNullOrWhiteSpace(PosId) ? null : PosId.Trim(),
                FbrUseSandbox = FbrUseSandbox,
                Theme = Theme
            };

            var result = await _settingsService.UpdateSettingsAsync(dto);

            if (result.Succeeded)
            {
                _current = dto;
                _themeService.SetDark(string.Equals(Theme, "Dark", StringComparison.OrdinalIgnoreCase));

                if (DefaultWarehouse is not null && !DefaultWarehouse.IsDefault)
                {
                    var warehouseDto = DefaultWarehouse with { IsDefault = true };
                    await _warehouseService.UpdateAsync(DefaultWarehouse.Id, warehouseDto);
                }

                _snackbarService.Show("Settings saved.");
            }
            else
                _snackbarService.Show(result.Error ?? "Save failed.");

            IsBusy = false;
        }, "Save settings");
    }
}
