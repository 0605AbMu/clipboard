import Meta from 'gi://Meta';
import St from 'gi://St';

export class ClipboardMonitor {
    constructor(historyStore) {
        this._historyStore = historyStore;
        this._selection = global.display.get_selection();
        this._clipboard = St.Clipboard.get_default();
        this._signalId = 0;
    }

    start() {
        if (this._signalId) return;

        this._signalId = this._selection.connect(
            'owner-changed',
            (selection, selectionType) => {
                if (selectionType === Meta.SelectionType.SELECTION_CLIPBOARD) {
                    this._clipboard.get_text(St.ClipboardType.CLIPBOARD, (clip, text) => {
                        if (text && text.length > 0) {
                            this._historyStore.add(text);
                        }
                    });
                }
            }
        );

        // Capture initial clipboard content
        this._clipboard.get_text(St.ClipboardType.CLIPBOARD, (clip, text) => {
            if (text && text.length > 0) {
                this._historyStore.add(text);
            }
        });
    }

    stop() {
        if (this._signalId) {
            this._selection.disconnect(this._signalId);
            this._signalId = 0;
        }
    }
}
