using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AppHealth.Core;

namespace AvaloniaTest;

public partial class WidgetWindow : Window
{
    private readonly Monitor _monitor;
    private MainWindow? _dashboard;

    public WidgetWindow(Monitor monitor)
    {
        InitializeComponent();
        _monitor = monitor;
        _monitor.Updated += Refresh;

        // put it in the top-right corner
        Position = new PixelPoint(1100, 60);
    }

    private void Refresh()
    {
        var worst = _monitor.Triage.FirstOrDefault();
        if (worst is null)
        {
            WorstText.Text = "no apps tracked";
            SevText.Text = "";
            return;
        }

        WorstText.Text = worst.ProcessName;
        SevText.Text = worst.Severity.ToString();
        SevText.Foreground = worst.Severity switch
        {
            Severity.Critical => Brushes.Red,
            Severity.High     => Brushes.Orange,
            Severity.Medium   => Brushes.Gold,
            _                 => Brushes.LightGreen,
        };
    }

    private void OnWidgetClicked(object? sender, PointerPressedEventArgs e)
    {
        if (_dashboard is null || !_dashboard.IsVisible)
        {
            _dashboard = new MainWindow(_monitor);   // pass the shared monitor
            _dashboard.Show();
        }
        else
        {
            _dashboard.Activate();   // bring existing one to front
        }
    }
}