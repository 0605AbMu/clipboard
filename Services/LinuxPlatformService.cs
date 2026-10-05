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
    private readonly object _clipLock = new();
    private string _cachedText = string.Empty;
    private string _lastReportedText = string.Empty;
    private volatile bool _hasChanged = false;

    private Window? _window;
    private readonly bool _isWayland = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
    private CancellationTokenSource? _clipboardWatcherCts;
    private Action? _onHotkeyTriggered;
    private Thread? _x11HotkeyThread;
    private CancellationTokenSource? _x11HotkeyCts;

    public void Initialize(Window window)
    {
        _window = window;

        // Initialize cache with current clipboard content
        var initial = FetchSystemClipboard();
        if (!string.IsNullOrEmpty(initial))
        {
            _cachedText = initial;
            _lastReportedText = initial;
        }

        StartClipboardWatcher();
    }

    private void StartClipboardWatcher()
    {
        _clipboardWatcherCts = new CancellationTokenSource();
        var token = _clipboardWatcherCts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var text = FetchSystemClipboard();
                    if (!string.IsNullOrEmpty(text))
                    {
                        lock (_clipLock)
                        {
                            if (text != _lastReportedText)
                            {
                                _cachedText = text;
                                _hasChanged = true;
                            }
                        }
                    }
                }
                catch { }

                try
                {
                    // Polling interval using lightweight xclip X11 query - zero Wayland compositor spam
                    await Task.Delay(1500, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, token);
    }

    private string? FetchSystemClipboard()
    {
        // Query X11 clipboard via Xwayland using standard UTF-8 targets without invoking wl-paste.
        // GNOME Mutter automatically bridges Wayland selections to Xwayland.
        // Avoiding wl-paste avoids creating transient Wayland subsurfaces that crash Mutter window stacking tracker.
        var utf8 = RunCommand("xclip", "-selection", "clipboard", "-t", "UTF8_STRING", "-o");
        if (!string.IsNullOrEmpty(utf8)) return utf8;

        var textPlain = RunCommand("xclip", "-selection", "clipboard", "-t", "text/plain", "-o");
        if (!string.IsNullOrEmpty(textPlain)) return textPlain;

        var str = RunCommand("xclip", "-selection", "clipboard", "-t", "STRING", "-o");
        if (!string.IsNullOrEmpty(str)) return str;

        return null;
    }

    public string? GetClipboardText()
    {
        if (!OperatingSystem.IsLinux()) return null;

        lock (_clipLock)
        {
            if (!string.IsNullOrEmpty(_cachedText))
            {
                return _cachedText;
            }
        }

        return FetchSystemClipboard();
    }

    public void SetClipboardText(string text)
    {
        if (!OperatingSystem.IsLinux()) return;

        lock (_clipLock)
        {
            _cachedText = text;
            _lastReportedText = text;
            _hasChanged = false;
        }

        try
        {
            _window?.Clipboard?.SetTextAsync(text);
        }
        catch { }

        // Write via wl-copy and xclip in background task so UI thread never blocks
        Task.Run(() =>
        {
            if (_isWayland)
            {
                try
                {
                    RunCommandWithInput("wl-copy", Array.Empty<string>(), text);
                    RunCommandWithInput("wl-copy", new[] { "--primary" }, text);
                }
                catch { }
            }

            try
            {
                RunCommandWithInput("xclip", new[] { "-selection", "clipboard" }, text);
                RunCommandWithInput("xclip", new[] { "-selection", "primary" }, text);
            }
            catch { }
        });
    }

    public bool HasClipboardChanged()
    {
        if (!OperatingSystem.IsLinux()) return false;

        lock (_clipLock)
        {
            if (_hasChanged)
            {
                _hasChanged = false;
                _lastReportedText = _cachedText;
                return true;
            }
            return false;
        }
    }

    public void SimulatePaste()
    {
        if (!OperatingSystem.IsLinux()) return;

        Task.Run(async () =>
        {
            // Delay to allow target window to regain focus after clipboard manager hides
            await Task.Delay(250);

            // 1. GNOME Shell Extension D-Bus bridge (primary native Wayland method)
            if (_isWayland && TryExtensionSimulatePaste())
            {
                return;
            }

            // 2. GNOME Mutter RemoteDesktop D-Bus keyboard injection (hardware evdev keycodes)
            if (_isWayland && TryMutterSimulatePaste())
            {
                return;
            }

            // 3. Fallback to xdotool on X11
            try
            {
                RunCommand("xdotool", "key", "--clearmodifiers", "ctrl+v");
            }
            catch { }
        });
    }

    private static bool TryExtensionSimulatePaste()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "gdbus",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("call");
            psi.ArgumentList.Add("--session");
            psi.ArgumentList.Add("--dest");
            psi.ArgumentList.Add("org.gnome.Shell");
            psi.ArgumentList.Add("--object-path");
            psi.ArgumentList.Add("/org/gnome/Shell/Extensions/ClipboardBridge");
            psi.ArgumentList.Add("--method");
            psi.ArgumentList.Add("org.gnome.Shell.Extensions.ClipboardBridge.Paste");

            using var proc = Process.Start(psi);
            if (proc == null) return false;
            proc.WaitForExit(350);
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryMutterSimulatePaste()
    {
        try
        {
            // Hardware evdev keycodes: KEY_LEFTCTRL = 29, KEY_V = 47
            var script = "import dbus, time; b=dbus.SessionBus(); m=b.get_object('org.gnome.Mutter.RemoteDesktop','/org/gnome/Mutter/RemoteDesktop'); sp=dbus.Interface(m,'org.gnome.Mutter.RemoteDesktop').CreateSession(); s=dbus.Interface(b.get_object('org.gnome.Mutter.RemoteDesktop',sp),'org.gnome.Mutter.RemoteDesktop.Session'); s.Start(); s.NotifyKeyboardKeycode(dbus.UInt32(29),True); time.sleep(0.03); s.NotifyKeyboardKeycode(dbus.UInt32(47),True); time.sleep(0.03); s.NotifyKeyboardKeycode(dbus.UInt32(47),False); time.sleep(0.03); s.NotifyKeyboardKeycode(dbus.UInt32(29),False); time.sleep(0.03); s.Stop()";

            var psi = new ProcessStartInfo
            {
                FileName = "python3",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add(script);

            using var proc = Process.Start(psi);
            if (proc == null) return false;
            proc.WaitForExit(700);
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public void HideAndDeactivateWindow(Window window)
    {
        window.Hide();
    }

    public void RemoveWindowDecorations(Window window)
    {
        // Window manager honors CanResize=False and client-side border styling
    }

    public bool RegisterGlobalHotkey(Action onTriggered)
    {
        if (!OperatingSystem.IsLinux()) return false;

        _onHotkeyTriggered = onTriggered;

        // 1. Configure GNOME media-keys custom shortcut for Wayland / GNOME desktops
        ConfigureGnomeShortcuts();
        EnsureExtensionEnabled();

        // 2. Start X11 XGrabKey listener thread ONLY on pure X11 (never on Wayland!)
        if (!_isWayland)
        {
            StartX11HotkeyListener();
        }

        return true;
    }

    private static void EnsureExtensionEnabled()
    {
        Task.Run(() =>
        {
            try
            {
                RunCommand("gnome-extensions", "enable", "clipboard-bridge@0605AbMu");
            }
            catch { }
        });
    }

    private static void ConfigureGnomeShortcuts()
    {
        Task.Run(() =>
        {
            try
            {
                if (!File.Exists("/usr/bin/gsettings")) return;

                var execPath = File.Exists("/usr/bin/clipboard") ? "/usr/bin/clipboard" : (Environment.ProcessPath ?? "clipboard");
                var toggleCmd = $"{execPath} --toggle";

                var currentListRaw = RunCommand("gsettings", "get", "org.gnome.settings-daemon.plugins.media-keys", "custom-keybindings");
                if (string.IsNullOrEmpty(currentListRaw)) return;

                // Configure Ctrl+Shift+V
                string path1 = "/org/gnome/settings-daemon/plugins/media-keys/custom-keybindings/clipboard-toggle/";
                string schema1 = $"org.gnome.settings-daemon.plugins.media-keys.custom-keybinding:{path1}";
                RunCommand("gsettings", "set", schema1, "name", "Clipboard Manager");
                RunCommand("gsettings", "set", schema1, "command", toggleCmd);
                RunCommand("gsettings", "set", schema1, "binding", "<Primary><Shift>v");

                // Configure Alt+V
                string path2 = "/org/gnome/settings-daemon/plugins/media-keys/custom-keybindings/clipboard-toggle-alt/";
                string schema2 = $"org.gnome.settings-daemon.plugins.media-keys.custom-keybinding:{path2}";
                RunCommand("gsettings", "set", schema2, "name", "Clipboard Manager (Alt+V)");
                RunCommand("gsettings", "set", schema2, "command", toggleCmd);
                RunCommand("gsettings", "set", schema2, "binding", "<Alt>v");

                // Add to custom-keybindings array if not present
                bool hasPath1 = currentListRaw.Contains("clipboard-toggle/");
                bool hasPath2 = currentListRaw.Contains("clipboard-toggle-alt/");

                if (!hasPath1 || !hasPath2)
                {
                    string trimmed = currentListRaw.Trim();
                    string listBody = "";
                    if (trimmed != "@as []" && trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        listBody = trimmed.Substring(1, trimmed.Length - 2).Trim();
                    }

                    if (!hasPath1)
                    {
                        listBody = string.IsNullOrEmpty(listBody) ? $"'{path1}'" : $"{listBody}, '{path1}'";
                    }
                    if (!hasPath2)
                    {
                        listBody = string.IsNullOrEmpty(listBody) ? $"'{path2}'" : $"{listBody}, '{path2}'";
                    }

                    RunCommand("gsettings", "set", "org.gnome.settings-daemon.plugins.media-keys", "custom-keybindings", $"[{listBody}]");
                }
            }
            catch { }
        });
    }

    private void StartX11HotkeyListener()
    {
        var displayEnv = Environment.GetEnvironmentVariable("DISPLAY");
        if (string.IsNullOrEmpty(displayEnv)) return;

        _x11HotkeyCts = new CancellationTokenSource();
        var token = _x11HotkeyCts.Token;

        _x11HotkeyThread = new Thread(() =>
        {
            IntPtr display = IntPtr.Zero;
            try
            {
                display = X11Native.XOpenDisplay(null);
                if (display == IntPtr.Zero) return;

                // Install dummy error handler to prevent crashing if another app grabbed the key
                X11Native.XSetErrorHandler((IntPtr d, ref X11Native.XErrorEvent ev) => 0);

                IntPtr root = X11Native.XDefaultRootWindow(display);
                IntPtr keysymV = X11Native.XStringToKeysym("v");
                int keycodeV = X11Native.XKeysymToKeycode(display, keysymV);

                if (keycodeV == 0) return;

                // Hotkey 1: Ctrl + Shift + V
                // Hotkey 2: Alt + V
                uint[] baseMods = { X11Native.ControlMask | X11Native.ShiftMask, X11Native.Mod1Mask };
                uint[] extraMasks = { 0, X11Native.Mod2Mask, X11Native.LockMask, X11Native.Mod2Mask | X11Native.LockMask };

                foreach (var baseMod in baseMods)
                {
                    foreach (var extra in extraMasks)
                    {
                        X11Native.XGrabKey(display, keycodeV, baseMod | extra, root, true, X11Native.GrabModeAsync, X11Native.GrabModeAsync);
                    }
                }

                X11Native.XEvent xevent = new();
                while (!token.IsCancellationRequested)
                {
                    if (X11Native.XPending(display) > 0)
                    {
                        X11Native.XNextEvent(display, ref xevent);
                        if (xevent.type == X11Native.KeyPress && xevent.keycode == (uint)keycodeV)
                        {
                            Dispatcher.UIThread.Post(() => _onHotkeyTriggered?.Invoke());
                        }
                    }
                    else
                    {
                        Thread.Sleep(50);
                    }
                }

                // Clean ungrab
                foreach (var baseMod in baseMods)
                {
                    foreach (var extra in extraMasks)
                    {
                        X11Native.XUngrabKey(display, keycodeV, baseMod | extra, root);
                    }
                }
            }
            catch { }
            finally
            {
                if (display != IntPtr.Zero)
                {
                    try { X11Native.XCloseDisplay(display); } catch { }
                }
            }
        })
        {
            IsBackground = true,
            Name = "LinuxX11GlobalHotkeyThread"
        };

        _x11HotkeyThread.Start();
    }

    private static string? RunCommand(string command, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var arg in args)
            {
                psi.ArgumentList.Add(arg);
            }
            using var proc = Process.Start(psi);
            if (proc == null) return null;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(400);
            return output;
        }
        catch
        {
            return null;
        }
    }

    private static void RunCommandWithInput(string command, string[] args, string input)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var arg in args)
            {
                psi.ArgumentList.Add(arg);
            }
            using var proc = Process.Start(psi);
            if (proc == null) return;
            proc.StandardInput.Write(input);
            proc.StandardInput.Close();
            proc.WaitForExit(400);
        }
        catch { }
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

                var execPath = File.Exists("/usr/bin/clipboard") ? "/usr/bin/clipboard" : (Environment.ProcessPath ?? "clipboard");
                var content = $@"[Desktop Entry]
Type=Application
Version=1.0
Name=Clipboard
Comment=Minimalist Plain-Text Clipboard Manager
Exec={execPath} --background
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

    private static class X11Native
    {
        private const string LibX11 = "libX11.so.6";

        [DllImport(LibX11)]
        public static extern IntPtr XOpenDisplay(string? display_name);

        [DllImport(LibX11)]
        public static extern int XCloseDisplay(IntPtr display);

        [DllImport(LibX11)]
        public static extern IntPtr XDefaultRootWindow(IntPtr display);

        [DllImport(LibX11)]
        public static extern IntPtr XStringToKeysym(string str);

        [DllImport(LibX11)]
        public static extern byte XKeysymToKeycode(IntPtr display, IntPtr keysym);

        [DllImport(LibX11)]
        public static extern int XGrabKey(IntPtr display, int keycode, uint modifiers, IntPtr grab_window, bool owner_events, int pointer_mode, int keyboard_mode);

        [DllImport(LibX11)]
        public static extern int XUngrabKey(IntPtr display, int keycode, uint modifiers, IntPtr grab_window);

        [DllImport(LibX11)]
        public static extern int XSelectInput(IntPtr display, IntPtr window, IntPtr event_mask);

        [DllImport(LibX11)]
        public static extern int XPending(IntPtr display);

        [DllImport(LibX11)]
        public static extern int XNextEvent(IntPtr display, ref XEvent xevent);

        [DllImport(LibX11)]
        public static extern IntPtr XSetErrorHandler(XErrorHandler handler);

        public delegate int XErrorHandler(IntPtr display, ref XErrorEvent error_event);

        public const int GrabModeAsync = 1;
        public const int KeyPress = 2;
        public static readonly IntPtr KeyPressMask = new(1);

        public const uint ShiftMask = 1 << 0;
        public const uint LockMask = 1 << 1;    // CapsLock
        public const uint ControlMask = 1 << 2;
        public const uint Mod1Mask = 1 << 3;   // Alt
        public const uint Mod2Mask = 1 << 4;   // NumLock

        [StructLayout(LayoutKind.Explicit, Size = 192)]
        public struct XEvent
        {
            [FieldOffset(0)]
            public int type;
            [FieldOffset(8)]
            public IntPtr serial;
            [FieldOffset(16)]
            public bool send_event;
            [FieldOffset(24)]
            public IntPtr display;
            [FieldOffset(32)]
            public IntPtr window;
            [FieldOffset(40)]
            public IntPtr root;
            [FieldOffset(48)]
            public IntPtr subwindow;
            [FieldOffset(56)]
            public IntPtr time;
            [FieldOffset(64)]
            public int x;
            [FieldOffset(68)]
            public int y;
            [FieldOffset(72)]
            public int x_root;
            [FieldOffset(76)]
            public int y_root;
            [FieldOffset(80)]
            public uint state;
            [FieldOffset(84)]
            public uint keycode;
            [FieldOffset(88)]
            public bool same_screen;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct XErrorEvent
        {
            public int type;
            public IntPtr display;
            public IntPtr resourceid;
            public IntPtr serial;
            public byte error_code;
            public byte request_code;
            public byte minor_code;
        }
    }
}
