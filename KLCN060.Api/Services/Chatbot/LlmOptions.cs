namespace KLCN060.Api.Services.Chatbot;

/// <summary>
/// Cấu hình chatbot dùng mô hình ngôn ngữ (LLM). Giao thức là "chat completions" tương thích OpenAI nên cùng một mã chạy được với
/// mô hình chạy trên máy (Ollama, LM Studio) lẫn gói miễn phí của Gemini, Groq, OpenRouter... Mặc định TẮT: chatbot vẫn là bản đối sánh từ khóa, không tốn gì.
/// </summary>
public sealed class LlmOptions
{
    public const string SectionName = "Chatbot:Llm";

    public bool Enabled { get; set; }

    /// <summary>Địa chỉ gốc của API tương thích OpenAI (có hoặc không có dấu "/" cuối). Mặc định là Ollama chạy trên máy.</summary>
    public string BaseUrl { get; set; } = "http://localhost:11434/v1/";

    public string Model { get; set; } = "";

    /// <summary>
    /// Các model dự phòng, thử theo thứ tự khi model chính hết hạn mức hoặc nhà cung cấp quá tải. Mỗi model có hạn mức riêng ở nhà cung cấp nên cộng lại được thêm lượt
    /// (VD gói miễn phí Gemini: mỗi model Flash-Lite 500 lượt mỗi ngày). Để trống = chỉ dùng <see cref="Model"/>. Các giới hạn lượt bên dưới tính RIÊNG cho từng model.
    /// </summary>
    public string[] FallbackModels { get; set; } = Array.Empty<string>();

    /// <summary>Danh sách model theo thứ tự ưu tiên: model chính rồi các model dự phòng (bỏ trống và trùng).</summary>
    public IReadOnlyList<string> DanhSachModel()
        => new[] { Model }.Concat(FallbackModels ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Khóa API (để trống với mô hình chạy trên máy). Chỉ đặt trong appsettings.Local.json, không commit.</summary>
    public string ApiKey { get; set; } = "";

    public int TimeoutSeconds { get; set; } = 25;
    public int MaxQuestionLength { get; set; } = 500;
    public int MaxOutputTokens { get; set; } = 400;
    public double Temperature { get; set; } = 0.2;

    /// <summary>Câu hỏi giống hệt (sau khi chuẩn hóa) được trả lại từ bộ nhớ đệm trong khoảng này, đỡ tốn lượt của gói miễn phí.</summary>
    public int CacheMinutes { get; set; } = 30;

    /// <summary>Tổng số lần gọi mô hình mỗi ngày; hết thì tự dùng chatbot từ khóa tới sáng hôm sau. 0 = không giới hạn.</summary>
    public int MaxCallsPerDay { get; set; } = 300;

    /// <summary>Tổng số lần gọi mô hình tối đa mỗi phút trên toàn hệ thống (mọi khách cộng lại), để không chạm hạn mức RPM của nơi cung cấp (gói miễn phí Gemini Flash-Lite: 15). Vượt thì câu đó dùng chatbot từ khóa. 0 = không giới hạn.</summary>
    public int MaxCallsPerMinute { get; set; } = 12;

    /// <summary>Lỗi liên tiếp từng này lần thì tạm ngừng gọi mô hình <see cref="PauseMinutes"/> phút (tránh chờ hết thời gian ở mọi câu hỏi khi mô hình chưa chạy).</summary>
    public int FailuresBeforePause { get; set; } = 3;
    public int PauseMinutes { get; set; } = 2;

    /// <summary>Nơi cung cấp báo hết hạn mức (HTTP 429) mà không nói bao lâu nữa hồi lại: ngừng gọi mô hình từng này phút rồi thử lại.</summary>
    public int QuotaPauseMinutes { get; set; } = 10;

    /// <summary>Số câu hỏi tối đa của mỗi địa chỉ IP trong một phút (chỉ áp dụng khi <see cref="Enabled"/> bật).</summary>
    public int RatePerMinutePerIp { get; set; } = 8;
}
