using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using MacDesktopApp.Services;
using MacDesktopApp.ViewModels;
using MacDesktopApp.Views;

namespace MacDesktopApp;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private MainWindowViewModel? _viewModel;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            // Prevent app from quitting when MainWindow is hidden
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _viewModel = new MainWindowViewModel();
            _mainWindow = new MainWindow
            {
                DataContext = _viewModel
            };
            desktop.MainWindow = _mainWindow;

            // Initialize cross-platform service
            PlatformService.Current.Initialize(_mainWindow);

            // Setup native Menu Bar / System Tray status icon
            SetupTrayIcon(desktop);

            // Register global hotkeys across OS
            PlatformService.Current.RegisterGlobalHotkey(() =>
            {
                Dispatcher.UIThread.Post(ToggleWindow);
            });

            // Initially show window with focus
            _mainWindow.Show();
            _mainWindow.Activate();
            _mainWindow.Focus();
            _mainWindow.FocusAndPrepare();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            var menu = new NativeMenu();

            var openItem = new NativeMenuItem("📋 Clipboard Tarixi");
            openItem.Click += (s, e) => ToggleWindow();
            menu.Add(openItem);

            menu.Add(new NativeMenuItemSeparator());

            var clearItem = new NativeMenuItem("🧹 Qadalmaganlarni tozalash");
            clearItem.Click += (s, e) => _viewModel?.ClearAll();
            menu.Add(clearItem);

            menu.Add(new NativeMenuItemSeparator());

            var exitItem = new NativeMenuItem("❌ Chiqish");
            exitItem.Click += (s, e) => desktop.Shutdown();
            menu.Add(exitItem);

            var trayIcon = new TrayIcon
            {
                ToolTipText = "Clipboard Menejeri",
                Menu = menu,
                IsVisible = true
            };

            try
            {
                var iconStream = AssetLoader.Open(new Uri("avares://MacDesktopApp/Assets/avalonia-logo.ico"));
                trayIcon.Icon = new WindowIcon(iconStream);
            }
            catch { }

            var trayIcons = new TrayIcons { trayIcon };
            TrayIcon.SetIcons(this, trayIcons);
        }
        catch { }
    }

    public void ToggleWindow()
    {
        if (_mainWindow == null) return;

        if (_mainWindow.IsVisible)
        {
            PlatformService.Current.HideAndDeactivateWindow(_mainWindow);
        }
        else
        {
            if (OperatingSystem.IsMacOS())
            {
                MacNative.ActivateApp();
            }

            _mainWindow.Show();
            _mainWindow.Activate();
            _mainWindow.Focus();
            _mainWindow.FocusAndPrepare();
        }
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}