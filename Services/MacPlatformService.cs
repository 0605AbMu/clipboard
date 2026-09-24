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

    private string GetLaunchAgentPlistPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return System.IO.Path.Combine(home, "Library", "LaunchAgents", "com.antigravity.macdesktopapp.plist");
    }

    public bool IsAutoStartEnabled()
    {
        try
        {
            return System.IO.File.Exists(GetLaunchAgentPlistPath());
        }
        catch
        {
            return false;
        }
    }

    public void SetAutoStart(bool enabled)
    {
        try
        {
            var plistPath = GetLaunchAgentPlistPath();
            if (enabled)
            {
                var dir = System.IO.Path.GetDirectoryName(plistPath);
                if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                {
                    System.IO.Directory.CreateDirectory(dir);
                }

                var execPath = Environment.ProcessPath ?? "/Applications/Clipboard.app/Contents/MacOS/MacDesktopApp";
                if (System.IO.File.Exists("/Applications/Clipboard.app/Contents/MacOS/MacDesktopApp"))
                {
                    execPath = "/Applications/Clipboard.app/Contents/MacOS/MacDesktopApp";
                }

                var plistContent = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>Label</key>
    <string>com.antigravity.macdesktopapp</string>
    <key>ProgramArguments</key>
    <array>
        <string>{execPath}</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
    <key>ProcessType</key>
    <string>Interactive</string>
</dict>
</plist>";
                System.IO.File.WriteAllText(plistPath, plistContent);
            }
            else
            {
                if (System.IO.File.Exists(plistPath))
                {
                    System.IO.File.Delete(plistPath);
                }
            }
        }
        catch { }
    }
}

