using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MacDesktopApp.Models;
using MacDesktopApp.Services;
using MacDesktopApp.ViewModels;

namespace MacDesktopApp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Closing += OnWindowClosing;
        Deactivated += OnWindowDeactivated;

        // Tunnel strategy intercepts Up, Down, Enter, Esc before any child control (like TextBox) consumes them
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);

        if (OperatingSystem.IsLinux())
        {
            Topmost = false;
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent, WindowTransparencyLevel.None };
            if (MacAcrylicBorder != null)
            {
                MacAcrylicBorder.IsVisible = false;
            }
        }

        if (ItemsListBox != null)
        {
            ItemsListBox.Tapped += OnListBoxTapped;
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        ApplyNativeWindowStyle();
    }

    public void ApplyNativeWindowStyle()
    {
        try
        {
            if (OperatingSystem.IsLinux() && MacAcrylicBorder != null)
            {
                MacAcrylicBorder.IsVisible = false;
            }
            PlatformService.Current.RemoveWindowDecorations(this);
        }
        catch { }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainWindowViewModel vm)
        {
            vm.RequestHideWindow -= OnRequestHide;
            vm.RequestHideWindow += OnRequestHide;
        }
    }

    private void OnRequestHide()
    {
        PlatformService.Current.HideAndDeactivateWindow(this);
    }

    private DateTime _lastShownTime = DateTime.MinValue;

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        e.Cancel = true;
        PlatformService.Current.HideAndDeactivateWindow(this);
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        // Prevent accidental immediate hide during window mapping/activation (< 350ms)
        if ((DateTime.UtcNow - _lastShownTime).TotalMilliseconds < 350)
        {
            return;
        }

        PlatformService.Current.HideAndDeactivateWindow(this);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        // Number shortcuts: Alt+1..9 or Ctrl+1..9
        if (e.Key >= Key.D1 && e.Key <= Key.D9 && 
            (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Alt)))
        {
            int index = (int)(e.Key - Key.D1);
            if (index < vm.FilteredItems.Count)
            {
                vm.SelectAndCopy(vm.FilteredItems[index]);
                e.Handled = true;
                return;
            }
        }

        // Pin/Unpin shortcut: Ctrl+P
        if (e.Key == Key.P && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (vm.SelectedItem != null)
            {
                vm.TogglePin(vm.SelectedItem);
                e.Handled = true;
                return;
            }
        }

        // Delete shortcut: Ctrl+Delete or Ctrl+D or Delete when SearchInput is empty
        if ((e.Key == Key.D && e.KeyModifiers.HasFlag(KeyModifiers.Control)) ||
            (e.Key == Key.Delete && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || string.IsNullOrEmpty(vm.SearchText))))
        {
            if (vm.SelectedItem != null)
            {
                vm.DeleteItem(vm.SelectedItem);
                e.Handled = true;
                return;
            }
        }

        // Window toggle/close shortcut while focused: Ctrl+Shift+V or Alt+V
        if ((e.Key == Key.V && e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift)) ||
            (e.Key == Key.V && e.KeyModifiers.HasFlag(KeyModifiers.Alt)))
        {
            PlatformService.Current.HideAndDeactivateWindow(this);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down)
        {
            if (vm.FilteredItems.Count > 0)
            {
                int currentIndex = vm.SelectedItem != null ? vm.FilteredItems.IndexOf(vm.SelectedItem) : -1;
                int nextIndex = Math.Min(currentIndex + 1, vm.FilteredItems.Count - 1);
                vm.SelectedItem = vm.FilteredItems[nextIndex];
                ItemsListBox?.ScrollIntoView(vm.SelectedItem);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            if (vm.FilteredItems.Count > 0)
            {
                int currentIndex = vm.SelectedItem != null ? vm.FilteredItems.IndexOf(vm.SelectedItem) : 1;
                int prevIndex = Math.Max(currentIndex - 1, 0);
                vm.SelectedItem = vm.FilteredItems[prevIndex];
                ItemsListBox?.ScrollIntoView(vm.SelectedItem);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.PageDown)
        {
            if (vm.FilteredItems.Count > 0)
            {
                int currentIndex = vm.SelectedItem != null ? vm.FilteredItems.IndexOf(vm.SelectedItem) : 0;
                int nextIndex = Math.Min(currentIndex + 5, vm.FilteredItems.Count - 1);
                vm.SelectedItem = vm.FilteredItems[nextIndex];
                ItemsListBox?.ScrollIntoView(vm.SelectedItem);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.PageUp)
        {
            if (vm.FilteredItems.Count > 0)
            {
                int currentIndex = vm.SelectedItem != null ? vm.FilteredItems.IndexOf(vm.SelectedItem) : 0;
                int prevIndex = Math.Max(currentIndex - 5, 0);
                vm.SelectedItem = vm.FilteredItems[prevIndex];
                ItemsListBox?.ScrollIntoView(vm.SelectedItem);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            var item = vm.SelectedItem ?? vm.FilteredItems.FirstOrDefault();
            if (item != null)
            {
                vm.SelectAndCopy(item);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (!string.IsNullOrEmpty(vm.SearchText))
            {
                vm.SearchText = string.Empty;
            }
            else
            {
                PlatformService.Current.HideAndDeactivateWindow(this);
            }
            e.Handled = true;
        }
    }

    private void OnListBoxTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Button || (e.Source as Control)?.Parent is Button)
        {
            return;
        }

        if (DataContext is MainWindowViewModel vm)
        {
            var item = vm.SelectedItem ?? vm.FilteredItems.FirstOrDefault();
            if (item != null)
            {
                vm.SelectAndCopy(item);
            }
        }
    }

    public void FocusAndPrepare()
    {
        _lastShownTime = DateTime.UtcNow;
        ApplyNativeWindowStyle();

        if (DataContext is MainWindowViewModel vm)
        {
            vm.RefreshClipboard();
            vm.CheckAccessibility();
            if (vm.SelectedItem == null && vm.FilteredItems.Count > 0)
            {
                vm.SelectedItem = vm.FilteredItems[0];
            }
        }

        Dispatcher.UIThread.Post(() =>
        {
            SearchInput?.Focus();
            SearchInput?.SelectAll();
        }, DispatcherPriority.Input);
    }
}