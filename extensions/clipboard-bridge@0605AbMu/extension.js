import { Extension } from 'resource:///org/gnome/shell/extensions/extension.js';
import Clutter from 'gi://Clutter';
import Gio from 'gi://Gio';
import GLib from 'gi://GLib';
import Meta from 'gi://Meta';
import Shell from 'gi://Shell';

const ClipboardBridgeIface = `
<node>
  <interface name="org.gnome.Shell.Extensions.ClipboardBridge">
    <method name="Paste"/>
  </interface>
</node>
`;

export default class ClipboardBridgeExtension extends Extension {
    enable() {
        this._virtualKeyboard = null;
        try {
            const seat = Clutter.get_default_backend().get_default_seat();
            this._virtualKeyboard = seat.create_virtual_device(Clutter.InputDeviceType.KEYBOARD_DEVICE);
        } catch (e) {
            console.error('[ClipboardBridge] Failed to create virtual keyboard:', e);
        }

        const nodeInfo = Gio.DBusNodeInfo.new_for_xml(ClipboardBridgeIface);
        this._dbusImpl = Gio.DBusExportedObject.wrapJSObject(nodeInfo.interfaces[0], this);
        this._dbusImpl.export(Gio.DBus.session, '/org/gnome/Shell/Extensions/ClipboardBridge');
        console.log('[ClipboardBridge] Extension enabled and D-Bus interface exported');
    }

    disable() {
        if (this._dbusImpl) {
            this._dbusImpl.unexport();
            this._dbusImpl = null;
        }
        this._virtualKeyboard = null;
        console.log('[ClipboardBridge] Extension disabled');
    }

    _isTerminalWindow(win) {
        if (!win) return false;

        try {
            const wmClass = (win.get_wm_class ? win.get_wm_class() : win.wm_class) || '';
            const wmInstance = (win.get_wm_class_instance ? win.get_wm_class_instance() : '') || '';
            const gtkId = (win.get_gtk_application_id ? win.get_gtk_application_id() : win.gtk_application_id) || '';
            const sandboxedId = (win.get_sandboxed_app_id ? win.get_sandboxed_app_id() : '') || '';

            const identifiers = [
                wmClass,
                wmInstance,
                gtkId,
                sandboxedId
            ].map(s => (s || '').toLowerCase());

            const terminalKeywords = [
                'terminal', 'ptyxis', 'alacritty', 'kitty', 'foot', 'wezterm',
                'ghostty', 'terminator', 'tilix', 'xterm', 'urxvt', 'konsole',
                'lxterminal', 'mate-terminal', 'xfce4-terminal', 'guake', 'tilda',
                'hyper', 'tabby', 'blackbox', 'rio', 'warp', 'console'
            ];

            for (const text of identifiers) {
                for (const kw of terminalKeywords) {
                    if (text.includes(kw)) return true;
                }
            }

            const app = Shell.WindowTracker.get_default().get_window_app(win);
            if (app) {
                const appId = (app.get_id() || '').toLowerCase();
                for (const kw of terminalKeywords) {
                    if (appId.includes(kw)) return true;
                }

                const appInfo = app.get_app_info();
                if (appInfo) {
                    const categories = (appInfo.get_categories() || '').toLowerCase();
                    if (categories.includes('terminalemulator')) {
                        return true;
                    }
                }
            }
        } catch (e) {
            console.warn('[ClipboardBridge] Error detecting terminal:', e);
        }

        return false;
    }

    Paste() {
        if (!this._virtualKeyboard) {
            try {
                const seat = Clutter.get_default_backend().get_default_seat();
                this._virtualKeyboard = seat.create_virtual_device(Clutter.InputDeviceType.KEYBOARD_DEVICE);
            } catch (e) {
                console.error('[ClipboardBridge] Virtual keyboard unavailable:', e);
                return;
            }
        }

        const win = global.display.focus_window;
        const isTerminal = this._isTerminalWindow(win);
        const time = GLib.get_monotonic_time();

        // Hardware evdev keycodes: KEY_LEFTCTRL = 29, KEY_LEFTSHIFT = 42, KEY_V = 47
        try {
            if (isTerminal) {
                this._virtualKeyboard.notify_key(time, 29, Clutter.KeyState.PRESSED);
                this._virtualKeyboard.notify_key(time + 15000, 42, Clutter.KeyState.PRESSED);
                this._virtualKeyboard.notify_key(time + 30000, 47, Clutter.KeyState.PRESSED);
                this._virtualKeyboard.notify_key(time + 45000, 47, Clutter.KeyState.RELEASED);
                this._virtualKeyboard.notify_key(time + 60000, 42, Clutter.KeyState.RELEASED);
                this._virtualKeyboard.notify_key(time + 75000, 29, Clutter.KeyState.RELEASED);
            } else {
                this._virtualKeyboard.notify_key(time, 29, Clutter.KeyState.PRESSED);
                this._virtualKeyboard.notify_key(time + 20000, 47, Clutter.KeyState.PRESSED);
                this._virtualKeyboard.notify_key(time + 40000, 47, Clutter.KeyState.RELEASED);
                this._virtualKeyboard.notify_key(time + 60000, 29, Clutter.KeyState.RELEASED);
            }
        } catch (e) {
            try {
                if (isTerminal) {
                    this._virtualKeyboard.notify_keyval(time, Clutter.KEY_Control_L, Clutter.KeyState.PRESSED);
                    this._virtualKeyboard.notify_keyval(time + 15000, Clutter.KEY_Shift_L, Clutter.KeyState.PRESSED);
                    this._virtualKeyboard.notify_keyval(time + 30000, Clutter.KEY_v, Clutter.KeyState.PRESSED);
                    this._virtualKeyboard.notify_keyval(time + 45000, Clutter.KEY_v, Clutter.KeyState.RELEASED);
                    this._virtualKeyboard.notify_keyval(time + 60000, Clutter.KEY_Shift_L, Clutter.KeyState.RELEASED);
                    this._virtualKeyboard.notify_keyval(time + 75000, Clutter.KEY_Control_L, Clutter.KeyState.RELEASED);
                } else {
                    this._virtualKeyboard.notify_keyval(time, Clutter.KEY_Control_L, Clutter.KeyState.PRESSED);
                    this._virtualKeyboard.notify_keyval(time + 20000, Clutter.KEY_v, Clutter.KeyState.PRESSED);
                    this._virtualKeyboard.notify_keyval(time + 40000, Clutter.KEY_v, Clutter.KeyState.RELEASED);
                    this._virtualKeyboard.notify_keyval(time + 60000, Clutter.KEY_Control_L, Clutter.KeyState.RELEASED);
                }
            } catch (err) {
                console.error('[ClipboardBridge] Error injecting paste keystrokes:', err);
            }
        }
    }
}
