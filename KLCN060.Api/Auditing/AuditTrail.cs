using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace KLCN060.Api.Auditing;

/// <summary>
/// Sổ ghi nhật ký của MỘT request: dịch vụ nghiệp vụ gọi <see cref="Ghi"/> ngay trước lệnh lưu chính, dòng nhật ký được lưu CÙNG lần lưu đó
/// (cùng transaction với nghiệp vụ) bởi <see cref="AuditSaveChangesInterceptor"/>. Đã có dòng nhật ký riêng cho request thì
/// <c>AuditLoggingMiddleware</c> không ghi thêm dòng chung chung "POST_/api/..." nữa.
/// Không phụ thuộc DbContext (DbContext tạo interceptor, interceptor cần sổ này) nên chỉ giữ hàng chờ trong bộ nhớ.
/// </summary>
public interface IAuditTrail
{
    /// <summary>Tên đăng nhập của người đang gọi (null nếu không có request/chưa đăng nhập).</summary>
    string? TenDN { get; }
    string? DiaChiIP { get; }

    /// <summary>true khi request này đã có dòng nhật ký riêng (không cần ghi dòng chung).</summary>
    bool DaGhi { get; }

    /// <summary>Lý do gắn vào các dòng lịch sử tình trạng phòng sinh ra ở lần lưu kế tiếp (VD "Bảo trì máy lạnh", "Nhận phòng").</summary>
    string? LyDoTinhTrangPhong { get; set; }

    void Ghi(string hanhDong, string? doiTuong = null, string? chiTiet = null);
}

public sealed class AuditTrail : IAuditTrail
{
    private readonly IHttpContextAccessor _http;
    private readonly List<NhatKyThaoTac> _choGhi = new();

    public AuditTrail(IHttpContextAccessor http) => _http = http;

    public string? TenDN
    {
        get
        {
            var user = _http.HttpContext?.User;
            return user?.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? user?.FindFirstValue(ClaimTypes.NameIdentifier);
        }
    }

    public string? DiaChiIP => _http.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public bool DaGhi { get; private set; }

    public string? LyDoTinhTrangPhong { get; set; }

    public void Ghi(string hanhDong, string? doiTuong = null, string? chiTiet = null)
    {
        var tenDN = TenDN;
        if (string.IsNullOrEmpty(tenDN)) return; // khóa ngoại tới TaiKhoan: không có người gọi thì không ghi được
        _choGhi.Add(new NhatKyThaoTac
        {
            MaTaiKhoan = tenDN,
            HanhDong = hanhDong.Length > 100 ? hanhDong[..100] : hanhDong,
            DoiTuongTacDong = doiTuong is { Length: > 100 } ? doiTuong[..100] : doiTuong,
            ThoiGian = DateTime.Now,
            DiaChiIP = DiaChiIP,
            ChiTiet = chiTiet
        });
        DaGhi = true;
    }

    internal List<NhatKyThaoTac> LayHangCho()
    {
        var ds = _choGhi.ToList();
        _choGhi.Clear();
        return ds;
    }

    internal void DanhDauDaGhi() => DaGhi = true;
}

/// <summary>Trước mỗi lần lưu: (1) đưa các dòng nhật ký đang chờ vào cùng lần lưu; (2) ghi lịch sử mỗi lần tình trạng phòng đổi.</summary>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly AuditTrail _audit;
    private readonly byte[] _khoaKy;

    public AuditSaveChangesInterceptor(AuditTrail audit, IConfiguration configuration)
    {
        _audit = audit;
        _khoaKy = KhoaKy(configuration);
    }

    /// <summary>Khóa ký nhật ký: Audit:SigningKey, nếu không có thì dùng Jwt:Key (bí mật, nằm ngoài CSDL nên người chỉ có quyền CSDL không làm giả được chữ ký).</summary>
    internal static byte[] KhoaKy(IConfiguration configuration)
        => Encoding.UTF8.GetBytes(configuration["Audit:SigningKey"] ?? configuration["Jwt:Key"] ?? "KLCN060-audit-khong-co-khoa");

    internal static string NoiDungKy(NhatKyThaoTac x)
        => string.Join("|", x.MaTaiKhoan, x.HanhDong, x.DoiTuongTacDong ?? "", x.ThoiGian.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", System.Globalization.CultureInfo.InvariantCulture),
            x.DiaChiIP ?? "", x.ChiTiet ?? "", x.KetQua, x.MaLoi ?? "");

    internal static string TinhChuKy(byte[] khoa, NhatKyThaoTac x)
        => Convert.ToHexString(HMACSHA256.HashData(khoa, Encoding.UTF8.GetBytes(NoiDungKy(x))));

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        XuLy(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        XuLy(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void XuLy(DbContext? context)
    {
        if (context is null) return;

        foreach (var nhatKy in _audit.LayHangCho())
            context.Set<NhatKyThaoTac>().Add(nhatKy);

        // Mọi dòng nhật ký mới (dù do dịch vụ, middleware hay mã cũ thêm trực tiếp) đều được ký trước khi lưu.
        foreach (var entry in context.ChangeTracker.Entries<NhatKyThaoTac>().Where(x => x.State == EntityState.Added))
        {
            var nk = entry.Entity;
            if (string.IsNullOrEmpty(nk.KetQua)) nk.KetQua = "THANH_CONG";
            nk.ChuKy = TinhChuKy(_khoaKy, nk);
        }

        var bayGio = DateTime.Now;
        foreach (var entry in context.ChangeTracker.Entries<Phong>().Where(x => x.State == EntityState.Modified).ToList())
        {
            var tinhTrang = entry.Property(x => x.TinhTrang);
            if (!tinhTrang.IsModified || tinhTrang.OriginalValue == tinhTrang.CurrentValue) continue;

            context.Set<LichSuTinhTrangPhong>().Add(new LichSuTinhTrangPhong
            {
                MaPhong = entry.Entity.MaPhong,
                TuTinhTrang = tinhTrang.OriginalValue,
                DenTinhTrang = tinhTrang.CurrentValue,
                ThoiGian = bayGio,
                MaTaiKhoan = _audit.TenDN,
                LyDo = _audit.LyDoTinhTrangPhong is { Length: > 255 } lyDo ? lyDo[..255] : _audit.LyDoTinhTrangPhong
            });
        }

        // Dịch vụ cũ vẫn tự thêm NhatKyThaoTac: coi như request đã có nhật ký riêng.
        if (context.ChangeTracker.Entries<NhatKyThaoTac>().Any(x => x.State == EntityState.Added))
            _audit.DanhDauDaGhi();
    }
}
