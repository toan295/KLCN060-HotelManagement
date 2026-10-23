using System.Text.RegularExpressions;
using KLCN060.Api.Middlewares;

namespace KLCN060.Api.Services;

/// <summary>
/// Kiểm tra định dạng SĐT/Email dùng chung cho Auth/Guests/Users-me. Giới hạn độ dài SĐT khớp CHÍNH XÁC
/// Khach.SoDT varchar(15) (Mục 2 tài liệu đặc tả) — chặn ở đây trước khi vào EF Core để tránh SqlParameter bị
/// cắt ngắn theo HasMaxLength(15) của cột khi so sánh, gây false-positive "đã tồn tại" cho SĐT > 15 ký tự.
/// </summary>
public static class ContactValidator
{
    private static readonly Regex SoDTPattern = new(@"^[0-9]{9,15}$", RegexOptions.Compiled);
    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    public static string KiemTraSoDT(string? soDT, string field = "soDT")
    {
        if (string.IsNullOrWhiteSpace(soDT))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_SO_DIEN_THOAI", "Số điện thoại là bắt buộc.", field);

        var sdt = soDT.Trim();
        if (!SoDTPattern.IsMatch(sdt))
            throw new ApiException(StatusCodes.Status400BadRequest, "SO_DIEN_THOAI_KHONG_HOP_LE", "Số điện thoại chỉ gồm 9-15 chữ số.", field);

        if (sdt.Distinct().Count() == 1)
            throw new ApiException(StatusCodes.Status400BadRequest, "SO_DIEN_THOAI_KHONG_HOP_LE", "Số điện thoại không hợp lệ (toàn một chữ số lặp lại).", field);

        return sdt;
    }

    /// <summary>Email lưu vào CSDL: cắt khoảng trắng hai đầu, rỗng thì coi như không có (để tra cứu/so sánh email luôn nhất quán).</summary>
    public static string? ChuanHoaEmail(string? email) => string.IsNullOrWhiteSpace(email) ? null : email.Trim();

    public static void KiemTraEmail(string? email, string field = "email")
    {
        if (string.IsNullOrWhiteSpace(email)) return;

        var mail = email.Trim();
        if (mail.Length > 100 || !EmailPattern.IsMatch(mail))
            throw new ApiException(StatusCodes.Status400BadRequest, "EMAIL_KHONG_HOP_LE", "Địa chỉ email không hợp lệ.", field);
    }
}
