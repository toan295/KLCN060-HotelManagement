namespace KLCN060.Api.Services.EInvoice;

/// <summary>Cấu hình hóa đơn điện tử (mục "EInvoice" trong appsettings.json / user-secrets).</summary>
public class EInvoiceOptions
{
    public const string SectionName = "EInvoice";

    /// <summary>Tên nhà cung cấp HĐĐT đang dùng. "NONE" = chưa ký hợp đồng/kết nối: hóa đơn chỉ lưu ở trạng thái chờ phát hành.</summary>
    public string Provider { get; set; } = "NONE";

    /// <summary>
    /// Thuế suất GTGT (%) áp cho dịch vụ lưu trú. Mặc định 10; nếu đang áp dụng mức giảm theo chính sách của Chính phủ thì đặt 8.
    /// Kế toán của khách sạn phải xác nhận mức này; giá trên hóa đơn thanh toán được hiểu là đã gồm VAT.
    /// </summary>
    public decimal ThueSuatGTGT { get; set; } = 10m;

    /// <summary>Địa chỉ trang tra cứu hóa đơn của cơ quan thuế/nhà cung cấp (hiển thị cho khách khi nhà cung cấp không trả về liên kết riêng).</summary>
    public string? TrangTraCuu { get; set; }

    /// <summary>Kết nối MISA meInvoice, dùng khi Provider = "MISA".</summary>
    public MisaOptions Misa { get; set; } = new();
}

/// <summary>Một dòng hàng hóa/dịch vụ gửi lên nhà cung cấp HĐĐT.</summary>
public sealed record DongHoaDonDienTu(string Ten, int SoLuong, decimal DonGia, decimal ThanhTien);

/// <summary>Dữ liệu gửi nhà cung cấp để phát hành. <see cref="MaThamChieu"/> ổn định theo hóa đơn để nhà cung cấp chống phát hành trùng khi gọi lại.</summary>
public sealed record YeuCauPhatHanhHoaDonDienTu(
    string MaThamChieu, DateTime NgayLap,
    string TenNguoiMua, string? TenDonVi, string? MaSoThue, string? DiaChi, string? Email,
    decimal ThueSuat, decimal TienChuaThue, decimal TienThue, decimal TongThanhToan,
    IReadOnlyList<DongHoaDonDienTu> Dong);

/// <summary>Kết quả nhà cung cấp trả về.</summary>
public sealed record KetQuaPhatHanhHoaDonDienTu(
    bool ThanhCong, bool DangXuLy, string? MauSo, string? KyHieu, string? SoHoaDon, string? MaCQT,
    DateTime? NgayPhatHanh, string? MaTraCuu, string? LienKetTraCuu, string? MaThamChieuNhaCungCap, string? Loi)
{
    public static KetQuaPhatHanhHoaDonDienTu That(string loi) => new(false, false, null, null, null, null, null, null, null, null, loi);
}

/// <summary>
/// Cổng nối tới nhà cung cấp hóa đơn điện tử đã được Tổng cục Thuế công nhận (MISA meInvoice, VNPT Invoice, Viettel S-Invoice, BKAV eInvoice...).
/// Mỗi nhà cung cấp là một lớp cài đặt riêng, đăng ký theo <see cref="Ten"/> và chọn bằng EInvoice:Provider.
/// </summary>
public interface IEInvoiceProvider
{
    string Ten { get; }

    /// <summary>false = thiếu thông tin kết nối (hợp đồng, tài khoản API...) nên chưa gửi được.</summary>
    bool DaCauHinh { get; }

    Task<KetQuaPhatHanhHoaDonDienTu> PhatHanhAsync(YeuCauPhatHanhHoaDonDienTu yeuCau, CancellationToken cancellationToken = default);
}

/// <summary>Mặc định khi chưa ký hợp đồng với nhà cung cấp nào: KHÔNG tự bịa số hóa đơn hay mã cơ quan thuế.</summary>
public sealed class ChuaKetNoiEInvoiceProvider : IEInvoiceProvider
{
    public string Ten => "NONE";
    public bool DaCauHinh => false;

    public Task<KetQuaPhatHanhHoaDonDienTu> PhatHanhAsync(YeuCauPhatHanhHoaDonDienTu yeuCau, CancellationToken cancellationToken = default)
        => Task.FromResult(KetQuaPhatHanhHoaDonDienTu.That("Chưa kết nối nhà cung cấp hóa đơn điện tử (EInvoice:Provider)."));
}
