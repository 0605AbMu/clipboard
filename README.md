# Cross-Platform Plain-Text Clipboard Manager (macOS, Linux, Windows)

Ushbu dastur Windows'dagi **Win + V** clipboard menejerining **macOS, Linux va Windows** operatsion tizimlari uchun yaratilgan, **faqat toza matn (plain-text)** bilan ishlovchi, **avtomatik joylash (auto-paste)** imkoniyatiga ega bo'lgan to'liq cross-platform talqini.

---

## 🚀 Tizimlar Bo'yicha Xususiyatlar

| Xususiyat | 🍏 macOS | 🐧 Linux | 🪟 Windows |
|---|---|---|---|
| **Global Chaqirish** | `⌘ + ⇧ + V` / `⌘ + ⌥ + V` | `Ctrl + Shift + V` / `Alt + V` | `Ctrl + Shift + V` / `Alt + V` |
| **Kuzatish Mexanizmi** | `NSPasteboard` `changeCount` | Avalonia `Clipboard` / `wl-paste` | Win32 `GetClipboardSequenceNumber` |
| **Auto-Paste** | `CGEvent` (Maccy ketma-ketligi) | `wtype` (Wayland) / `xdotool` (X11) | Win32 `SendInput` (`Ctrl + V`) |
| **Doimiy Xotira** | `~/Library/Application Support/Clipboard` | `~/.config/Clipboard` | `%APPDATA%\Clipboard` |
| **Oyna Boshqaruvi** | Dockless (`LSUIElement`) | X11 / Wayland CSD | Win32 Borderless Tray App |
| **Tarqatish Formati** | `.dmg` (Disk Image) | `.tar.gz` (.desktop bilan) | `.zip` (Portable `.exe`) |

---

## ⚡ Asosiy Imkoniyatlar

1. **📄 Faqat Toza Matn (Pure Plain Text)**:
   - Hech qanday HTML, RTF, rang yoki uslublarsiz toza matn olinadi va joylanadi.
2. **📏 Ixcham Ro'yxat (Compact Rows)**:
   - Har bir element bir qatorli (~28px) ixcham ko'rinishda.
3. **⚡ Darhol Strelkalar bilan Harakatlanish (`↓ / ↑`)**:
   - Oyna ochilishi bilanoq strelkalar orqali ro'yxat bo'yicha yuriladi.
4. **🎯 Enter orqali Avtomatik Paste**:
   - `Enter` bosilganda oyna yashirinadi va matn kursor turgan joyga darhol qo'yiladi.
5. **📌 Qadash (Pinning)** va **🧹 Tozalash**:
   - Muhim matnlarni qadab qo'yish va qadalmaganlarini bir zumda tozalash.
6. **🚀 Tizim bilan Birga Avtomatik Ishga Tushish (Auto-start / Launch at Login)**:
   - macOS (`LaunchAgents`), Windows (`HKCU Run Registry`), Linux (`~/.config/autostart`) orqali tizim yoqilganda avtomatik ishga tushishni Tray menyusi yoki sarlavhadagi `🚀` tugmasi orqali oson yoqish/o'chirish.
7. **🎨 Maxsus Professional Ilova Ikonkasi (Native Icons)**:
   - macOS uchun ko'p qatlamli `.icns`, Windows uchun multi-res `.ico`, Linux uchun yuqori aniqlikdagi `.png`.

---

## 🏃‍♂️ Loyihani Yig'ish (Build)

### Lokal Yig'ish:
- **macOS DMG**: `./build-dmg.sh osx-arm64`
- **Linux Tar.gz**: `./build-linux.sh linux-x64`
- **Windows Zip**: `./build-windows.sh win-x64`

### To'g'ridan-to'g'ri Ishga Tushirish:
```bash
dotnet run
```

---

## 📦 CI/CD va Avtomatik Chiqarish (Release)

GitHub Actions orqali har safar yangi teg (masalan, `v1.1.0`) yuborilganda:
1. `osx-arm64` va `osx-x64` uchun **macOS DMG**;
2. `linux-x64` va `linux-arm64` uchun **Linux .tar.gz**;
3. `win-x64` uchun **Windows .zip**;

barcha platformalar uchun avtomatik yig'ilib, [GitHub Releases](https://github.com/0605AbMu/clipboard/releases) sahifasiga yuklanadi!
