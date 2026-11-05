using KLCN060.Domain;

namespace KLCN060.Api.Services.Chatbot;

/// <summary>
/// "Số phiên bản" của dữ liệu mà chatbot đọc (loại phòng, phòng, dịch vụ, gói, khuyến mãi, bảng giá, câu hỏi thường gặp, đặt/nhận phòng).
/// Mỗi lần lưu thay đổi dữ liệu này, số tăng lên và bộ nhớ đệm của chatbot (bối cảnh gửi cho AI, câu trả lời đã nhớ) tự mất hiệu lực,
/// nên Quản lý sửa giá/loại phòng/dịch vụ xong thì khách hỏi chatbot là thấy ngay, không phải chờ bộ nhớ đệm hết hạn.
/// </summary>
public static class ChatbotDuLieu
{
    private static long _phienBan;

    public static long PhienBan => Interlocked.Read(ref _phienBan);

    public static void DaThayDoi() => Interlocked.Increment(ref _phienBan);

    /// <summary>Thực thể nào đổi thì chatbot cần đọc lại dữ liệu.</summary>
    public static bool LienQuan(object thucThe) => thucThe is LoaiPhong or Phong or DichVu or KhuyenMai
        or CauHoiThuongGap or PhieuDatPhong or ChiTietPhieuDat or PhieuNhanPhong or ChiTietPhieuNhan;
}
