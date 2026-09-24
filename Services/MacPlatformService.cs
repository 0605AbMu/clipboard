using System;
using Avalonia.Controls;

namespace MacDesktopApp.Services;

public class MacPlatformService : IPlatformService
{
    private nint _lastChangeCount = -1;

    public void Initialize(Window window)
    {
        MacNative.SetAsAccessoryApp();
        RemoveWindowDecorations(window);
    }

    public string? GetClipboardText()
    {
        return MacNative.GetPasteboardString();
    }

    public void SetClipboardText(string text)
    {
        MacNative.SetPasteboardString(text);
        _lastChangeCount = MacNative.GetPasteboardChangeCount();
    }

    public bool HasClipboardChanged()
    {
        var current = MacNative.GetPasteboardChangeCount();
        if (current < 0 || current == _lastChangeCount)
        {
            return false;
        }

        _lastChangeCount = current;
        return true;
    }

    public void SimulatePaste()
    {
        MacNative.SimulatePaste();
    }

    public void HideAndDeactivateWindow(Window window)
    {
        window.Hide();
        MacNative.HideAppAndDeactivate();
    }

    public void RemoveWindowDecorations(Window window)
    {
        try
        {
            var handle = window.TryGetPlatformHandle()?.Handle;
            if (handle.HasValue && handle.Value != IntPtr.Zero)
            {
                MacNative.RemoveMinimizeAndMaximizeButtons(handle.Value);
            }
        }
        catch { }
    }

    public bool RegisterGlobalHotkey(Action onTriggered)
    {
        MacNative.HotKeyPressed += onTriggered;
        return MacNative.RegisterGlobalHotkeys();
    }

    public void OpenAccessibilitySettings()
    {
        MacNative.OpenAccessibilitySettings();
    }

    public bool IsAccessibilityGranted()
    {
        return MacNative.IsAccessibilityGranted();
    }
}
