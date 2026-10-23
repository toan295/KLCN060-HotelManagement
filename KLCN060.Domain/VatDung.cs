using KLCN060.Domain.Enums;

namespace KLCN060.Domain;

/// <summary>
/// Danh mục đồ dùng trong phòng cần kiểm (khăn, ga, gối, nước uống, minibar, amenities...). SoLuongChuan là định mức mỗi phòng.
/// Có <see cref="MaDV"/> = tiêu thụ được tính phí cho khách theo giá dịch vụ đó (minibar, nước uống); không có = chỉ theo dõi hao hụt.
/// </summary>
public class VatDung
{
    public string MaVatDung { get; set; } = null!;
    public string TenVatDung { get; set; } = null!;
    public NhomVatDung Nhom { get; set; }
    public string DonViTinh { get; set; } = null!;
    /// <summary>Định mức theo đơn vị của <see cref="CachTinh"/>: mỗi phòng, mỗi giường hoặc mỗi người tiêu chuẩn.</summary>
    public int SoLuongChuan { get; set; }
    public CachTinhDinhMuc CachTinh { get; set; } = CachTinhDinhMuc.MOI_PHONG;
    public string? MaDV { get; set; }
    public bool HoatDong { get; set; } = true;

    public DichVu? DichVu { get; set; }

    /// <summary>Các loại phòng có đồ dùng này. RỖNG = áp dụng cho mọi loại phòng.</summary>
    public ICollection<VatDungLoaiPhong> LoaiPhongApDungs { get; set; } = new List<VatDungLoaiPhong>();

    public bool ApDungChoLoai(string maLoai) => LoaiPhongApDungs.Count == 0 || LoaiPhongApDungs.Any(x => x.MaLoai == maLoai);

    /// <summary>Định mức thực tế của một phòng thuộc loại <paramref name="loai"/>: phòng 2 giường cần gấp đôi ga/gối; phòng 3 người cần gấp 1,5 lần khăn của phòng 2 người...</summary>
    public int DinhMucCho(LoaiPhong loai) => CachTinh switch
    {
        CachTinhDinhMuc.MOI_GIUONG => SoLuongChuan * loai.SoGiuong,
        CachTinhDinhMuc.MOI_NGUOI => SoLuongChuan * Math.Max(1, loai.SoNguoiTieuChuan),
        _ => SoLuongChuan
    };
    public ICollection<ChiTietKiemPhong> ChiTietKiemPhongs { get; set; } = new List<ChiTietKiemPhong>();
}

/// <summary>Đồ dùng áp dụng cho loại phòng nào (ví dụ áo choàng tắm chỉ có ở phòng Deluxe). Không có dòng nào cho một đồ dùng = áp dụng mọi loại phòng.</summary>
public class VatDungLoaiPhong
{
    public string MaVatDung { get; set; } = null!;
    public string MaLoai { get; set; } = null!;

    public VatDung VatDung { get; set; } = null!;
    public LoaiPhong LoaiPhong { get; set; } = null!;
}
