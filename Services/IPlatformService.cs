using System;
using Avalonia.Controls;

namespace MacDesktopApp.Services;

public interface IPlatformService
{
    void Initialize(Window window);

    string? GetClipboardText();

    void SetClipboardText(string text);

    bool HasClipboardChanged();

    void SimulatePaste();

    void HideAndDeactivateWindow(Window window);

    void RemoveWindowDecorations(Window window);

    bool RegisterGlobalHotkey(Action onTriggered);

    void OpenAccessibilitySettings();

    bool IsAccessibilityGranted();

    bool IsAutoStartEnabled();

    void SetAutoStart(bool enabled);
}

