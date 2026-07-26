using System.Windows;
using System.Windows.Controls;
using CarAutoParts.Presentation.ViewModels;

namespace CarAutoParts.Presentation.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is LoginViewModel oldVm)
            oldVm.LoginCompleted -= OnLoginCompleted;

        if (e.NewValue is LoginViewModel newVm)
            newVm.LoginCompleted += OnLoginCompleted;
    }

    private void OnLoginCompleted(bool success)
    {
        if (success)
            DialogResult = true;
    }

    private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm && sender is PasswordBox box)
            vm.Password = box.Password;
    }
}
