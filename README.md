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
cd TranslatorApp
dotnet build
dotnet run
```

Hoặc publish ra file .exe:
```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

## Sử dụng

1. Chạy app → icon xuất hiện ở System Tray (góc phải taskbar)
2. Bôi đen văn bản trong bất kỳ ứng dụng nào
3. Nhấn **Ctrl + Shift + T**
4. Popup xuất hiện gần con trỏ chuột với bản dịch tiếng Việt
5. Popup tự đóng sau 5 giây, hoặc click X để đóng sớm
6. Click vào popup để giữ không tự đóng

## Cấu trúc project

```
TranslatorApp/
├── App.xaml                    # Entry point, system tray
├── App.xaml.cs                 # Logic chính: hotkey → clipboard → dịch → popup
├── appsettings.json            # Cấu hình API key, model, hotkey
├── Services/
│   ├── HotkeyService.cs        # Đăng ký Ctrl+Shift+T toàn cục
│   ├── ClipboardService.cs     # Lấy text + cache
│   └── GptService.cs           # Gọi OpenAI API, retry, cache
└── Windows/
    ├── TranslationPopup.xaml   # UI popup
    └── TranslationPopup.xaml.cs
```

## Tính năng

- ✅ Global hotkey Ctrl+Shift+T (hoạt động ở mọi ứng dụng)
- ✅ System tray icon (không có cửa sổ chính)
- ✅ Popup always-on-top gần vị trí chuột
- ✅ Tự đóng sau 5 giây với progress bar
- ✅ Cache kết quả (không gọi API khi text trùng)
- ✅ Retry tự động khi API lỗi
- ✅ Hiển thị trạng thái "Đang dịch..." trong khi chờ
