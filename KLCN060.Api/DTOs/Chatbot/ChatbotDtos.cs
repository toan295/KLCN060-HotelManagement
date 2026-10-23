namespace KLCN060.Api.DTOs.Chatbot;

public class ChatbotAskRequest
{
    [global::System.ComponentModel.DataAnnotations.StringLength(1000)]
    public string CauHoi { get; set; } = null!;
}

public class ChatbotAskResponse
{
    public string CauTraLoi { get; set; } = null!;

    /// <summary>Các câu hỏi gợi ý để khách bấm chọn; chỉ có khi trả lời bằng chatbot từ khóa (AI tắt, lỗi, chậm, hết lượt hoặc tạm ngừng).</summary>
    public List<string> GoiY { get; set; } = new();
}
