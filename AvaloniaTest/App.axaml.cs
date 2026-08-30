using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace AvaloniaTest;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var monitor = new Monitor();                 // the shared engine
            desktop.MainWindow = new WidgetWindow(monitor);   // widget is the startup window
        }

        base.OnFrameworkInitializationCompleted();
    }
}