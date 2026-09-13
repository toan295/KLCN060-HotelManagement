using System.Globalization;
using System.Text;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

/// <summary>Chatbot cơ bản đối sánh từ khoá — KHÔNG dùng mô hình AI/NLP (Mục 6.13, CLAUDE.md).</summary>
public class ChatbotService : IChatbotService
{
    private const string CauTraLoiMacDinh =
        "Xin lỗi, tôi chưa có câu trả lời phù hợp cho câu hỏi này. Quý khách vui lòng liên hệ trực tiếp lễ tân để được hỗ trợ thêm.";

    private readonly KLCN060DbContext _context;

    public ChatbotService(KLCN060DbContext context)
    {
        _context = context;
    }

    public async Task<string> AskAsync(string cauHoi)
    {
        var cauHoiChuanHoa = ChuanHoa(cauHoi);
        var faqs = await _context.CauHoiThuongGaps.ToListAsync();

        foreach (var faq in faqs)
        {
            var tuKhoas = faq.TuKhoa.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tuKhoas.Any(tuKhoa => cauHoiChuanHoa.Contains(ChuanHoa(tuKhoa))))
                return faq.CauTraLoi;
        }

        return CauTraLoiMacDinh;
    }

    /// <summary>Bỏ dấu tiếng Việt và đưa về chữ thường, để câu hỏi có dấu vẫn khớp được từ khoá không dấu.</summary>
    private static string ChuanHoa(string text)
    {
        var chuThuong = text.Trim().ToLowerInvariant();
        var phanRa = chuThuong.Normalize(NormalizationForm.FormD);

        var sb = new StringBuilder();
        foreach (var c in phanRa)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd');
    }
}
