using System.ComponentModel.DataAnnotations;
using KLCN060.Web.Models.Home;

namespace KLCN060.Web.Models.Booking;

/// <summary>W7 - Đặt phòng theo đoàn: nhiều dòng { maLoai, soLuong } thêm/bớt động bằng JS.</summary>
public class BookGroupViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn ngày nhận phòng.")]
    [Display(Name = "Ngày nhận phòng")]
    [DataType(DataType.Date)]
    public DateOnly? NgayNhan { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày trả phòng.")]
    [Display(Name = "Ngày trả phòng")]
    [DataType(DataType.Date)]
    public DateOnly? NgayTra { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập số khách.")]
    [Range(1, 200, ErrorMessage = "Số khách phải từ 1 đến 200.")]
    [Display(Name = "Tổng số khách của đoàn")]
    public int? SoKhach { get; set; } = 1;

    public List<BookGroupRoomLineViewModel> PhongCanDat { get; set; } = new();

    /// <summary>Danh sách loại phòng để đổ vào dropdown chọn ở mỗi dòng (lấy từ GET /room-types).</summary>
    public List<RoomTypeViewModel> DanhSachLoaiPhong { get; set; } = new();
}

public class BookGroupRoomLineViewModel
{
    public string? MaLoai { get; set; }

    [Range(1, 50, ErrorMessage = "Số lượng phòng phải từ 1 đến 50.")]
    public int SoLuong { get; set; } = 1;
}
