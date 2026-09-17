namespace KLCN060.Web.Models.Home;

public class SearchViewModel
{
    public DateOnly? NgayNhan { get; set; }
    public DateOnly? NgayTra { get; set; }
    public int SoKhach { get; set; } = 1;

    /// <summary>
    /// Giai đoạn 2 CHƯA có /room-types/availability lọc theo tồn phòng thật (thuộc Giai đoạn 4)
    /// -> đây là TOÀN BỘ danh mục loại phòng, lọc theo Loại phòng/Mức giá được thực hiện PHÍA CLIENT bằng JavaScript (Mục 5).
    /// </summary>
    public List<RoomTypeViewModel> DanhSachLoaiPhong { get; set; } = new();
}
