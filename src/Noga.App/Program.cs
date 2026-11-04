using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Noga.Checking;

namespace Noga.App;

/// <summary>The app: Avalonia's Fluent theme, light or dark as the system is.</summary>
sealed class App : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var model = new AppModel((file, profile, progress, token) => Checker.Run(file, null, profile, token, progress));
            model.Add(desktop.Args ?? []);
            desktop.MainWindow = new MainWindow(model);
            desktop.ShutdownRequested += (_, _) => model.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}

/// <summary><c>noga-app [FILE or FOLDER ...]</c>: the window, with those queued (as "Open with" passes them).</summary>
static class Program
{
    [STAThread]
    public static int Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>The app on the platform it runs on (the designer and the tests build their own).</summary>
    // The X11 window's class is the desktop entry's StartupWMClass (packaging/linux/noga-app.desktop), which matches
    // the window to the entry's name and icon in docks and task switchers.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().With(new X11PlatformOptions { WmClass = "noga-app" });
}
