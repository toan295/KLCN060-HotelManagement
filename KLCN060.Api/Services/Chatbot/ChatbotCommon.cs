using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using KLCN060.Domain;
using KLCN060.Domain.Enums;

namespace KLCN060.Api.Services.Chatbot;

/// <summary>Câu trả lời của chatbot kèm các câu hỏi gợi ý để khách bấm chọn (chỉ có khi trả lời bằng bản từ khóa).</summary>
public sealed record ChatbotAnswer(string CauTraLoi, IReadOnlyList<string> GoiY)
{
    public static ChatbotAnswer ChiCoCauTraLoi(string cauTraLoi) => new(cauTraLoi, Array.Empty<string>());
}

public static class ChatbotGoiY
{
    public const string Hotline = "1900 0001";

    /// <summary>
    /// Các câu hỏi gợi ý. Mỗi câu PHẢI cho ra câu trả lời có nội dung ở chế độ từ khóa (có test kiểm tra), vì khi AI tắt hoặc lỗi
    /// khách vẫn bấm vào gợi ý và nhận được câu trả lời thật chứ không phải câu xin lỗi.
    /// </summary>
    public static readonly IReadOnlyList<string> MacDinh = new[]
    {
        "Giá các loại phòng",
        "Chính sách đặt cọc và hủy phòng",
        "Cách đặt phòng online",
        "Giờ nhận phòng và trả phòng",
        "Khuyến mãi đang có",
        "Khách sạn có dịch vụ gì?"
    };
}

/// <summary>Định dạng văn bản dùng chung cho lời nhắc của AI và câu trả lời của bản từ khóa.</summary>
internal static class ChatbotFormat
{
    public static readonly CultureInfo Vn = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Bỏ dấu tiếng Việt và đưa về chữ thường, để câu hỏi có dấu vẫn khớp được từ khoá không dấu.</summary>
    public static string BoDau(string? text)
    {
        var phanRa = (text ?? "").Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in phanRa)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd');
    }

    /// <summary>Dạng so sánh của một câu hỏi: không dấu, chữ thường, chỉ còn chữ và số cách nhau một dấu cách ("Giá phòng?" và "gia  phong" như nhau).</summary>
    public static string KhoaHoa(string? text) => Regex.Replace(BoDau(text), @"[^a-z0-9]+", " ").Trim();

    public static string Tien(decimal x) => $"{Math.Round(x, 0, MidpointRounding.AwayFromZero).ToString("N0", Vn)}đ";

    public static string Thu(DayOfWeek d) => d switch
    {
        DayOfWeek.Monday => "Thứ Hai", DayOfWeek.Tuesday => "Thứ Ba", DayOfWeek.Wednesday => "Thứ Tư", DayOfWeek.Thursday => "Thứ Năm",
        DayOfWeek.Friday => "Thứ Sáu", DayOfWeek.Saturday => "Thứ Bảy", _ => "Chủ nhật"
    };

    public static string ThuNgan(DayOfWeek d) => d switch
    {
        DayOfWeek.Monday => "T2", DayOfWeek.Tuesday => "T3", DayOfWeek.Wednesday => "T4", DayOfWeek.Thursday => "T5",
        DayOfWeek.Friday => "T6", DayOfWeek.Saturday => "T7", _ => "CN"
    };

    public static string MoTaKhuyenMai(KhuyenMai k)
    {
        var giam = k.LoaiKM == LoaiKhuyenMai.PHAN_TRAM ? $"giảm {(k.PhanTramKM ?? k.GiaTri ?? 0).ToString("0.#", Vn)}%" : $"giảm {Tien(k.GiaTri ?? 0)}";
        var s = $"{k.TenKM}: {giam}, từ {k.NgayBatDau:dd/MM/yyyy} đến {k.NgayKetThuc:dd/MM/yyyy}";
        if (k.SoDemToiThieu is { } d) s += $", ở tối thiểu {d} đêm";
        if (k.TongTienToiThieu is { } t) s += $", tiền phòng tối thiểu {Tien(t)}";
        if (!string.IsNullOrWhiteSpace(k.DieuKien)) s += $". {k.DieuKien.Trim()}";
        return s;
    }
}
