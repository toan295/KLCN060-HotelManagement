namespace KLCN060.Api.DTOs.Media;

public class RoomImageDto
{
    /// <summary>Vị trí ảnh: "LP01" (ảnh đại diện), "LP01-1".."LP01-3" (ảnh chi tiết) hoặc "hero" (banner trang chủ).</summary>
    public string Slot { get; set; } = null!;
    public string TenFile { get; set; } = null!;
    /// <summary>Đường dẫn tương đối tới file, dạng /media/rooms/LP01.jpg (ghép sau địa chỉ API).</summary>
    public string Url { get; set; } = null!;
    /// <summary>Thay đổi mỗi khi ảnh được thay -> dùng làm ?v= để trình duyệt không giữ ảnh cũ.</summary>
    public long PhienBan { get; set; }
    public long KichThuocByte { get; set; }
}
