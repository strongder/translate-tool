using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace TranslatorApp.Services;

public class GptService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly int _maxRetries;
    private readonly string _provider; // "openai" hoặc "gemini"

    private const string TRANSLATE_PROMPT =
        "Dịch đoạn sau sang tiếng Việt.\n" +
        "Nếu có lỗi chính tả hoặc ký tự sai, hãy tự sửa trước khi dịch.\n" +
        "Chỉ trả về nội dung đã dịch, không giải thích.\n\n" +
        "Text:\n{0}";

    public GptService(IConfiguration config)
    {
        _provider = (config["Provider"] ?? "openai").ToLower();

        if (_provider == "gemini")
        {
            _apiKey = config["Gemini:ApiKey"] ?? throw new InvalidOperationException("Thiếu Gemini:ApiKey trong appsettings.json");
            _model = config["Gemini:Model"] ?? "gemini-2.0-flash";
            _maxRetries = int.TryParse(config["Gemini:MaxRetries"], out var gr) ? gr : 2;
            int geminiTimeout = int.TryParse(config["Gemini:TimeoutSeconds"], out var gt) ? gt : 30;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(geminiTimeout) };
        }
        else
        {
            _apiKey = config["OpenAI:ApiKey"] ?? throw new InvalidOperationException("Thiếu OpenAI:ApiKey trong appsettings.json");
            _model = config["OpenAI:Model"] ?? "gpt-4.1-mini";
            _maxRetries = int.TryParse(config["OpenAI:MaxRetries"], out var or) ? or : 2;
            int openaiTimeout = int.TryParse(config["OpenAI:TimeoutSeconds"], out var ot) ? ot : 30;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(openaiTimeout) };
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        LogService.Info($"Provider: {_provider} | Model: {_model}");
    }

    public async Task<string> TranslateAsync(string text)
    {
        if (ClipboardService.TryGetCached(text, out var cached))
        {
            LogService.Info($"Cache hit: \"{text[..Math.Min(40, text.Length)]}...\"");
            return $"[Cache] {cached}";
        }

        LogService.Info($"Gọi API | provider={_provider} | model={_model} | text=\"{text[..Math.Min(60, text.Length)]}\"");

        var prompt = string.Format(TRANSLATE_PROMPT, text);

        for (int attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                var translation = _provider == "gemini"
                    ? await CallGeminiAsync(prompt)
                    : await CallOpenAiAsync(prompt);

                LogService.Info($"Dịch thành công | {text.Length} chars");
                ClipboardService.SetCache(text, translation);
                return translation;
            }
            catch (TaskCanceledException ex)
            {
                LogService.Error($"Timeout (attempt {attempt + 1}/{_maxRetries + 1})", ex);
                if (attempt == _maxRetries)
                    return $"[Lỗi] Timeout sau {_httpClient.Timeout.TotalSeconds}s";
                await Task.Delay(500 * (attempt + 1));
            }
            catch (Exception ex) when (attempt < _maxRetries)
            {
                LogService.Error($"Lỗi attempt {attempt + 1}, sẽ retry", ex);
                await Task.Delay(500 * (attempt + 1));
            }
            catch (Exception ex)
            {
                LogService.Error("Lỗi cuối cùng, không retry", ex);
                return $"[Lỗi] {ex.Message}";
            }
        }

        return "[Lỗi] Không thể kết nối API sau nhiều lần thử";
    }

    private async Task<string> CallOpenAiAsync(string prompt)
    {
        var requestBody = new
        {
            model = _model,
            messages = new[] { new { role = "user", content = prompt } },
            temperature = 0.3,
            max_tokens = 2000
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(
            "https://api.openai.com/v1/chat/completions", content);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"OpenAI lỗi {(int)response.StatusCode}: {errorBody}");
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "[Không có kết quả]";
    }

    private async Task<string> CallGeminiAsync(string prompt)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                temperature = 0.3,
                maxOutputTokens = 2000
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, content);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Gemini lỗi {(int)response.StatusCode}: {errorBody}");
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);
        return doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? "[Không có kết quả]";
    }
}
