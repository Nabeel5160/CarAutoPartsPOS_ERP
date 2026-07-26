using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;

namespace CarAutoParts.Presentation.Services;

public class DialogService : IDialogService
{
    private object _dialogHostIdentifier = "RootDialog";

    public void SetDialogHostIdentifier(object identifier) => _dialogHostIdentifier = identifier;

    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var content = new StackPanel { Margin = new Thickness(16) };
        content.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });
        content.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap
        });

        var result = await DialogHost.Show(new DialogContentWrapper(content), _dialogHostIdentifier);
        return result is true;
    }

    public Task ShowMessageAsync(string title, string message)
        => DialogHost.Show(BuildDialog(title, message), _dialogHostIdentifier);

    public Task ShowErrorAsync(string title, string message)
        => DialogHost.Show(BuildDialog(title, message), _dialogHostIdentifier);

    private static UIElement BuildDialog(string title, string message)
    {
        var panel = new StackPanel { Margin = new Thickness(16), MinWidth = 320 };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });
        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap
        });
        return new DialogContentWrapper(panel);
    }

    private sealed class DialogContentWrapper : UserControl
    {
        public DialogContentWrapper(UIElement content)
        {
            Content = content;
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0)
            };

            var ok = new Button
            {
                Content = "OK",
                Style = System.Windows.Application.Current.TryFindResource("MaterialDesignFlatButton") as Style,
                Margin = new Thickness(8, 0, 0, 0),
                IsDefault = true
            };
            ok.Click += (_, _) => DialogHost.CloseDialogCommand.Execute(null, ok);

            if (content is StackPanel panel)
            {
                panel.Children.Add(buttons);
                buttons.Children.Add(ok);
            }
        }
    }
}
