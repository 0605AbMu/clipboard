import Clutter from 'gi://Clutter';
import GObject from 'gi://GObject';
import St from 'gi://St';
import GLib from 'gi://GLib';
import Pango from 'gi://Pango';
import Meta from 'gi://Meta';
import Shell from 'gi://Shell';
import * as Main from 'resource:///org/gnome/shell/ui/main.js';

let spotlightGTypeName = 'ClipboardSpotlightDialog';
if (GObject.type_from_name(spotlightGTypeName)) {
    spotlightGTypeName = `ClipboardSpotlightDialog_${Date.now()}`;
}

export const SpotlightDialog = GObject.registerClass({
    GTypeName: spotlightGTypeName,
    Signals: {
        'closed': {},
        'item-selected': { param_types: [GObject.TYPE_STRING] },
        'item-deleted': { param_types: [GObject.TYPE_STRING] },
        'item-pin-toggled': { param_types: [GObject.TYPE_STRING] }
    }
}, class extends St.Widget {
    _init(historyStore, inputSimulator, autoPaste = true) {
        super._init({
            style_class: 'clipboard-modal-backdrop',
            reactive: true,
            visible: false,
        });

        Main.uiGroup.add_child(this);

        // Cover the entire screen so clicks anywhere outside the dialog land on this modal actor
        this.add_constraint(new Clutter.BindConstraint({
            source: global.stage,
            coordinate: Clutter.BindCoordinate.ALL,
        }));

        this._historyStore = historyStore;
        this._inputSimulator = inputSimulator;
        this._autoPaste = autoPaste;
        this._items = [];
        this._itemWidgets = [];
        this._selectedIndex = 0;
        this._grab = null;
        this._stageEventId = 0;
        this._isClosing = false;

        this._targetWindow = null;
        this._isTargetTerminal = false;
        this._captureTargetWindow();

        this._buildUI();

        this.connect('button-press-event', (actor, event) => {
            const src = event.get_source();
            if (src === this) {
                this.close();
                return Clutter.EVENT_STOP;
            }
            return Clutter.EVENT_PROPAGATE;
        });

        this.connect('key-press-event', (actor, event) => {
            if (event.get_key_symbol() === Clutter.KEY_Escape) {
                this.close();
                return Clutter.EVENT_STOP;
            }
            return Clutter.EVENT_PROPAGATE;
        });
    }

    _captureTargetWindow() {
        try {
            let win = global.display.focus_window;
            if (!win) {
                const normalWindows = global.display.get_tab_list(Meta.TabList.NORMAL, null);
                if (normalWindows && normalWindows.length > 0) {
                    win = normalWindows[0];
                }
            }
            this._targetWindow = win;
            this._isTargetTerminal = this._isTerminalWindow(win);
        } catch (e) {
            console.warn('[Clipboard] Failed to capture target window:', e);
            this._targetWindow = null;
            this._isTargetTerminal = false;
        }
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
                    if (text.includes(kw)) {
                        return true;
                    }
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
            console.warn('[Clipboard] Error detecting terminal window:', e);
        }

        return false;
    }

    _buildUI() {
        // Main centered dialog box (compact width matching Avalonia UI: 380px)
        this._dialogBox = new St.BoxLayout({
            style_class: 'clipboard-popup-box',
            vertical: true,
            reactive: true,
            width: 380
        });
        // Clicking inside the dialog box container should not dismiss the dialog
        this._dialogBox.connect('button-press-event', () => Clutter.EVENT_STOP);
        this.add_child(this._dialogBox);

        // Header: App Icon + Title + Clear Button (matching Avalonia header)
        const headerBox = new St.BoxLayout({
            style_class: 'clipboard-header-box',
            vertical: false,
            x_expand: true
        });

        const titleBox = new St.BoxLayout({
            style_class: 'clipboard-title-box',
            vertical: false,
            x_expand: true,
            y_align: Clutter.ActorAlign.CENTER
        });

        const appIcon = new St.Label({
            text: '📋',
            style_class: 'clipboard-app-icon',
            y_align: Clutter.ActorAlign.CENTER
        });
        titleBox.add_child(appIcon);

        const titleLabel = new St.Label({
            text: 'Clipboard',
            style_class: 'clipboard-title-text',
            y_align: Clutter.ActorAlign.CENTER
        });
        titleBox.add_child(titleLabel);
        headerBox.add_child(titleBox);

        const headerActions = new St.BoxLayout({
            style_class: 'clipboard-header-actions',
            vertical: false,
            y_align: Clutter.ActorAlign.CENTER
        });

        const clearBtn = new St.Button({
            style_class: 'clipboard-header-btn',
            can_focus: false,
            track_hover: true,
            reactive: true,
            child: new St.Label({
                text: '🧹',
                style_class: 'clipboard-header-btn-icon',
                y_align: Clutter.ActorAlign.CENTER
            })
        });
        clearBtn.connect('button-press-event', () => {
            this._historyStore.clear();
            this._refreshItems();
            return Clutter.EVENT_STOP;
        });
        headerActions.add_child(clearBtn);
        headerBox.add_child(headerActions);
        this._dialogBox.add_child(headerBox);

        // Search Bar (watermark: 🔍 Qidirish...)
        const searchBox = new St.BoxLayout({
            style_class: 'clipboard-search-box',
            vertical: false,
            x_expand: true
        });

        this._searchEntry = new St.Entry({
            style_class: 'clipboard-search-entry',
            hint_text: '🔍 Qidirish...',
            can_focus: true,
            x_expand: true
        });

        const clutterText = this._searchEntry.get_clutter_text();
        clutterText.connect('key-press-event', this._onKeyPressed.bind(this));
        clutterText.connect('text-changed', this._onSearchChanged.bind(this));
        searchBox.add_child(this._searchEntry);
        this._dialogBox.add_child(searchBox);

        // Scroll View for Items
        this._scrollView = new St.ScrollView({
            style_class: 'clipboard-scroll-view',
            hscrollbar_policy: St.PolicyType.NEVER,
            vscrollbar_policy: St.PolicyType.AUTOMATIC,
            x_expand: true,
            y_expand: true
        });

        this._itemsContainer = new St.BoxLayout({
            style_class: 'clipboard-items-container',
            vertical: true,
            x_expand: true
        });
        this._scrollView.add_child(this._itemsContainer);
        this._dialogBox.add_child(this._scrollView);

        // Footer / Key hints (matching Avalonia shortcut hints)
        const footer = new St.BoxLayout({
            style_class: 'clipboard-footer',
            vertical: false,
            x_expand: true
        });
        const hintText = new St.Label({
            text: '↑↓ Tanlash   ↵ Qo\'yish   Del O\'chirish   Esc Yopish',
            style_class: 'clipboard-footer-hint',
            x_expand: true,
            x_align: Clutter.ActorAlign.CENTER
        });
        footer.add_child(hintText);
        this._dialogBox.add_child(footer);
    }

    open() {
        this._captureTargetWindow();
        this.visible = true;

        this._grab = Main.pushModal(this);

        // Center dialog box on active workspace and monitor
        const activeWs = global.workspace_manager.get_active_workspace();
        const monitor = global.display.get_current_monitor();
        const workArea = activeWs.get_work_area_for_monitor(monitor);

        const boxWidth = Math.min(380, workArea.width - 40);
        this._dialogBox.set_width(boxWidth);

        const posX = workArea.x + Math.floor((workArea.width - boxWidth) / 2);
        const posY = workArea.y + Math.max(80, Math.floor((workArea.height - 440) / 2));
        this._dialogBox.set_position(posX, posY);

        this._searchEntry.set_text('');
        this._refreshItems();
        this._searchEntry.grab_key_focus();
    }

    close() {
        if (this._isClosing)
            return;
        this._isClosing = true;

        if (this._stageEventId) {
            global.stage.disconnect(this._stageEventId);
            this._stageEventId = 0;
        }

        if (this._grab) {
            Main.popModal(this._grab);
            this._grab = null;
        }

        this.emit('closed');
        super.destroy();
    }

    destroy() {
        this.close();
    }

    _refreshItems() {
        this._itemsContainer.destroy_all_children();
        this._itemWidgets = [];

        const allItems = this._historyStore.getItems();
        const filter = this._searchEntry.get_text().toLowerCase().trim();

        this._items = allItems.filter(item => {
            if (!filter) return true;
            return item.text.toLowerCase().includes(filter);
        });

        if (this._items.length === 0) {
            const emptyBox = new St.BoxLayout({
                style_class: 'clipboard-empty-box',
                vertical: true,
                x_expand: true,
                y_expand: true,
                x_align: Clutter.ActorAlign.CENTER,
                y_align: Clutter.ActorAlign.CENTER
            });

            const emptyTitle = new St.Label({
                text: filter ? 'Natija topilmadi' : 'Matn yo\'q',
                style_class: 'clipboard-empty-title',
                x_align: Clutter.ActorAlign.CENTER
            });
            emptyBox.add_child(emptyTitle);

            if (!filter) {
                const emptySub = new St.Label({
                    text: 'Ctrl+C orqali nusxalang',
                    style_class: 'clipboard-empty-subtitle',
                    x_align: Clutter.ActorAlign.CENTER
                });
                emptyBox.add_child(emptySub);
            }

            this._itemsContainer.add_child(emptyBox);
            this._selectedIndex = -1;
            return;
        }

        this._items.forEach((item, index) => {
            const row = this._createItemRow(item, index);
            this._itemsContainer.add_child(row);
            this._itemWidgets.push(row);
        });

        this._selectedIndex = 0;
        this._highlightSelected();
    }

    _createItemRow(item, index) {
        const row = new St.BoxLayout({
            style_class: 'clipboard-item-row',
            vertical: false,
            reactive: true,
            track_hover: true,
            x_expand: true
        });

        // 1. Pin indicator on the left (📌 when pinned, • when unpinned)
        const pinIndicator = new St.Label({
            text: item.pinned ? '📌' : '•',
            style_class: 'clipboard-pin-indicator' + (item.pinned ? ' pinned' : ''),
            y_align: Clutter.ActorAlign.CENTER
        });
        row.add_child(pinIndicator);

        // 2. Single Line Plain Text Preview (max 120 chars with ellipsis)
        let snippet = item.text.replace(/[\r\n\t]+/g, ' ').trim();
        if (snippet.length > 120) {
            snippet = snippet.substring(0, 117) + '…';
        }

        const textLabel = new St.Label({
            text: snippet || '(Bo\'sh)',
            style_class: 'clipboard-item-text',
            x_expand: true,
            y_align: Clutter.ActorAlign.CENTER
        });
        const clutterLabel = textLabel.get_clutter_text();
        if (clutterLabel && clutterLabel.set_ellipsize) {
            clutterLabel.set_ellipsize(Pango.EllipsizeMode.END);
            clutterLabel.set_line_wrap(false);
        }
        row.add_child(textLabel);

        // 3. Right-hand Info & Actions (Time, Pin, Delete)
        const rightBox = new St.BoxLayout({
            style_class: 'clipboard-item-right-box',
            vertical: false,
            y_align: Clutter.ActorAlign.CENTER
        });

        // Time display (HH:mm)
        const d = new Date(item.timestamp || Date.now());
        const hh = String(d.getHours()).padStart(2, '0');
        const mm = String(d.getMinutes()).padStart(2, '0');
        const timeLabel = new St.Label({
            text: `${hh}:${mm}`,
            style_class: 'clipboard-item-time',
            y_align: Clutter.ActorAlign.CENTER
        });
        rightBox.add_child(timeLabel);

        // Pin Button 📌
        const pinBtn = new St.Button({
            style_class: 'clipboard-row-btn',
            can_focus: false,
            track_hover: true,
            reactive: true,
            child: new St.Label({
                text: '📌',
                style_class: 'clipboard-row-btn-text',
                y_align: Clutter.ActorAlign.CENTER
            })
        });
        pinBtn.connect('button-press-event', () => {
            this._historyStore.togglePin(item.id);
            this._refreshItems();
            return Clutter.EVENT_STOP;
        });
        rightBox.add_child(pinBtn);

        // Delete Button ✕
        const delBtn = new St.Button({
            style_class: 'clipboard-row-btn clipboard-del-btn',
            can_focus: false,
            track_hover: true,
            reactive: true,
            child: new St.Label({
                text: '✕',
                style_class: 'clipboard-row-btn-text',
                y_align: Clutter.ActorAlign.CENTER
            })
        });
        delBtn.connect('button-press-event', () => {
            this._historyStore.remove(item.id);
            this._refreshItems();
            return Clutter.EVENT_STOP;
        });
        rightBox.add_child(delBtn);

        row.add_child(rightBox);

        // Row click handling (prevent row activation when clicking Pin or Delete buttons)
        row.connect('button-press-event', (actor, event) => {
            const src = event.get_source();
            if (src === pinBtn || (pinBtn.contains && pinBtn.contains(src)) ||
                src === delBtn || (delBtn.contains && delBtn.contains(src))) {
                return Clutter.EVENT_PROPAGATE;
            }
            this._selectedIndex = index;
            this._activateSelected();
            return Clutter.EVENT_STOP;
        });

        return row;
    }

    _highlightSelected() {
        this._itemWidgets.forEach((widget, idx) => {
            if (idx === this._selectedIndex) {
                widget.add_style_class_name('selected');
                // Scroll into view
                const scrollBar = this._scrollView.get_vscroll_bar();
                const adjustment = scrollBar ? scrollBar.get_adjustment() : (this._scrollView.vadjustment || null);
                if (adjustment) {
                    const itemY = widget.y;
                    const itemHeight = widget.height;
                    const viewHeight = this._scrollView.height;

                    if (itemY < adjustment.value) {
                        adjustment.value = itemY;
                    } else if (itemY + itemHeight > adjustment.value + viewHeight) {
                        adjustment.value = itemY + itemHeight - viewHeight;
                    }
                }
            } else {
                widget.remove_style_class_name('selected');
            }
        });
    }

    _selectNext() {
        if (this._items.length === 0) return;
        this._selectedIndex = (this._selectedIndex + 1) % this._items.length;
        this._highlightSelected();
    }

    _selectPrev() {
        if (this._items.length === 0) return;
        this._selectedIndex = (this._selectedIndex - 1 + this._items.length) % this._items.length;
        this._highlightSelected();
    }

    _activateSelected() {
        if (this._selectedIndex < 0 || this._selectedIndex >= this._items.length) {
            return;
        }

        const selected = this._items[this._selectedIndex];
        const textToPaste = selected.text;

        // 1. Copy selected item to clipboard
        const clip = St.Clipboard.get_default();
        clip.set_text(St.ClipboardType.CLIPBOARD, textToPaste);
        clip.set_text(St.ClipboardType.PRIMARY, textToPaste);

        const isTerminal = this._isTargetTerminal;
        const targetWin = this._targetWindow;

        // 2. Close dialog to restore focus to active target window
        this.close();

        // 3. Inject native paste keystrokes directly via Clutter VirtualInputDevice
        if (this._autoPaste) {
            GLib.timeout_add(GLib.PRIORITY_DEFAULT, 80, () => {
                if (targetWin && typeof targetWin.has_focus === 'function' && !targetWin.has_focus()) {
                    try {
                        targetWin.activate(global.get_current_time());
                    } catch (e) { }
                }
                this._inputSimulator.paste(isTerminal);
                return GLib.SOURCE_REMOVE;
            });
        }
    }

    _onSearchChanged() {
        this._refreshItems();
    }

    _onKeyPressed(clutterText, event) {
        const keySym = event.get_key_symbol();
        const state = event.get_state();
        const isCtrl = (state & Clutter.ModifierType.CONTROL_MASK) !== 0;
        const isAlt = (state & Clutter.ModifierType.MOD1_MASK) !== 0;

        if (keySym === Clutter.KEY_Escape) {
            if (this._searchEntry.get_text().length > 0) {
                this._searchEntry.set_text('');
            } else {
                this.close();
            }
            return Clutter.EVENT_STOP;
        }

        if (keySym === Clutter.KEY_Return ||
            keySym === Clutter.KEY_KP_Enter ||
            keySym === Clutter.KEY_ISO_Enter) {
            this._activateSelected();
            return Clutter.EVENT_STOP;
        }

        if (keySym === Clutter.KEY_Down) {
            this._selectNext();
            return Clutter.EVENT_STOP;
        }

        if (keySym === Clutter.KEY_Up) {
            this._selectPrev();
            return Clutter.EVENT_STOP;
        }

        if (keySym === Clutter.KEY_Page_Down) {
            if (this._items.length > 0) {
                this._selectedIndex = Math.min(this._selectedIndex + 5, this._items.length - 1);
                this._highlightSelected();
            }
            return Clutter.EVENT_STOP;
        }

        if (keySym === Clutter.KEY_Page_Up) {
            if (this._items.length > 0) {
                this._selectedIndex = Math.max(this._selectedIndex - 5, 0);
                this._highlightSelected();
            }
            return Clutter.EVENT_STOP;
        }

        // Quick Pin toggle: Ctrl+P
        if (isCtrl && (keySym === Clutter.KEY_p || keySym === Clutter.KEY_P)) {
            if (this._selectedIndex >= 0 && this._selectedIndex < this._items.length) {
                const item = this._items[this._selectedIndex];
                this._historyStore.togglePin(item.id);
                this._refreshItems();
                return Clutter.EVENT_STOP;
            }
        }

        // Quick number shortcuts: Alt+1..9 or Ctrl+1..9
        if ((isCtrl || isAlt) && keySym >= Clutter.KEY_1 && keySym <= Clutter.KEY_9) {
            const numIndex = keySym - Clutter.KEY_1;
            if (numIndex < this._items.length) {
                this._selectedIndex = numIndex;
                this._activateSelected();
                return Clutter.EVENT_STOP;
            }
        }

        // Quick delete: Del or Ctrl+D or Ctrl+Backspace
        if (keySym === Clutter.KEY_Delete ||
            (isCtrl && (keySym === Clutter.KEY_d || keySym === Clutter.KEY_D)) ||
            (isCtrl && keySym === Clutter.KEY_BackSpace)) {
            if (this._selectedIndex >= 0 && this._selectedIndex < this._items.length) {
                const item = this._items[this._selectedIndex];
                this._historyStore.remove(item.id);
                this._refreshItems();
                return Clutter.EVENT_STOP;
            }
        }

        return Clutter.EVENT_PROPAGATE;
    }
});
