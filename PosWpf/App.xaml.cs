using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using PosWpf.Models;
using PosWpf.Services;
using PosWpf.ViewModels;

namespace PosWpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();

        var fbrSettings = new FbrSettings();
        config.GetSection("Fbr").Bind(fbrSettings);

        var sellerSettings = new SellerSettings();
        config.GetSection("Seller").Bind(sellerSettings);

        var fbrService = new FbrService(fbrSettings);
        var viewModel = new MainViewModel(fbrService, sellerSettings, fbrLive: fbrSettings.HasToken, useSandbox: fbrSettings.UseSandbox);
        viewModel.Initialize();

        var window = new MainWindow { DataContext = viewModel };
        window.Show();
    }
}
