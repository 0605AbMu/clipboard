using System;
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
            var handle = TryGetPlatformHandle()?.Handle;
            if (handle.HasValue && handle.Value != IntPtr.Zero)
            {
                MacNative.RemoveMinimizeAndMaximizeButtons(handle.Value);
            }
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
        Hide();
        MacNative.HideAppAndDeactivate();
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        e.Cancel = true;
        Hide();
        MacNative.HideAppAndDeactivate();
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        Hide();
        MacNative.HideAppAndDeactivate();
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

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
        else if (e.Key == Key.Enter)
        {
            if (vm.SelectedItem != null)
            {
                vm.SelectAndCopy(vm.SelectedItem);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Hide();
            MacNative.HideAppAndDeactivate();
            e.Handled = true;
        }
    }

    private void OnListBoxTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Button || (e.Source as Control)?.Parent is Button)
        {
            return;
        }

        if (DataContext is MainWindowViewModel vm && vm.SelectedItem != null)
        {
            vm.SelectAndCopy(vm.SelectedItem);
        }
    }

    public void FocusAndPrepare()
    {
        ApplyNativeWindowStyle();

        if (DataContext is MainWindowViewModel vm)
        {
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