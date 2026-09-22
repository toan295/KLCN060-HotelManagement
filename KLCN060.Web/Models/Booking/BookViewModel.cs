using System.ComponentModel.DataAnnotations;

namespace KLCN060.Web.Models.Booking;

/// <summary>W6 - Đặt phòng cá nhân. MaLoai đã biết sẵn từ link ở W3, không có trên form cho khách chọn lại.</summary>
public class BookViewModel
{
    public string MaLoai { get; set; } = null!;
    public string? TenLoai { get; set; }
    public decimal? DonGia { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày nhận phòng.")]
    [Display(Name = "Ngày nhận phòng")]
    [DataType(DataType.Date)]
    public DateOnly? NgayNhan { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày trả phòng.")]
    [Display(Name = "Ngày trả phòng")]
    [DataType(DataType.Date)]
    public DateOnly? NgayTra { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập số khách.")]
    [Range(1, 20, ErrorMessage = "Số khách phải từ 1 đến 20.")]
    [Display(Name = "Số khách")]
    public int SoKhach { get; set; } = 1;
}
