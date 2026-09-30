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
| **Tarqatish Formati** | `.dmg` (Disk Image) | `.deb` (Ubuntu/Debian) & `.tar.gz` | `.zip` (Portable `.exe`) |

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
8. **⚡ Native AOT & Trimming (Ultra-engil va Darhol Ishga Tushuvchi)**:
   - Dastur har bir OS uchun to'g'ridan-to'g'ri Native AOT (Ahead-of-Time Machine Code) sifatida yig'iladi. Ortiqcha JIT runtime yo'q, hajm ~45-50% ga kamaytirilgan (DMG ~30MB, Windows zip ~25MB), xotira (RAM) sarfi minimal va ishga tushish tezligi < 30ms!

---

## 🐧 Ubuntu Desktop: O'rnatish va Yangilash (.deb)

Ubuntu va Debian asosidagi tizimlarda ilovani qulay o'rnatish va yangilash:

### 1. O'rnatish (Install):
```bash
# GitHub Releases'dan .deb faylni yuklab olib:
sudo apt install ./clipboard_1.0.0_amd64.deb
# yoki
sudo dpkg -i clipboard_1.0.0_amd64.deb
```
> O'rnatilgandan so'ng, ilova avtomatik tarzda Ubuntu ilovalar ro'yxatiga (App Grid / Applications menu) qo'shiladi va terminaldan `clipboard` buyrug'i orqali ham ishga tushiriladi.

### 2. Yangilash (Update):
Yangi versiya chiqqanda uni yangilash juda oddiy — yangi versiyadagi `.deb` faylni xuddi shu buyruq bilan o'rnatish kifoya:
```bash
sudo apt install ./clipboard_1.0.1_amd64.deb
```
Package menejeri (APT / DPKG) mavjud versiyani avtomatik aniqlab, eski versiyadagi barcha fayllarni yangisiga xavfsiz almashtiradi. Sizning qadab qo'yilgan (pinned) va oldingi clipboard tarixingiz (`~/.config/Clipboard/`) to'liq saqlanib qoladi.

Ilova Tray menyusidagi **"🔄 Yangilanishlarni tekshirish"** bandi orqali ham bir klikda yangi versiyalarni ko'rishingiz mumkin.

### 3. Tizimdan o'chirish (Uninstall):
```bash
sudo apt remove clipboard
```

---

## 🏃‍♂️ Loyihani Yig'ish (Build)

### Lokal Yig'ish:
- **macOS DMG**: `./build-dmg.sh osx-arm64`
- **Linux .deb va .tar.gz**: `./build-linux.sh linux-x64`
- **Windows Zip**: `./build-windows.sh win-x64`

### To'g'ridan-to'g'ri Ishga Tushirish:
```bash
dotnet run
```

---

## 📦 CI/CD va Avtomatik Chiqarish (Release)

GitHub Actions orqali har safar yangi teg (masalan, `v1.1.0`) yuborilganda:
1. `osx-arm64` va `osx-x64` uchun **macOS DMG**;
2. `linux-x64` va `linux-arm64` uchun **Linux Ubuntu .deb installer** hamda **.tar.gz**;
3. `win-x64` uchun **Windows .zip**;

barcha platformalar uchun avtomatik yig'ilib, [GitHub Releases](https://github.com/0605AbMu/clipboard/releases) sahifasiga yuklanadi!
