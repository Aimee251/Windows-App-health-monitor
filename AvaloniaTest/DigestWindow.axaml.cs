using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AvaloniaTest;

public partial class DigestWindow : Window
{
    public DigestWindow(string text)
    {
        InitializeComponent();
        DigestText.Text = text;
    }

    private void OnDismiss(object? sender, RoutedEventArgs e) => Close();
}