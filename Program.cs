using Avalonia;
using System;
using System.Threading;
using MacDesktopApp.Services;

namespace MacDesktopApp;

sealed class Program
{
    private static Mutex? _singleInstanceMutex;

    [STAThread]
    public static void Main(string[] args)
    {
        string command = "toggle";
        if (args.Length > 0)
        {
            var arg = args[0].TrimStart('-').ToLowerInvariant();
            if (arg == "show" || arg == "s") command = "show";
            else if (arg == "hide" || arg == "h" || arg == "background" || arg == "b" || arg == "autostart") command = "hide";
            else if (arg == "toggle" || arg == "t") command = "toggle";
        }

        // If another instance is already running, notify it and exit immediately
        if (SingleInstanceService.SendCommandIfAlreadyRunning(command))
        {
            return;
        }

        App.StartupCommand = command;

        bool isOnlyInstance;
        try
        {
            _singleInstanceMutex = new Mutex(true, "ClipboardApp_0605AbMu_SingleInstance", out isOnlyInstance);
        }
        catch
        {
            isOnlyInstance = true;
        }

        if (!isOnlyInstance)
        {
            // Another instance is already running
            return;
        }

        // Start listening for commands from CLI or desktop shortcuts
        SingleInstanceService.StartServer(cmd =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (App.Instance is { } app)
                {
                    if (cmd == "show") app.ShowWindow();
                    else if (cmd == "hide") app.HideWindow();
                    else app.ToggleWindow();
                }
            });
        });

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            SingleInstanceService.StopServer();
            try
            {
                _singleInstanceMutex?.ReleaseMutex();
            }
            catch { }
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
