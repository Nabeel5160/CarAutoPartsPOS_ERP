using System.Windows;
using CarAutoParts.Presentation.Services;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;

namespace CarAutoParts.Presentation.Views;

public partial class ShellWindow : Window
{
    public ShellWindow()
    {
        InitializeComponent();
        FitToScreen();
        Loaded += OnLoaded;
    }

    private void FitToScreen()
    {
        var workArea = SystemParameters.WorkArea;
        Width = Math.Min(1280, workArea.Width);
        Height = Math.Min(800, workArea.Height);
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Top + (workArea.Height - Height) / 2;
        WindowState = WindowState.Maximized;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (App.Services?.GetService<ISnackbarService>() is SnackbarService snackbarService
            && FindName("MainSnackbar") is Snackbar snackbar)
        {
            snackbar.MessageQueue = snackbarService.MessageQueue;
        }

        if (App.Services?.GetService<IDialogService>() is DialogService dialogService)
            dialogService.SetDialogHostIdentifier("RootDialog");
    }
}
