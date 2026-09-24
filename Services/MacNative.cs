using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace MacDesktopApp.Services;

public static class MacNative
{
    private const string ObjCLib = "/usr/lib/libobjc.A.dylib";
    private const string CarbonLib = "/System/Library/Frameworks/Carbon.framework/Carbon";
    private const string CoreGraphicsLib = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string CoreFoundationLib = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string AppServicesLib = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";

    [DllImport(ObjCLib)]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(ObjCLib)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr self, IntPtr op);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend_nint(IntPtr self, IntPtr op);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern bool objc_msgSend_bool_nint(IntPtr self, IntPtr op, nint arg);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern bool objc_msgSend_bool_bool(IntPtr self, IntPtr op, bool arg);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void_bool(IntPtr self, IntPtr op, bool arg);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr_nint(IntPtr self, IntPtr op, nint arg);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr_bool(IntPtr self, IntPtr op, bool arg);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr_IntPtr(IntPtr self, IntPtr op, IntPtr arg);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr_IntPtr_IntPtr(IntPtr self, IntPtr op, IntPtr arg1, IntPtr arg2);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr_string(IntPtr self, IntPtr op, [MarshalAs(UnmanagedType.LPUTF8Str)] string arg);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern bool objc_msgSend_bool_IntPtr_IntPtr(IntPtr self, IntPtr op, IntPtr arg1, IntPtr arg2);

    #region CoreGraphics Input Simulation (Cmd + V)

    [DllImport(CoreGraphicsLib)]
    private static extern IntPtr CGEventSourceCreate(int stateID);

    [DllImport(CoreGraphicsLib)]
    private static extern IntPtr CGEventCreateKeyboardEvent(IntPtr source, ushort virtualKey, bool keyDown);

    [DllImport(CoreGraphicsLib)]
    private static extern void CGEventSetFlags(IntPtr eventRef, ulong flags);

    [DllImport(CoreGraphicsLib)]
    private static extern void CGEventPost(uint tap, IntPtr eventRef);

    [DllImport(CoreFoundationLib)]
    private static extern void CFRelease(IntPtr cf);

    [DllImport(AppServicesLib)]
    private static extern bool AXIsProcessTrusted();

    [DllImport(AppServicesLib)]
    private static extern bool AXIsProcessTrustedWithOptions(IntPtr options);

    public static bool IsAccessibilityGranted()
    {
        try
        {
            return AXIsProcessTrusted();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Prompts macOS to register this app or terminal in the Accessibility list.
    /// </summary>
    public static void TriggerAccessibilityPrompt()
    {
        try
        {
            var nsNumberClass = objc_getClass("NSNumber");
            var selBool = sel_registerName("numberWithBool:");
            var boolVal = objc_msgSend_IntPtr_bool(nsNumberClass, selBool, true);

            var nsStringClass = objc_getClass("NSString");
            var selStr = sel_registerName("stringWithUTF8String:");
            var keyStr = objc_msgSend_IntPtr_string(nsStringClass, selStr, "AXTrustedCheckOptionPrompt");

            var nsDictClass = objc_getClass("NSDictionary");
            var selDict = sel_registerName("dictionaryWithObject:forKey:");
            var dict = objc_msgSend_IntPtr_IntPtr_IntPtr(nsDictClass, selDict, boolVal, keyStr);

            AXIsProcessTrustedWithOptions(dict);
        }
        catch { }
    }

    public static void OpenAccessibilitySettings()
    {
        // 1. Ask macOS to show its official prompt and register the app in Accessibility
        TriggerAccessibilityPrompt();

        // 2. Open System Settings > Privacy & Security > Accessibility
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "open",
                Arguments = "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility",
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }
        catch { }

        // 3. Reveal MacDesktopApp.app in Finder so user can also drag & drop it directly
        try
        {
            var appPath = "/Users/0605abmu/Desktop/test/MacDesktopApp.app";
            if (Directory.Exists(appPath))
            {
                Process.Start("open", $"-R \"{appPath}\"");
            }
        }
        catch { }
    }

    /// <summary>
    /// Simulates Cmd + V paste into the active application where the user's cursor is.
    /// Uses Maccy's exact CGEvent sequence: Command down (0x37), 'V' down (0x09) + flag, 'V' up + flag, Command up.
    /// </summary>
    public static void SimulatePaste()
    {
        Task.Run(async () =>
        {
            // Delay 120ms to allow macOS WindowServer to finish activating the previous application
            await Task.Delay(120);

            try
            {
                // CGEventSourceStateCombinedSessionState = 0
                var source = CGEventSourceCreate(0);

                const ushort kVK_Command = 0x37; // 55
                const ushort kVK_ANSI_V = 0x09;  // 9
                const ulong maskCommand = 0x00100000;
                const uint cghidEventTap = 0;

                // 1. Command Down
                var cmdDown = CGEventCreateKeyboardEvent(source, kVK_Command, true);

                // 2. 'V' Down with Command Flag
                var vDown = CGEventCreateKeyboardEvent(source, kVK_ANSI_V, true);
                if (vDown != IntPtr.Zero) CGEventSetFlags(vDown, maskCommand);

                // 3. 'V' Up with Command Flag
                var vUp = CGEventCreateKeyboardEvent(source, kVK_ANSI_V, false);
                if (vUp != IntPtr.Zero) CGEventSetFlags(vUp, maskCommand);

                // 4. Command Up
                var cmdUp = CGEventCreateKeyboardEvent(source, kVK_Command, false);

                // Post all events in order to the system HID tap
                if (cmdDown != IntPtr.Zero) { CGEventPost(cghidEventTap, cmdDown); CFRelease(cmdDown); }
                if (vDown != IntPtr.Zero) { CGEventPost(cghidEventTap, vDown); CFRelease(vDown); }
                if (vUp != IntPtr.Zero) { CGEventPost(cghidEventTap, vUp); CFRelease(vUp); }
                if (cmdUp != IntPtr.Zero) { CGEventPost(cghidEventTap, cmdUp); CFRelease(cmdUp); }

                if (source != IntPtr.Zero) CFRelease(source);
            }
            catch { }

            // Secondary failsafe: AppleScript
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "/usr/bin/osascript",
                    Arguments = "-e 'tell application \"System Events\" to keystroke \"v\" using command down'",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(500);
            }
            catch { }
        });
    }

    #endregion

    #region Window Style & App Lifecycle (Deactivation / Hide)

    /// <summary>
    /// Hides the application and immediately restores focus to the previously active application.
    /// </summary>
    public static void HideAppAndDeactivate()
    {
        try
        {
            var nsAppClass = objc_getClass("NSApplication");
            var selSharedApp = sel_registerName("sharedApplication");
            var app = objc_msgSend(nsAppClass, selSharedApp);

            // [NSApp hide:nil] restores focus to the previously focused application
            var selHide = sel_registerName("hide:");
            objc_msgSend_IntPtr_IntPtr(app, selHide, IntPtr.Zero);
        }
        catch { }
    }

    /// <summary>
    /// Removes and disables the minimize button and zoom/maximize button on macOS NSWindow.
    /// </summary>
    public static void RemoveMinimizeAndMaximizeButtons(IntPtr nsWindow)
    {
        if (nsWindow == IntPtr.Zero) return;
        try
        {
            var selButton = sel_registerName("standardWindowButton:");
            var selSetHidden = sel_registerName("setHidden:");
            var selSetEnabled = sel_registerName("setEnabled:");

            // 1 = NSWindowMiniaturizeButton (Minimize)
            var minBtn = objc_msgSend_IntPtr_nint(nsWindow, selButton, 1);
            if (minBtn != IntPtr.Zero)
            {
                objc_msgSend_void_bool(minBtn, selSetHidden, true);
                objc_msgSend_void_bool(minBtn, selSetEnabled, false);
            }

            // 2 = NSWindowZoomButton (Maximize/Fullscreen)
            var zoomBtn = objc_msgSend_IntPtr_nint(nsWindow, selButton, 2);
            if (zoomBtn != IntPtr.Zero)
            {
                objc_msgSend_void_bool(zoomBtn, selSetHidden, true);
                objc_msgSend_void_bool(zoomBtn, selSetEnabled, false);
            }
        }
        catch { }
    }

    #endregion

    #region Carbon Global Hotkeys

    [StructLayout(LayoutKind.Sequential)]
    public struct EventHotKeyID
    {
        public uint signature;
        public uint id;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct EventTypeSpec
    {
        public uint eventClass;
        public uint eventKind;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int EventHandlerDelegate(IntPtr inHandlerCallRef, IntPtr inEvent, IntPtr inUserData);

    [DllImport(CarbonLib)]
    private static extern IntPtr GetApplicationEventTarget();

    [DllImport(CarbonLib)]
    private static extern int InstallEventHandler(
        IntPtr inTarget,
        EventHandlerDelegate inHandler,
        uint inNumTypes,
        EventTypeSpec[] inList,
        IntPtr inUserData,
        out IntPtr outHandlerRef);

    [DllImport(CarbonLib)]
    private static extern int RegisterEventHotKey(
        uint inHotKeyCode,
        uint inHotKeyModifiers,
        EventHotKeyID inHotKeyID,
        IntPtr inTarget,
        uint inOptions,
        out IntPtr outRef);

    private const uint kEventClassKeyboard = 0x6b657962; // 'keyb'
    private const uint kEventHotKeyPressed = 5;

    private static EventHandlerDelegate? _hotKeyDelegate;
    private static IntPtr _handlerRef;
    private static IntPtr _hotKeyRef1;
    private static IntPtr _hotKeyRef2;

    public static event Action? HotKeyPressed;

    public static bool RegisterGlobalHotkeys()
    {
        try
        {
            var target = GetApplicationEventTarget();
            var spec = new EventTypeSpec[]
            {
                new() { eventClass = kEventClassKeyboard, eventKind = kEventHotKeyPressed }
            };

            _hotKeyDelegate = (callRef, ev, data) =>
            {
                HotKeyPressed?.Invoke();
                return 0;
            };

            int status = InstallEventHandler(target, _hotKeyDelegate, 1, spec, IntPtr.Zero, out _handlerRef);
            if (status != 0) return false;

            // Cmd + Shift + V (9 = 'V', 256 | 512 = 768)
            var hotKeyId1 = new EventHotKeyID { signature = 0x434c4950, id = 1 };
            RegisterEventHotKey(9, 768, hotKeyId1, target, 0, out _hotKeyRef1);

            // Cmd + Option + V (256 | 2048 = 2304)
            var hotKeyId2 = new EventHotKeyID { signature = 0x434c4950, id = 2 };
            RegisterEventHotKey(9, 2304, hotKeyId2, target, 0, out _hotKeyRef2);

            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region macOS App & Pasteboard Integration

    public static void SetAsAccessoryApp()
    {
        try
        {
            var nsAppClass = objc_getClass("NSApplication");
            var selSharedApp = sel_registerName("sharedApplication");
            var app = objc_msgSend(nsAppClass, selSharedApp);
            var selSetActivationPolicy = sel_registerName("setActivationPolicy:");
            // NSApplicationActivationPolicyAccessory = 1 (removes Dock icon)
            objc_msgSend_bool_nint(app, selSetActivationPolicy, 1);
        }
        catch { }
    }

    public static void ActivateApp()
    {
        try
        {
            var nsAppClass = objc_getClass("NSApplication");
            var selSharedApp = sel_registerName("sharedApplication");
            var app = objc_msgSend(nsAppClass, selSharedApp);
            var selActivate = sel_registerName("activateIgnoringOtherApps:");
            objc_msgSend_bool_bool(app, selActivate, true);
        }
        catch { }
    }

    public static nint GetPasteboardChangeCount()
    {
        try
        {
            var pbClass = objc_getClass("NSPasteboard");
            var selGeneral = sel_registerName("generalPasteboard");
            var pb = objc_msgSend(pbClass, selGeneral);
            if (pb == IntPtr.Zero) return -1;
            var selChange = sel_registerName("changeCount");
            return objc_msgSend_nint(pb, selChange);
        }
        catch
        {
            return -1;
        }
    }

    public static string? GetPasteboardString()
    {
        try
        {
            var pbClass = objc_getClass("NSPasteboard");
            var selGeneral = sel_registerName("generalPasteboard");
            var pb = objc_msgSend(pbClass, selGeneral);
            if (pb == IntPtr.Zero) return null;

            var selStringForType = sel_registerName("stringForType:");
            var selUtf8 = sel_registerName("UTF8String");

            var nsStringClass = objc_getClass("NSString");
            var selStringWithUtf8 = sel_registerName("stringWithUTF8String:");
            var typeStr = objc_msgSend_IntPtr_string(nsStringClass, selStringWithUtf8, "public.utf8-plain-text");

            var resultStr = objc_msgSend_IntPtr_IntPtr(pb, selStringForType, typeStr);
            if (resultStr != IntPtr.Zero)
            {
                var utf8Ptr = objc_msgSend(resultStr, selUtf8);
                if (utf8Ptr != IntPtr.Zero)
                {
                    return Marshal.PtrToStringUTF8(utf8Ptr);
                }
            }
        }
        catch { }
        return null;
    }

    public static void SetPasteboardString(string text)
    {
        try
        {
            var pbClass = objc_getClass("NSPasteboard");
            var selGeneral = sel_registerName("generalPasteboard");
            var pb = objc_msgSend(pbClass, selGeneral);
            if (pb == IntPtr.Zero) return;

            var selClear = sel_registerName("clearContents");
            objc_msgSend(pb, selClear);

            var nsStringClass = objc_getClass("NSString");
            var selStringWithUtf8 = sel_registerName("stringWithUTF8String:");
            var typeStr = objc_msgSend_IntPtr_string(nsStringClass, selStringWithUtf8, "public.utf8-plain-text");
            var contentStr = objc_msgSend_IntPtr_string(nsStringClass, selStringWithUtf8, text);

            var selSetString = sel_registerName("setString:forType:");
            objc_msgSend_bool_IntPtr_IntPtr(pb, selSetString, contentStr, typeStr);
        }
        catch { }
    }

    #endregion
}
