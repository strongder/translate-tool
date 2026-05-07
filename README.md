# Translator Tool - Hướng dẫn cài đặt và sử dụng

## Cài đặt

### Yêu cầu
- .NET 8 SDK (https://dotnet.microsoft.com/download)
- Windows 10/11
- API Key từ OpenAI

### Bước 1: Cấu hình API Key
Mở file `appsettings.json` và thay `YOUR_OPENAI_API_KEY_HERE` bằng API key của bạn:

```json
{
  "OpenAI": {
    "ApiKey": "sk-...",
    "Model": "gpt-4.1-mini"
  }
}
```

### Bước 2: Build và chạy
```bash
# chạy trực tiếp từ source
 dotnet build
 dotnet run
```

Hoặc publish ra file .exe:
```bash
 dotnet publish -c Release -r win-x64 --self-contained true
```

## Sử dụng

1. Chạy app → icon xuất hiện ở System Tray (góc phải taskbar)
2. Bôi đen văn bản nhấn Alt + T để dịch hoặc Alt + S để chụp màn hình
3. Popup xuất hiện gần con trỏ chuột với bản dịch tiếng Việt

## Cấu trúc project

> Cây thư mục dưới đây được cập nhật để khớp với cấu trúc thực tế của repository.

```
.
├── .gitignore
├── App.xaml
├── App.xaml.cs
├── CLAUDE.md
├── README.md
├── TranslatorApp.csproj
├── idea.md
├── Services/
│   ├── ClipboardService.cs
│   ├── GptService.cs
│   ├── HotkeyService.cs
│   ├── LogService.cs
│   └── OcrService.cs
└── Windows/
    ├── ScreenOverlay.xaml
    ├── ScreenOverlay.xaml.cs
    ├── TranslationPopup.xaml
    └── TranslationPopup.xaml.cs
```

## Tính năng

- ✅ Global hotkey Alt + T (hoạt động ở mọi ứng dụng)
- ✅ System tray icon (không có cửa sổ chính)
- ✅ Popup always-on-top gần vị trí chuột
- ✅ Cache kết quả (không gọi API khi text trùng)
- ✅ Retry tự động khi API lỗi
- ✅ Hiển thị trạng thái "Đang dịch..." trong khi chờ
