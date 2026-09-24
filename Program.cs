using Avalonia;
using System;
using System.Threading;

namespace MacDesktopApp;

sealed class Program
{
    private static Mutex? _singleInstanceMutex;

    [STAThread]
    public static void Main(string[] args)
    {
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
            // Another instance is already running; avoid hotkey collision and multiple file writes
            return;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
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
