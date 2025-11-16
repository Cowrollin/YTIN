using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace YTIN;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Exit += (sender, args) => OnAppExitAsync();
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void OnAppExitAsync()
    {
        try
        {
            AppConfig.Save();
        }
        catch (Exception ex)
        {
            AppLog.Write($"Error while saving config: {ex.Message}", "ERROR");
        }
    }
}