namespace KLCN060.Web.Models.Home;

public class SearchViewModel
{
    public DateOnly? NgayNhan { get; set; }
    public DateOnly? NgayTra { get; set; }
    public int SoKhach { get; set; } = 1;

    /// <summary>Có ngày nhận/trả hợp lệ hay không -> dùng SoPhongTrong (tồn phòng thật) thay vì danh mục tĩnh.</summary>
    public bool DaLocTheoNgay { get; set; }

    /// <summary>
    /// Khi DaLocTheoNgay = true: kết quả từ GET /room-types/availability (tồn phòng thật theo đúng khoảng ngày/số khách).
    /// Khi false (chưa chọn ngày): toàn bộ danh mục loại phòng, SoPhongTrong = -1 (không xác định, không hiển thị badge tồn kho).
    /// </summary>
    public List<RoomTypeAvailabilityViewModel> DanhSachLoaiPhong { get; set; } = new();
}
