namespace KLCN060.Domain;

public class LoaiPhong
{
    public string MaLoai { get; set; } = null!;
    public string TenLoai { get; set; } = null!;
    public int SoNguoiTieuChuan { get; set; }
    public decimal DonGia { get; set; }
    public decimal PhuThu { get; set; }

    // Thông tin mô tả (hiển thị cho khách trên Website và cho nhân viên): không ảnh hưởng giá / tồn phòng.
    /// <summary>Diện tích phòng (m²); null = chưa nhập.</summary>
    public decimal? DienTich { get; set; }
    /// <summary>Loại giường, VD "1 giường đôi", "2 giường đơn".</summary>
    public string? LoaiGiuong { get; set; }
    /// <summary>Tiện nghi, cách nhau bằng dấu phẩy: "Wifi, Điều hòa, TV".</summary>
    public string? TienNghi { get; set; }
    public string? MoTa { get; set; }

    /// <summary>
    /// Số giường suy ra từ mô tả loại giường (cộng các số đứng trước chữ "giường": "1 giường đôi + 1 giường đơn" = 2; "2 giường đơn" = 2).
    /// Không đọc được thì coi là 1 giường.
    /// </summary>
    public int SoGiuong
    {
        get
        {
            if (string.IsNullOrWhiteSpace(LoaiGiuong)) return 1;
            var tong = System.Text.RegularExpressions.Regex.Matches(LoaiGiuong, @"(\d+)\s*giường", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                .Sum(m => int.Parse(m.Groups[1].Value));
            return tong > 0 ? tong : 1;
        }
    }

    public ICollection<Phong> Phongs { get; set; } = new List<Phong>();
}
