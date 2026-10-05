import { Extension } from 'resource:///org/gnome/shell/extensions/extension.js';
import * as Main from 'resource:///org/gnome/shell/ui/main.js';
import * as PanelMenu from 'resource:///org/gnome/shell/ui/panelMenu.js';
import St from 'gi://St';
import Clutter from 'gi://Clutter';
import Meta from 'gi://Meta';
import Shell from 'gi://Shell';
import Gio from 'gi://Gio';

import { HistoryStore } from './src/historyStore.js';
import { ClipboardMonitor } from './src/clipboardMonitor.js';
import { InputSimulator } from './src/inputSimulator.js';
import { SpotlightDialog } from './src/ui/spotlightDialog.js';

export default class ClipboardManagerExtension extends Extension {
    enable() {
        this._settings = this.getSettings('org.gnome.shell.extensions.clipboard-manager');

        const maxItems = this._settings.get_int('history-limit');
        this._historyStore = new HistoryStore(maxItems);

        this._historyLimitId = this._settings.connect('changed::history-limit', () => {
            this._historyStore.setMaxItems(this._settings.get_int('history-limit'));
        });

        this._inputSimulator = new InputSimulator();
        this._monitor = new ClipboardMonitor(this._historyStore);
        this._monitor.start();

        this._dialog = null;

        // Prevent GNOME Shell's built-in message tray from intercepting Super+V
        this._unbindTrayConflict();

        // Register global keybinding
        Main.wm.addKeybinding(
            'toggle-shortcut',
            this._settings,
            Meta.KeyBindingFlags.IGNORE_AUTOREPEAT,
            Shell.ActionMode.NORMAL | Shell.ActionMode.OVERVIEW,
            () => this._toggleDialog()
        );

        // Add top bar status indicator if enabled
        this._indicator = null;
        if (this._settings.get_boolean('show-indicator')) {
            this._setupIndicator();
        }

        this._indicatorSettingId = this._settings.connect('changed::show-indicator', () => {
            if (this._settings.get_boolean('show-indicator')) {
                this._setupIndicator();
            } else if (this._indicator) {
                this._indicator.destroy();
                this._indicator = null;
            }
        });

        console.log('[ClipboardManager] Native GNOME extension enabled');
    }

    _setupIndicator() {
        if (this._indicator) return;

        this._indicator = new PanelMenu.Button(0.5, 'Clipboard Manager', false);
        const icon = new St.Icon({
            icon_name: 'edit-paste-symbolic',
            style_class: 'system-status-icon'
        });
        this._indicator.add_child(icon);

        this._indicator.connect('button-press-event', () => {
            this._toggleDialog();
            return Clutter.EVENT_STOP;
        });

        Main.panel.addToStatusArea('clipboard-manager', this._indicator);
    }

    _toggleDialog() {
        try {
            if (this._dialog) {
                this._dialog.close();
                this._dialog = null;
                return;
            }

            const autoPaste = this._settings.get_boolean('auto-paste');
            this._dialog = new SpotlightDialog(this._historyStore, this._inputSimulator, autoPaste);
            this._dialog.connect('closed', () => {
                this._dialog = null;
            });

            this._dialog.open();
        } catch (e) {
            console.error('[ClipboardManager] Error toggling dialog:', e);
        }
    }

    _unbindTrayConflict() {
        try {
            this._shellKeybindings = new Gio.Settings({ schema_id: 'org.gnome.shell.keybindings' });
            const trayShortcuts = this._shellKeybindings.get_strv('toggle-message-tray');
            if (trayShortcuts.includes('<Super>v')) {
                this._savedTrayShortcuts = trayShortcuts;
                const filtered = trayShortcuts.filter(k => k !== '<Super>v');
                this._shellKeybindings.set_strv('toggle-message-tray', filtered);
            }
        } catch (e) {
            console.warn('[ClipboardManager] Could not update toggle-message-tray:', e);
        }
    }

    _restoreTrayConflict() {
        try {
            if (this._savedTrayShortcuts && this._shellKeybindings) {
                this._shellKeybindings.set_strv('toggle-message-tray', this._savedTrayShortcuts);
                this._savedTrayShortcuts = null;
            }
        } catch (e) {
            console.warn('[ClipboardManager] Could not restore toggle-message-tray:', e);
        }
    }

    disable() {
        Main.wm.removeKeybinding('toggle-shortcut');

        this._restoreTrayConflict();
        this._shellKeybindings = null;

        if (this._historyLimitId) {
            this._settings.disconnect(this._historyLimitId);
            this._historyLimitId = 0;
        }

        if (this._indicatorSettingId) {
            this._settings.disconnect(this._indicatorSettingId);
            this._indicatorSettingId = 0;
        }

        if (this._monitor) {
            this._monitor.stop();
            this._monitor = null;
        }

        if (this._dialog) {
            this._dialog.close();
            this._dialog = null;
        }

        if (this._indicator) {
            this._indicator.destroy();
            this._indicator = null;
        }

        if (this._inputSimulator) {
            this._inputSimulator.destroy();
            this._inputSimulator = null;
        }

        this._historyStore = null;
        this._settings = null;

        console.log('[ClipboardManager] Native GNOME extension disabled');
    }
}
