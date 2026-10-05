import Clutter from 'gi://Clutter';
import GLib from 'gi://GLib';

export class InputSimulator {
    constructor() {
        this._virtualKeyboard = null;
        this._ensureKeyboard();
    }

    _ensureKeyboard() {
        if (this._virtualKeyboard) return;
        try {
            const seat = Clutter.get_default_backend().get_default_seat();
            this._virtualKeyboard = seat.create_virtual_device(Clutter.InputDeviceType.KEYBOARD_DEVICE);
        } catch (e) {
            console.error('[Clipboard] Failed to create virtual keyboard device:', e);
        }
    }

    paste() {
        this._ensureKeyboard();
        if (!this._virtualKeyboard) {
            console.error('[Clipboard] Cannot simulate paste: virtual keyboard unavailable');
            return;
        }

        const now = GLib.get_monotonic_time();
        // Hardware evdev keycodes: KEY_LEFTCTRL = 29, KEY_V = 47
        try {
            this._virtualKeyboard.notify_key(now, 29, Clutter.KeyState.PRESSED);
            this._virtualKeyboard.notify_key(now + 15000, 47, Clutter.KeyState.PRESSED);
            this._virtualKeyboard.notify_key(now + 30000, 47, Clutter.KeyState.RELEASED);
            this._virtualKeyboard.notify_key(now + 45000, 29, Clutter.KeyState.RELEASED);
        } catch (e) {
            console.error('[Clipboard] Error injecting paste keystrokes:', e);
        }
    }

    destroy() {
        this._virtualKeyboard = null;
    }
}
