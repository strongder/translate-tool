# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
cd TranslatorApp
dotnet build
dotnet run
```

Publish self-contained exe:
```bash
dotnet publish -c Release -r win-x64 --self-contained true

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

**Quan trọng:** Trước khi build, kill process cũ nếu đang chạy (file .exe bị lock):
```powershell
Stop-Process -Name "TranslatorApp" -Force -ErrorAction SilentlyContinue
```

Target framework: `net8.0-windows10.0.19041.0` (cần Windows 10 SDK để dùng WinRT OCR).

## Cấu hình

`appsettings.json` — chọn provider bằng `"Provider": "openai"` hoặc `"gemini"`. File này được copy to output directory khi build.

**Lưu ý bảo mật:** File `appsettings.json` hiện chứa Gemini API key thật — không commit lên git public.

## Kiến trúc

App chạy nền (không có MainWindow), entry point là `App.xaml.cs`.

**Flow Alt+T (dịch text bôi đen):**
```
HotkeyService.WM_HOTKEY
  → App.OnTextHotkeyPressed()
  → ClipboardService.CaptureTargetWindow()  ← capture hwnd TRƯỚC khi focus đổi
  → ClipboardService.CopyAndGetText()       ← AttachThreadInput + Ctrl+C + poll clipboard
  → GptService.TranslateAsync()
  → TranslationPopup.Show()
```

**Flow Alt+S (chụp màn hình → OCR → dịch):**
```
HotkeyService.WM_HOTKEY
  → App.OnScreenHotkeyPressed()
  → ScreenOverlay.ShowDialog()              ← fullscreen overlay, rubber-band selection
  → [overlay đóng] App chụp Bitmap từ CaptureRegion
  → OcrService.ExtractTextAsync()           ← Windows.Media.Ocr (WinRT built-in)
  → GptService.TranslateAsync()
  → TranslationPopup.Show()
```

### Các điểm kỹ thuật quan trọng

**ClipboardService:** Chạy trên STA thread riêng. Dùng `AttachThreadInput(currentThread, targetThread, true)` trước `SetForegroundWindow` để bypass Windows anti-focus-stealing. Release `VK_ALT` và `VK_SHIFT` trước khi gửi Ctrl+C (tránh Ctrl+Shift+C). Detach trong `finally`.

**HotkeyService:** Tạo message-only window (`HWND_MESSAGE = IntPtr(-3)`) để nhận `WM_HOTKEY`. Hai hotkey ID riêng biệt: `9001` (Alt+T), `9002` (Alt+S).

**ScreenOverlay:** `WindowStyle=None` + `AllowsTransparency=True` không tương thích với `WindowState=Maximized` — phải set `Left/Top/Width/Height` thủ công từ `SystemInformation.VirtualScreen` để span tất cả màn hình. Canvas phải có `Background="Transparent"` để nhận mouse events (null background = click-through). `DialogResult` phải được set trước khi `Close()` — không gọi `Hide()` trước khi set `DialogResult`.

**OcrService:** Dùng `Windows.Media.Ocr` (WinRT, không cần NuGet). Cần namespace `Windows.Globalization` cho `Language`. Convert bitmap → `SoftwareBitmap` (Bgra8) qua `BitmapDecoder`.

**GptService:** Hỗ trợ cả OpenAI và Gemini. Cache 1 entry (text → translation) trong `ClipboardService._lastText/_lastTranslation`.

**TranslationPopup:** Đóng khi mất focus (`Window_Deactivated`). `ForceClose()` safe để gọi nhiều lần.

### Guard quan trọng

`_isTranslating` flag trong `App.xaml.cs` ngăn mở nhiều popup/overlay cùng lúc. Flag được set/reset trong mọi code path (kể cả early return và exception).
