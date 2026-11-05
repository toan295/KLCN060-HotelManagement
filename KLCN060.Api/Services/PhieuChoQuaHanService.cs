using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

/// <summary>
/// Dọn các phiếu đặt phòng CHO_XAC_NHAN (chưa nhận được tiền cọc) mà ngày nhận phòng đã qua: không còn ý nghĩa giữ chỗ và không thể đặt cọc nữa
/// (xác nhận cọc / tạo mã QR đều từ chối phiếu quá ngày nhận), nên tự chuyển sang DA_HUY với lý do ghi rõ. Phiếu chưa có tiền nên không có gì để hoàn hay giữ lại.
/// Tắt bằng <c>Booking:TuHuyPhieuChoQuaHan = false</c>.
/// </summary>
public class PhieuChoQuaHanService : BackgroundService
{
    public const string HuyBoiHeThong = "HE_THONG";
    public const string LyDo = "Tự động hủy: quá ngày nhận phòng mà chưa đặt cọc";

    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly ILogger<PhieuChoQuaHanService> _logger;

    public PhieuChoQuaHanService(IServiceScopeFactory scopes, IConfiguration config, ILogger<PhieuChoQuaHanService> logger)
    {
        _scopes = scopes;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);   // để API khởi động xong (migrate + seed) trước
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_config.GetValue("Booking:TuHuyPhieuChoQuaHan", true))
                {
                    try
                    {
                        await using var scope = _scopes.CreateAsyncScope();
                        var soPhieu = await DonAsync(scope.ServiceProvider.GetRequiredService<KLCN060DbContext>(), DateOnly.FromDateTime(DateTime.Now), ct: stoppingToken);
                        if (soPhieu > 0) _logger.LogInformation("Đã tự hủy {So} phiếu đặt phòng chưa đặt cọc và quá ngày nhận phòng.", soPhieu);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "Dọn phiếu chờ xác nhận quá hạn gặp lỗi");
                    }
                }
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // dừng ứng dụng
        }
    }

    /// <summary>Hủy các phiếu CHO_XAC_NHAN có ngày nhận trước <paramref name="homNay"/> (hoặc chỉ phiếu <paramref name="chiMaPhieu"/>, dùng khi kiểm thử); trả về số phiếu đã hủy.</summary>
    public static async Task<int> DonAsync(KLCN060DbContext context, DateOnly homNay, string? chiMaPhieu = null, CancellationToken ct = default)
    {
        var phieus = await context.PhieuDatPhongs
            .Include(x => x.ChiTietKhuyenMais)
            .Include(x => x.Khach).ThenInclude(k => k.TaiKhoan)
            .Where(x => x.TrangThai == TrangThaiPhieuDat.CHO_XAC_NHAN && x.NgayDonDuKien < homNay && x.PhieuNhanPhong == null
                        && (chiMaPhieu == null || x.MaPhieuDat == chiMaPhieu))
            .ToListAsync(ct);
        if (phieus.Count == 0) return 0;

        var bayGio = DateTime.Now;
        foreach (var p in phieus)
        {
            p.TrangThai = TrangThaiPhieuDat.DA_HUY;
            p.NgayHuy = bayGio;
            p.LyDoHuy = LyDo;
            p.HuyBoi = HuyBoiHeThong;
            p.SoTienHoanCoc = 0m;
            p.TienCocGiuLai = 0m;

            // Trả lại lượt khuyến mãi phiếu đã chiếm.
            foreach (var km in p.ChiTietKhuyenMais)
            {
                var maKM = km.MaKM;
                await context.KhuyenMais.Where(x => x.MaKM == maKM && x.SoLuongDaSuDung > 0)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.SoLuongDaSuDung, x => x.SoLuongDaSuDung - 1), ct);
            }

            var maPhieu = p.MaPhieuDat;
            await context.GiaoDichChuyenKhoans
                .Where(x => x.LoaiThamChieu == LoaiThamChieuThanhToan.DAT_COC && x.MaThamChieu == maPhieu && x.TrangThai == TrangThaiGiaoDichChuyenKhoan.CHO_THANH_TOAN)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.TrangThai, TrangThaiGiaoDichChuyenKhoan.DA_HUY), ct);

            // Nhật ký gắn vào tài khoản của khách (khóa ngoại tới TaiKhoan); khách không có tài khoản thì lý do vẫn nằm trên chính phiếu.
            if (p.Khach.TaiKhoan is { } tk)
                context.NhatKyThaoTacs.Add(new NhatKyThaoTac
                {
                    MaTaiKhoan = tk.TenDN,
                    HanhDong = "TU_HUY_PHIEU_QUA_HAN",
                    DoiTuongTacDong = p.MaPhieuDat,
                    ThoiGian = bayGio,
                    ChiTiet = $"ngayDonDuKien={p.NgayDonDuKien:yyyy-MM-dd}; trangThai=CHO_XAC_NHAN->DA_HUY; lyDo={LyDo}"
                });
        }
        await context.SaveChangesAsync(ct);
        return phieus.Count;
    }
}
