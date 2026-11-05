namespace KLCN060.Web.Models;

public class ErrorViewModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    public int MaTrangThai { get; set; } = 500;
    public string TieuDe { get; set; } = "Đã xảy ra lỗi";
    public string NoiDung { get; set; } = "Hệ thống gặp sự cố khi xử lý yêu cầu của bạn. Vui lòng thử lại sau ít phút.";

    public static ErrorViewModel Tu(int ma, string? requestId) => ma switch
    {
        400 => new() { MaTrangThai = ma, RequestId = requestId, TieuDe = "Yêu cầu không hợp lệ", NoiDung = "Yêu cầu không hợp lệ hoặc biểu mẫu đã hết hạn (ví dụ để trang mở quá lâu). Vui lòng tải lại trang rồi thử lại." },
        401 or 403 => new() { MaTrangThai = ma, RequestId = requestId, TieuDe = "Bạn không có quyền truy cập", NoiDung = "Vui lòng đăng nhập bằng tài khoản phù hợp để xem nội dung này." },
        404 => new() { MaTrangThai = ma, RequestId = requestId, TieuDe = "Không tìm thấy trang", NoiDung = "Trang bạn tìm không tồn tại, đã bị xóa hoặc đường dẫn chưa đúng." },
        405 => new() { MaTrangThai = ma, RequestId = requestId, TieuDe = "Thao tác không được hỗ trợ", NoiDung = "Đường dẫn này không dùng được theo cách bạn vừa thực hiện. Vui lòng quay lại trang trước." },
        429 => new() { MaTrangThai = ma, RequestId = requestId, TieuDe = "Quá nhiều yêu cầu", NoiDung = "Bạn đã thao tác quá nhanh. Vui lòng chờ ít phút rồi thử lại." },
        503 => new() { MaTrangThai = ma, RequestId = requestId, TieuDe = "Hệ thống tạm thời không phản hồi", NoiDung = "Máy chủ đang bận hoặc đang bảo trì. Vui lòng thử lại sau ít phút." },
        _ => new() { MaTrangThai = ma, RequestId = requestId }
    };
}
