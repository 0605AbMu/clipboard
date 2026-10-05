import { Extension } from 'resource:///org/gnome/shell/extensions/extension.js';
import Clutter from 'gi://Clutter';
import Gio from 'gi://Gio';
import GLib from 'gi://GLib';

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

        const time = GLib.get_monotonic_time();
        // Hardware evdev keycodes: KEY_LEFTCTRL = 29, KEY_V = 47
        try {
            this._virtualKeyboard.notify_key(time, 29, Clutter.KeyState.PRESSED);
            this._virtualKeyboard.notify_key(time + 20000, 47, Clutter.KeyState.PRESSED);
            this._virtualKeyboard.notify_key(time + 40000, 47, Clutter.KeyState.RELEASED);
            this._virtualKeyboard.notify_key(time + 60000, 29, Clutter.KeyState.RELEASED);
        } catch (e) {
            this._virtualKeyboard.notify_keyval(time, Clutter.KEY_Control_L, Clutter.KeyState.PRESSED);
            this._virtualKeyboard.notify_keyval(time + 20000, Clutter.KEY_v, Clutter.KeyState.PRESSED);
            this._virtualKeyboard.notify_keyval(time + 40000, Clutter.KEY_v, Clutter.KeyState.RELEASED);
            this._virtualKeyboard.notify_keyval(time + 60000, Clutter.KEY_Control_L, Clutter.KeyState.RELEASED);
        }
    }
}
