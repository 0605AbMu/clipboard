using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;

namespace MacDesktopApp.Services;

public class WindowsPlatformService : IPlatformService
{
    private const string User32 = "user32.dll";
    private const string Kernel32 = "kernel32.dll";

    #region Win32 P/Invoke Declarations

    [DllImport(User32)]
    private static extern uint GetClipboardSequenceNumber();

    [DllImport(User32)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport(User32)]
    private static extern bool CloseClipboard();

    [DllImport(User32)]
    private static extern bool EmptyClipboard();

    [DllImport(User32)]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport(User32)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport(Kernel32)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport(Kernel32)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport(Kernel32)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport(User32)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport(User32)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport(User32, SetLastError = true)]
    private static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray)] INPUT[] pInputs, int cbSize);

    [DllImport(User32)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport(User32)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport(User32)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport(User32)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public KEYBDINPUT ki;
        private ulong pad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;
    private const int GWL_STYLE = -16;
    private const long WS_MINIMIZEBOX = 0x00020000L;
    private const long WS_MAXIMIZEBOX = 0x00010000L;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const int SW_HIDE = 0;

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_V = 0x56;

    // Hotkey modifiers: MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_ALT = 0x0001;

    #endregion

    private uint _lastSequenceNumber;
    private Window? _window;
    private Action? _onHotkeyTriggered;
    private Thread? _messageLoopThread;

    public void Initialize(Window window)
    {
        _window = window;
        _lastSequenceNumber = GetClipboardSequenceNumber();
        RemoveWindowDecorations(window);
    }

    public string? GetClipboardText()
    {
        if (!OperatingSystem.IsWindows()) return null;

        for (int i = 0; i < 5; i++)
        {
            if (OpenClipboard(IntPtr.Zero))
            {
                try
                {
                    var hData = GetClipboardData(CF_UNICODETEXT);
                    if (hData != IntPtr.Zero)
                    {
                        var ptr = GlobalLock(hData);
                        if (ptr != IntPtr.Zero)
                        {
                            try
                            {
                                return Marshal.PtrToStringUni(ptr);
                            }
                            finally
                            {
                                GlobalUnlock(hData);
                            }
                        }
                    }
                }
                finally
                {
                    CloseClipboard();
                }
                break;
            }
            Thread.Sleep(10);
        }
        return null;
    }

    public void SetClipboardText(string text)
    {
        if (!OperatingSystem.IsWindows()) return;

        for (int i = 0; i < 5; i++)
        {
            if (OpenClipboard(IntPtr.Zero))
            {
                try
                {
                    EmptyClipboard();

                    int bytes = (text.Length + 1) * 2;
                    var hMem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes);
                    if (hMem != IntPtr.Zero)
                    {
                        var ptr = GlobalLock(hMem);
                        if (ptr != IntPtr.Zero)
                        {
                            try
                            {
                                Marshal.Copy(text.ToCharArray(), 0, ptr, text.Length);
                                Marshal.WriteInt16(ptr, text.Length * 2, 0); // null terminator
                            }
                            finally
                            {
                                GlobalUnlock(hMem);
                            }
                            SetClipboardData(CF_UNICODETEXT, hMem);
                        }
                    }
                }
                finally
                {
                    CloseClipboard();
                }
                _lastSequenceNumber = GetClipboardSequenceNumber();
                break;
            }
            Thread.Sleep(10);
        }
    }

    public bool HasClipboardChanged()
    {
        if (!OperatingSystem.IsWindows()) return false;

        var current = GetClipboardSequenceNumber();
        if (current == _lastSequenceNumber || current == 0)
        {
            return false;
        }

        _lastSequenceNumber = current;
        return true;
    }

    public void SimulatePaste()
    {
        if (!OperatingSystem.IsWindows()) return;

        Task.Run(async () =>
        {
            // Allow active target window to regain focus
            await Task.Delay(80);

            var inputs = new INPUT[4];

            // 1. Ctrl Down
            inputs[0] = new INPUT
            {
                type = INPUT_KEYBOARD,
                ki = new KEYBDINPUT { wVk = VK_CONTROL }
            };

            // 2. V Down
            inputs[1] = new INPUT
            {
                type = INPUT_KEYBOARD,
                ki = new KEYBDINPUT { wVk = VK_V }
            };

            // 3. V Up
            inputs[2] = new INPUT
            {
                type = INPUT_KEYBOARD,
                ki = new KEYBDINPUT { wVk = VK_V, dwFlags = KEYEVENTF_KEYUP }
            };

            // 4. Ctrl Up
            inputs[3] = new INPUT
            {
                type = INPUT_KEYBOARD,
                ki = new KEYBDINPUT { wVk = VK_CONTROL, dwFlags = KEYEVENTF_KEYUP }
            };

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        });
    }

    public void HideAndDeactivateWindow(Window window)
    {
        window.Hide();
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var handle = window.TryGetPlatformHandle()?.Handle;
                if (handle.HasValue && handle.Value != IntPtr.Zero)
                {
                    ShowWindow(handle.Value, SW_HIDE);
                }
            }
            catch { }
        }
    }

    public void RemoveWindowDecorations(Window window)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            var handle = window.TryGetPlatformHandle()?.Handle;
            if (handle.HasValue && handle.Value != IntPtr.Zero)
            {
                var style = (long)GetWindowLongPtr(handle.Value, GWL_STYLE);
                style &= ~WS_MINIMIZEBOX;
                style &= ~WS_MAXIMIZEBOX;
                SetWindowLongPtr(handle.Value, GWL_STYLE, (IntPtr)style);
                SetWindowPos(handle.Value, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
            }
        }
        catch { }
    }

    public bool RegisterGlobalHotkey(Action onTriggered)
    {
        if (!OperatingSystem.IsWindows()) return false;

        _onHotkeyTriggered = onTriggered;

        // Dedicated message pump thread for global hotkeys
        _messageLoopThread = new Thread(() =>
        {
            try
            {
                // Register Ctrl+Shift+V and Alt+V
                RegisterHotKey(IntPtr.Zero, 1, MOD_CONTROL | MOD_SHIFT, VK_V);
                RegisterHotKey(IntPtr.Zero, 2, MOD_ALT, VK_V);

                [DllImport(User32)]
                static extern sbyte GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

                [DllImport(User32)]
                static extern bool TranslateMessage(ref MSG lpMsg);

                [DllImport(User32)]
                static extern IntPtr DispatchMessage(ref MSG lpMsg);

                const uint WM_HOTKEY = 0x0312;

                while (GetMessage(out MSG msg, IntPtr.Zero, 0, 0) > 0)
                {
                    if (msg.message == WM_HOTKEY)
                    {
                        Dispatcher.UIThread.Post(() => _onHotkeyTriggered?.Invoke());
                    }
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }

                UnregisterHotKey(IntPtr.Zero, 1);
                UnregisterHotKey(IntPtr.Zero, 2);
            }
            catch { }
        })
        {
            IsBackground = true,
            Name = "WindowsGlobalHotkeyThread"
        };

        _messageLoopThread.Start();
        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int pt_x;
        public int pt_y;
    }

    public void OpenAccessibilitySettings() { }

    public bool IsAccessibilityGranted() => true; // Windows allows SendInput without accessibility permissions
}
