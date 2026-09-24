# macOS Ixcham Plain-Text Clipboard Manager (Auto-Paste)

Ushbu dastur Windows'dagi **Win + V** clipboard menejerining macOS uchun yaratilgan, **faqat toza matn (plain-text)** bilan ishlovchi, **avtomatik joylash (auto-paste)** imkoniyatiga ega, minimalistik va **macOS native toza interfeysiga** ega versiyasi.

---

## 🔐 Auto-Paste uchun Ruxsat Berish (Accessibility)

macOS xavfsizlik tizimi sababli, `dotnet run` orqali ishga tushirilganda macOS sozlamalarda ilovani qanday topish mumkin:

### 1-usul: Terminal ga ruxsat berish (Eng osoni)
Agar dasturni terminal orqali `dotnet run` qilib ishga tushirgan bo'lsangiz:
1. **System Settings > Privacy & Security > Accessibility** ga kiring.
2. Ro'yxatdagi **Terminal** (yoki **iTerm** / **VS Code**) ni **ON** (yoqilgan) qiling.
3. Bo'ldi! Terminal ruxsat olgach, uning ichidagi dastur `Cmd + V` ni avtomatik bajara oladi.

### 2-usul: `MacDesktopApp.app` ni ro'yxatga qo'shish (Drag & Drop)
`/Desktop/test/` papkasida to'liq mustaqil **`MacDesktopApp.app`** ilovasi tayyorlandi:
1. Dasturdagi **"Ruxsat berish"** tugmasini bosing (u Sozlamalar va Finder oynasini bir vaqtda ochadi).
2. Finder'dagi **`MacDesktopApp.app`** ni sichqoncha bilan ushlab, **System Settings > Accessibility** ro'yxatiga tortib tashlang (Drag & Drop).
3. Yoki pastdagi **`+` (Plus)** tugmasini bosib, `Desktop/test/MacDesktopApp.app` ni tanlang.

---

## 🏃‍♂️ Ishga tushirish

Terminalda:

```bash
cd ~/Desktop/test
dotnet run
```

Yoki to'g'ridan-to'g'ri .app bundle sifatida:

```bash
open ~/Desktop/test/MacDesktopApp.app
```

---

## ⌨️ Boshqaruv

| Harakat | Vazifasi |
|---|---|
| **`Cmd + Shift + V`** | Oynani ochish / yashirish |
| **`Cmd + Option + V`** | Muqobil chaqirish tugmasi |
| **`↓` va `↑`** | Ro'yxatdagi elementlar bo'ylab yurish |
| **`Enter` (yoki Bosish)** | **Oynani yopib, kursor turgan joyga matnni avtomatik paste qilish** |
| **`Esc`** | Oynani yopish |
| **📌** | Elementni qadash |
| **✕** | Bitta elementni o'chirish |
| **🧹 (Tepada)** | Qadalmagan barcha matnlarni tozalash |
