import Gio from 'gi://Gio';
import GLib from 'gi://GLib';

export class HistoryStore {
    constructor(maxItems = 100) {
        this._maxItems = maxItems;
        this._items = [];
        this._listeners = new Set();

        const dataDir = GLib.build_filenamev([GLib.get_user_data_dir(), 'clipboard-manager']);
        GLib.mkdir_with_parents(dataDir, 0o755);
        this._file = Gio.File.new_for_path(GLib.build_filenamev([dataDir, 'history.json']));

        this._load();
    }

    setMaxItems(max) {
        this._maxItems = max;
        this._enforceLimit();
    }

    addListener(callback) {
        this._listeners.add(callback);
    }

    removeListener(callback) {
        this._listeners.delete(callback);
    }

    _notify() {
        for (const cb of this._listeners) {
            try { cb(this.getItems()); } catch (e) { console.error('[Clipboard] Listener error:', e); }
        }
    }

    getItems() {
        // Pinned items first, then newer items
        return [...this._items].sort((a, b) => {
            if (a.pinned && !b.pinned) return -1;
            if (!a.pinned && b.pinned) return 1;
            return b.timestamp - a.timestamp;
        });
    }

    add(text) {
        if (!text || typeof text !== 'string') return;
        const trimmed = text.trim();
        if (trimmed.length === 0) return;

        // Don't add if identical to newest item
        if (this._items.length > 0 && this._items[0].text === text) {
            return;
        }

        // Check if item already exists
        const existingIdx = this._items.findIndex(item => item.text === text);
        let pinned = false;

        if (existingIdx !== -1) {
            pinned = this._items[existingIdx].pinned;
            this._items.splice(existingIdx, 1);
        }

        const newItem = {
            id: GLib.uuid_string_random(),
            text: text,
            timestamp: Date.now(),
            pinned: pinned
        };

        this._items.unshift(newItem);
        this._enforceLimit();
        this._save();
        this._notify();
    }

    remove(id) {
        const idx = this._items.findIndex(item => item.id === id);
        if (idx !== -1) {
            this._items.splice(idx, 1);
            this._save();
            this._notify();
        }
    }

    togglePin(id) {
        const item = this._items.find(item => item.id === id);
        if (item) {
            item.pinned = !item.pinned;
            this._save();
            this._notify();
        }
    }

    clear() {
        // Keep pinned items, delete unpinned
        this._items = this._items.filter(item => item.pinned);
        this._save();
        this._notify();
    }

    _enforceLimit() {
        if (this._items.length <= this._maxItems) return;

        // Count unpinned items to drop oldest
        let unpinnedCount = this._items.filter(item => !item.pinned).length;
        const maxUnpinned = this._maxItems;

        if (unpinnedCount > maxUnpinned) {
            for (let i = this._items.length - 1; i >= 0 && unpinnedCount > maxUnpinned; i--) {
                if (!this._items[i].pinned) {
                    this._items.splice(i, 1);
                    unpinnedCount--;
                }
            }
        }
    }

    _load() {
        if (!this._file.query_exists(null)) {
            return;
        }

        try {
            const [success, contents] = this._file.load_contents(null);
            if (success) {
                const decoder = new TextDecoder('utf-8');
                const jsonStr = decoder.decode(contents);
                const parsed = JSON.parse(jsonStr);
                if (Array.isArray(parsed)) {
                    this._items = parsed;
                }
            }
        } catch (e) {
            console.error('[Clipboard] Failed to load history:', e);
        }
    }

    _save() {
        try {
            const jsonStr = JSON.stringify(this._items, null, 2);
            this._file.replace_contents_async(
                new TextEncoder().encode(jsonStr),
                null,
                false,
                Gio.FileCreateFlags.REPLACE_DESTINATION,
                null,
                (file, res) => {
                    try {
                        file.replace_contents_finish(res);
                    } catch (err) {
                        console.error('[Clipboard] Failed to finish saving history:', err);
                    }
                }
            );
        } catch (e) {
            console.error('[Clipboard] Failed to save history:', e);
        }
    }
}
