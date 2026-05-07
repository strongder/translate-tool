Bạn là một senior software engineer chuyên xây dựng desktop tool trên Windows.

Hãy thiết kế và viết code cho một ứng dụng desktop có chức năng:
"Bôi đen văn bản hoặc chụp vùng màn hình → nhấn hotkey → dịch sang tiếng Việt ngay lập tức"

========================
🎯 MỤC TIÊU CHÍNH
========================
- Người dùng có thể bôi đen text trong bất kỳ ứng dụng nào (browser, Word, PDF, app nội bộ)
- Hoặc chụp vùng màn hình bất kỳ (kể cả ảnh, game, phần mềm không copy được text)
- Nhấn hotkey → Tool dịch sang tiếng Việt và hiển thị popup

========================
⚙️ YÊU CẦU KỸ THUẬT
========================
- Ngôn ngữ: C# (.NET 8, WPF)
- Chạy nền (background app, không có MainWindow)
- Có system tray icon
- Hotkey đăng ký qua WinAPI (RegisterHotKey / message-only window)
- Tránh lỗi SetForegroundWindow bị chặn: dùng AttachThreadInput trước khi focus
- Release ALT key sau khi hotkey kích hoạt trước khi gửi Ctrl+C

========================
🔥 TÍNH NĂNG
========================

1. HOTKEY 1: Alt + T — Dịch text bôi đen
   - Capture hwnd của foreground window tại thời điểm nhấn hotkey
   - AttachThreadInput để SetForegroundWindow không bị chặn
   - Release Alt/Shift key còn held
   - Gửi Ctrl+C programmatic (~100ms delay)
   - Polling clipboard tối đa 1.5 giây
   - Nếu clipboard rỗng → không làm gì

2. HOTKEY 2: Alt + S — Chụp vùng màn hình để dịch
   - Mở overlay fullscreen mờ (semi-transparent, topmost)
   - Người dùng kéo chuột để chọn vùng cần dịch (rubber-band selection)
   - Sau khi thả chuột: chụp ảnh vùng đó (Bitmap)
   - Gọi OCR để trích xuất text từ ảnh
     + Ưu tiên: Windows.Media.Ocr (built-in, không cần cài thêm)
     + Fallback: Tesseract nếu cần ngôn ngữ phức tạp
   - Gọi GPT API để dịch text OCR
   - Hiển thị popup kết quả gần vị trí chuột

3. GPT API
   - Model: gpt-4.1-mini (nhẹ, nhanh, rẻ)
   - Timeout: 15 giây
   - Retry: 1 lần nếu timeout
   - Prompt dịch:
     "Dịch đoạn sau sang tiếng Việt.
      Nếu có lỗi chính tả hoặc ký tự sai, hãy tự sửa trước khi dịch.
      Chỉ trả về nội dung đã dịch, không giải thích.
      Text: {input}"

4. POPUP UI
   - WPF Window, Always on top
   - Hiển thị gần vị trí chuột (tránh ra ngoài màn hình)
   - Loading state: "Đang dịch..."
   - UpdateTranslation() để cập nhật kết quả sau khi có
   - Tự đóng khi mất focus (Deactivated event)
   - Nút X để đóng thủ công
   - ForceClose() để đóng từ code

5. ERROR HANDLING
   - Không crash nếu API lỗi
   - Hiển thị "[Lỗi dịch] {message}" trong popup
   - Log đầy đủ vào file translator.log

========================
🏗️ CẤU TRÚC PROJECT
========================

TranslatorApp/
├── App.xaml / App.xaml.cs          ← Startup, tray icon, hotkey setup, orchestration
├── appsettings.json                ← ApiKey, ApiUrl, model config
├── Services/
│   ├── HotkeyService.cs            ← RegisterHotKey qua message-only window (WM_HOTKEY)
│   ├── ClipboardService.cs         ← AttachThreadInput + Ctrl+C + polling clipboard
│   ├── ScreenCaptureService.cs     ← Overlay selection UI + Bitmap capture
│   ├── OcrService.cs               ← Windows.Media.Ocr (async)
│   └── GptService.cs               ← HttpClient gọi OpenAI API
├── Windows/
│   ├── TranslationPopup.xaml/.cs   ← Popup hiển thị kết quả
│   └── ScreenOverlay.xaml/.cs      ← Fullscreen overlay để chọn vùng chụp
└── LogService.cs                   ← File logger đơn giản

========================
🧠 FLOW XỬ LÝ
========================

[Alt+T — Text mode]
User bôi đen text
→ Nhấn Alt+T
→ CaptureTargetWindow() ngay lập tức
→ AttachThreadInput + SetForegroundWindow
→ Release Alt key
→ Ctrl+C programmatic
→ Poll clipboard
→ Gọi GptService.TranslateAsync(text)
→ Hiển thị TranslationPopup

[Alt+S — Screenshot mode]
→ Nhấn Alt+S
→ Mở ScreenOverlay (fullscreen, cursor crosshair)
→ User kéo để chọn vùng
→ Đóng overlay, chụp Bitmap vùng đã chọn
→ OcrService.ExtractTextAsync(bitmap)
→ Gọi GptService.TranslateAsync(ocrText)
→ Hiển thị TranslationPopup

========================
📋 CHI TIẾT KỸ THUẬT QUAN TRỌNG
========================

ClipboardService:
- Dùng AttachThreadInput(currentThreadId, targetThreadId, true) trước SetForegroundWindow
- Sau khi xong: AttachThreadInput(..., false) trong finally
- Release VK_ALT (0x12) và VK_SHIFT (0x10) trước Ctrl+C
- Chạy toàn bộ trên STA thread riêng

HotkeyService:
- Tạo message-only window (HWND_MESSAGE = IntPtr(-3))
- RegisterHotKey với MOD_ALT (0x0001) + VK_T (0x54) cho Alt+T
- RegisterHotKey với MOD_ALT (0x0001) + VK_S (0x53) cho Alt+S
- Dùng HwndSourceHook để nhận WM_HOTKEY (0x0312)
- Phân biệt HOTKEY_ID để raise đúng event

ScreenOverlay:
- WindowStyle=None, AllowsTransparency=True, Topmost=True
- Background: #66000000 (semi-transparent đen)
- MouseDown: bắt đầu vẽ selection rect
- MouseMove: cập nhật vùng chọn theo chuột
- MouseUp: xác nhận vùng, đóng overlay
- Canvas + Rectangle để vẽ rubber-band selection

OcrService (Windows.Media.Ocr):
- Dùng OcrEngine.TryCreateFromUserProfileLanguages() hoặc OcrEngine.TryCreateFromLanguage(new Language("en"))
- Convert Bitmap → SoftwareBitmap qua BitmapDecoder
- OcrEngine.RecognizeAsync(softwareBitmap)
- Concat OcrResult.Lines[].Text

========================
📦 NUGET PACKAGES
========================

- Microsoft.Extensions.Configuration.Json  ← đọc appsettings.json
- System.Drawing.Common                    ← Bitmap capture
- Windows.Media.Ocr                        ← built-in WinRT OCR (không cần NuGet trên .NET 8 Windows)

========================
⚠️ YÊU CẦU QUAN TRỌNG
========================

- Không viết pseudo-code
- Không bỏ sót phần nào
- Code phải thực tế, chạy được
- Ưu tiên đơn giản, dễ hiểu, dễ mở rộng
- Ngăn mở nhiều popup/overlay cùng lúc (_isTranslating flag)

========================
💡 BONUS
========================

- Cache kết quả (text → translation) để giảm API call trùng lặp
- Log đầy đủ: hwnd, clipboard text, mouse pos, kết quả dịch, lỗi
- Tray icon tooltip: "Translator Tool (Alt+T / Alt+S)"
- Double-click tray: hiển thị MessageBox hướng dẫn
