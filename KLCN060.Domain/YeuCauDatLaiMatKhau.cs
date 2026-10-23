namespace KLCN060.Domain;

/// <summary>
/// Một yêu cầu "quên mật khẩu" gồm 2 bước. Bước 1: gửi MÃ XÁC NHẬN 6 số tới email (chỉ lưu băm trong <see cref="MaXacNhanHash"/>, sai quá số lần cho phép thì hết hiệu lực).
/// Bước 2: nhập đúng mã thì hệ thống cấp MÃ ĐẶT LẠI (<see cref="TokenHash"/>) để đổi mật khẩu. Cả hai chỉ lưu BĂM (SHA-256), không lưu bản thô: nếu CSDL bị lộ thì kẻ xấu
/// vẫn không dùng được các mã đang còn hiệu lực. Mã dùng được đúng một lần và hết hạn sau vài phút.
/// </summary>
public class YeuCauDatLaiMatKhau
{
    public int MaYeuCau { get; set; }
    public string TenDN { get; set; } = null!;
    public string TokenHash { get; set; } = null!;
    public DateTime NgayTao { get; set; }
    public DateTime HanDung { get; set; }
    public DateTime? NgaySuDung { get; set; }
    public string? DiaChiIP { get; set; }

    /// <summary>Băm mã xác nhận 6 số gửi qua email; null khi mã đã được nhập đúng (đã đổi lấy <see cref="TokenHash"/>).</summary>
    public string? MaXacNhanHash { get; set; }

    /// <summary>Số lần đã nhập mã xác nhận (kể cả lần đúng). Quá giới hạn thì yêu cầu hết hiệu lực.</summary>
    public int SoLanNhapMa { get; set; }

    public TaiKhoan TaiKhoan { get; set; } = null!;
}
