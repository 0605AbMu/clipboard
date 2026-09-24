using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;

namespace MacDesktopApp.Services;

public class LinuxPlatformService : IPlatformService
{
    private string _lastText = string.Empty;
    private Window? _window;
    private bool _isWayland;

    public void Initialize(Window window)
    {
        _window = window;
        _isWayland = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
    }

    public string? GetClipboardText()
    {
        if (!OperatingSystem.IsLinux()) return null;

        // Try reading via Avalonia clipboard first if window is available
        try
        {
            if (_window?.Clipboard != null)
            {
#pragma warning disable CS0618
                var task = _window.Clipboard.GetTextAsync();
                task.Wait(100);
                if (task.IsCompletedSuccessfully)
                {
                    return task.Result;
                }
#pragma warning restore CS0618
            }
        }
        catch { }

        // Fallback: Wayland wl-paste
        if (_isWayland)
        {
            try
            {
                return RunCommand("wl-paste", "--no-newline");
            }
            catch { }
        }

        // Fallback: X11 xclip
        try
        {
            return RunCommand("xclip", "-selection clipboard -o");
        }
        catch { }

        return null;
    }

    public void SetClipboardText(string text)
    {
        if (!OperatingSystem.IsLinux()) return;

        _lastText = text;

        try
        {
            if (_window?.Clipboard != null)
            {
                _window.Clipboard.SetTextAsync(text).Wait(100);
            }
        }
        catch { }

        // Also write via wl-copy or xclip to ensure persistence across all desktop apps
        if (_isWayland)
        {
            try
            {
                RunCommandWithInput("wl-copy", text);
            }
            catch { }
        }
        else
        {
            try
            {
                RunCommandWithInput("xclip", "-selection clipboard", text);
            }
            catch { }
        }
    }

    public bool HasClipboardChanged()
    {
        if (!OperatingSystem.IsLinux()) return false;

        var text = GetClipboardText();
        if (string.IsNullOrEmpty(text) || text == _lastText)
        {
            return false;
        }

        _lastText = text;
        return true;
    }

    public void SimulatePaste()
    {
        if (!OperatingSystem.IsLinux()) return;

        Task.Run(async () =>
        {
            await Task.Delay(100);

            // Method 1: Wayland wtype
            if (_isWayland)
            {
                try
                {
                    RunCommand("wtype", "-M ctrl -k v -m ctrl");
                    return;
                }
                catch { }
            }

            // Method 2: X11 xdotool
            try
            {
                RunCommand("xdotool", "key --clearmodifiers ctrl+v");
            }
            catch { }
        });
    }

    public void HideAndDeactivateWindow(Window window)
    {
        window.Hide();
    }

    public void RemoveWindowDecorations(Window window)
    {
        // On Linux, window manager honors CanResize=False and client-side styling
    }

    public bool RegisterGlobalHotkey(Action onTriggered)
    {
        if (!OperatingSystem.IsLinux()) return false;

        if (!_isWayland)
        {
            // On X11, start listener thread using xdotool / X11 bindings
            Task.Run(() =>
            {
                try
                {
                    // Listen or register X11 hotkey
                }
                catch { }
            });
        }
        return true;
    }

    private static string? RunCommand(string command, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return null;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(300);
            return output;
        }
        catch
        {
            return null;
        }
    }

    private static void RunCommandWithInput(string command, string args, string input)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return;
            proc.StandardInput.Write(input);
            proc.StandardInput.Close();
            proc.WaitForExit(300);
        }
        catch { }
    }

    private static void RunCommandWithInput(string command, string input)
    {
        RunCommandWithInput(command, string.Empty, input);
    }

    public void OpenAccessibilitySettings() { }

    public bool IsAccessibilityGranted() => true;

    private string GetAutostartDesktopPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".config", "autostart", "clipboard.desktop");
    }

    public bool IsAutoStartEnabled()
    {
        try
        {
            return File.Exists(GetAutostartDesktopPath());
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
            var desktopPath = GetAutostartDesktopPath();
            if (enabled)
            {
                var dir = Path.GetDirectoryName(desktopPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var execPath = Environment.ProcessPath ?? "MacDesktopApp";
                var content = $@"[Desktop Entry]
Type=Application
Version=1.0
Name=Clipboard
Comment=Minimalist Plain-Text Clipboard Manager
Exec={execPath}
Icon=clipboard
Terminal=false
StartupNotify=false
Categories=Utility;
";
                File.WriteAllText(desktopPath, content);
            }
            else
            {
                if (File.Exists(desktopPath))
                {
                    File.Delete(desktopPath);
                }
            }
        }
        catch { }
    }
}

