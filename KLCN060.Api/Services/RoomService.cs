using KLCN060.Api.Auditing;
using KLCN060.Api.DTOs.Rooms;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KLCN060.Api.Services;

public partial class RoomService : IRoomService
{
    private readonly KLCN060DbContext _context;
    private readonly IAuditTrail _audit;
    private readonly IPermissionService _permissions;
    private readonly AlertOptions _alert;

    public RoomService(KLCN060DbContext context, IAuditTrail audit, IPermissionService permissions, IOptions<AlertOptions>? alert = null)
    {
        _context = context;
        _audit = audit;
        _permissions = permissions;
        _alert = alert?.Value ?? new AlertOptions();
    }

    public async Task<List<RoomDto>> GetAllAsync()
    {
        // Kèm thời điểm + lý do của lần đổi tình trạng gần nhất để sơ đồ phòng cho biết "phòng này bảo trì/chưa dọn từ bao giờ, vì sao".
        var dong = await _context.Phongs.AsNoTracking()
            .Include(x => x.LoaiPhong)
            .OrderBy(x => x.MaPhong)
            .Select(x => new
            {
                Phong = x,
                Cuoi = _context.LichSuTinhTrangPhongs.Where(h => h.MaPhong == x.MaPhong && h.DenTinhTrang == x.TinhTrang)
                    .OrderByDescending(h => h.ThoiGian).ThenByDescending(h => h.MaLichSu)
                    .Select(h => new { h.ThoiGian, h.LyDo }).FirstOrDefault()
            })
            .ToListAsync();

        var ketQua = dong.Select(x =>
        {
            var dto = ToDto(x.Phong);
            dto.TinhTrangTuLuc = x.Cuoi?.ThoiGian;
            dto.LyDoTinhTrang = x.Cuoi?.LyDo;
            return dto;
        }).ToList();
        await BoSungThongTinSoDoAsync(ketQua);
        return ketQua;
    }

    public async Task<List<RoomStatusHistoryDto>> GetHistoryAsync(string maPhong)
    {
        await TimPhongAsync(maPhong);
        var dong = await _context.LichSuTinhTrangPhongs.AsNoTracking().Where(x => x.MaPhong == maPhong)
            .OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.MaLichSu).Take(500).ToListAsync();

        var tenDNs = dong.Where(x => x.MaTaiKhoan != null).Select(x => x.MaTaiKhoan!).Distinct().ToList();
        var hoTen = await _context.TaiKhoans.AsNoTracking().Where(x => tenDNs.Contains(x.TenDN))
            .Select(x => new { x.TenDN, HoTen = x.NhanVien != null ? x.NhanVien.HoTen : null }).ToDictionaryAsync(x => x.TenDN, x => x.HoTen);

        return dong.Select(x => new RoomStatusHistoryDto
        {
            MaLichSu = x.MaLichSu,
            MaPhong = x.MaPhong,
            TuTinhTrang = x.TuTinhTrang.ToString(),
            DenTinhTrang = x.DenTinhTrang.ToString(),
            ThoiGian = x.ThoiGian,
            MaTaiKhoan = x.MaTaiKhoan,
            HoTenNguoiThucHien = x.MaTaiKhoan is null ? null : hoTen.GetValueOrDefault(x.MaTaiKhoan),
            LyDo = x.LyDo
        }).ToList();
    }

    public async Task<RoomDto> CreateAsync(RoomRequest request)
    {
        request.TenPhong = ChuanHoaTenPhong(request.TenPhong);
        KiemTraTang(request.Tang);
        await KiemTraLoaiPhongTonTaiAsync(request.MaLoai);

        var maPhong = $"P{request.TenPhong}";
        if (await _context.Phongs.AnyAsync(x => x.MaPhong == maPhong || x.TenPhong == request.TenPhong))
            throw new ApiException(StatusCodes.Status409Conflict, "PHONG_DA_TON_TAI", "Phòng với tên này đã tồn tại.", nameof(request.TenPhong));

        var tinhTrang = ParseTinhTrang(request.TinhTrang ?? nameof(TinhTrangPhong.VC));
        // Phòng mới chưa có khách nên không thể là OC/OD: hai trạng thái đó chỉ sinh ra từ nhận phòng.
        if (tinhTrang is TinhTrangPhong.OC or TinhTrangPhong.OD)
            throw new ApiException(StatusCodes.Status422UnprocessableEntity, "INVALID_STATUS_TRANSITION",
                "Phòng mới tạo chưa có khách nên chỉ được ở trạng thái VC, VD, VI hoặc OOO.", "tinhTrang");

        if (tinhTrang == TinhTrangPhong.OOO)
        {
            KiemTraLyDoBaoTri(tinhTrang, request.GhiChu);
            _audit.LyDoTinhTrangPhong = request.GhiChu?.Trim();
        }

        var phong = new Phong
        {
            MaPhong = maPhong,
            TenPhong = request.TenPhong,
            Tang = request.Tang,
            MaLoai = request.MaLoai,
            TinhTrang = tinhTrang,
            NgayTao = DateTime.Now
        };

        _context.Phongs.Add(phong);
        _audit.Ghi("TAO_PHONG", maPhong, $"ten={phong.TenPhong}; tang={phong.Tang}; loai={phong.MaLoai}; tinhTrang={tinhTrang}");
        await _context.SaveChangesAsync();

        return await LayTheoMaAsync(maPhong);
    }

    public async Task<RoomDto> UpdateAsync(string maPhong, RoomRequest request, string? tenDN = null)
    {
        var phong = await TimPhongAsync(maPhong);
        request.TenPhong = ChuanHoaTenPhong(request.TenPhong);
        KiemTraTang(request.Tang);
        await KiemTraLoaiPhongTonTaiAsync(request.MaLoai);
        if (request.TenPhong != phong.TenPhong
            && await _context.Phongs.AnyAsync(x => x.MaPhong != maPhong && (x.TenPhong == request.TenPhong || x.MaPhong == $"P{request.TenPhong}")))
            throw new ApiException(StatusCodes.Status409Conflict, "PHONG_DA_TON_TAI", "Đã có phòng khác mang tên này.", nameof(request.TenPhong));
        if (request.MaLoai != phong.MaLoai)
        {
            var (duoc, lyDo) = CoTheDoiLoaiPhong(phong, (await LayPhatSinhAsync(new List<string> { maPhong }))[maPhong]);
            if (!duoc)
                throw new ApiException(StatusCodes.Status409Conflict, "PHONG_KHONG_DOI_LOAI_DUOC", lyDo!, "maLoai");
        }
        var thayDoi = new List<string>();
        if (request.TenPhong != phong.TenPhong) thayDoi.Add($"ten: {phong.TenPhong} -> {request.TenPhong}");
        if (request.Tang != phong.Tang) thayDoi.Add($"tang: {phong.Tang} -> {request.Tang}");
        if (request.MaLoai != phong.MaLoai) thayDoi.Add($"loai: {phong.MaLoai} -> {request.MaLoai}");
        if (thayDoi.Count > 0) _audit.Ghi("SUA_PHONG", maPhong, string.Join("; ", thayDoi));

        // Tình trạng chỉ đổi khi KHÁC hiện tại, và khi đổi phải theo đúng sơ đồ chuyển trạng thái + kiểm tra khách đang ở như
        // PATCH /rooms/{id}/status. Trước đây PUT gán thẳng: quản lý sửa tên phòng bằng dữ liệu cũ có thể "trả" phòng đang có khách
        // về VC, hoặc đặt tay OC cho phòng không có khách.
        TinhTrangPhong? tinhTrangMoi = null;
        if (!string.IsNullOrEmpty(request.TinhTrang))
        {
            var yeuCau = ParseTinhTrang(request.TinhTrang);
            if (yeuCau != phong.TinhTrang)
                tinhTrangMoi = yeuCau;
        }
        if (tinhTrangMoi is not null)
        {
            await KiemTraChuyenTrangThaiAsync(phong, tinhTrangMoi.Value);
            await KiemTraQuyenXacNhanDonAsync(phong.TinhTrang, tinhTrangMoi.Value, tenDN);
            await KiemTraQuyenKhoaMoAsync(phong.TinhTrang, tinhTrangMoi.Value, tenDN);
            KiemTraLyDoBaoTri(tinhTrangMoi.Value, request.GhiChu);
            KiemTraLyDoDonLai(phong.TinhTrang, tinhTrangMoi.Value, request.GhiChu);
            _audit.LyDoTinhTrangPhong = request.GhiChu?.Trim();
            _audit.Ghi("DOI_TINH_TRANG_PHONG", maPhong, ChiTietDoiTinhTrang(phong.TinhTrang, tinhTrangMoi.Value, request.GhiChu));
            await CapNhatViecDonAsync(maPhong, phong.TinhTrang, tinhTrangMoi.Value, tenDN);
        }

        phong.TenPhong = request.TenPhong;
        phong.Tang = request.Tang;
        phong.MaLoai = request.MaLoai;
        if (tinhTrangMoi is not null)
            phong.TinhTrang = tinhTrangMoi.Value;

        await _context.SaveChangesAsync();
        return await LayTheoMaAsync(maPhong);
    }

    public async Task DeleteAsync(string maPhong)
    {
        var phong = await TimPhongAsync(maPhong);
        // Phòng đã phát sinh dữ liệu (đặt phòng, lưu trú, thiết bị, dọn phòng, sự cố...) thì KHÔNG xóa: xóa sẽ mất lịch sử. Chuyển sang bảo trì (OOO) để ngừng bán.
        var (duocXoa, lyDoKhongXoa) = CoTheXoaPhong((await LayPhatSinhAsync(new List<string> { maPhong }))[maPhong]);
        if (!duocXoa)
            throw new ApiException(StatusCodes.Status409Conflict, "PHONG_DANG_DUOC_SU_DUNG", lyDoKhongXoa!);
        _audit.Ghi("XOA_PHONG", maPhong, $"ten={phong.TenPhong}; tang={phong.Tang}; loai={phong.MaLoai}; tinhTrang={phong.TinhTrang}");
        _context.Phongs.Remove(phong);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "PHONG_DANG_DUOC_SU_DUNG", "Không thể xóa phòng vì đang có dữ liệu liên quan (đặt phòng, cơ sở vật chất...).");
        }
    }

    // Sơ đồ chuyển trạng thái buồng phòng hợp lệ. Cố ý KHÔNG có OC -> VC/VI: phòng đang có khách phải qua OD
    // (cần dọn) rồi mới về trống; phòng chỉ thành OC qua check-in (không đặt tay), trả phòng (OC/OD -> VD) do luồng check-out xử lý, không đi qua endpoint này.
    private static readonly Dictionary<TinhTrangPhong, TinhTrangPhong[]> ChuyenTrangThaiHopLe = new()
    {
        [TinhTrangPhong.VD] = new[] { TinhTrangPhong.VC, TinhTrangPhong.OOO },
        [TinhTrangPhong.VC] = new[] { TinhTrangPhong.VI, TinhTrangPhong.VD, TinhTrangPhong.OOO },
        [TinhTrangPhong.VI] = new[] { TinhTrangPhong.VD, TinhTrangPhong.OOO },
        [TinhTrangPhong.OC] = new[] { TinhTrangPhong.OD },
        [TinhTrangPhong.OD] = new[] { TinhTrangPhong.OC, TinhTrangPhong.VD },
        [TinhTrangPhong.OOO] = new[] { TinhTrangPhong.VD, TinhTrangPhong.VC }
    };

    public async Task<RoomDto> UpdateStatusAsync(string maPhong, string tinhTrang, string? ghiChu, string tenDN, string? tinhTrangCu = null)
    {
        // Hai người đổi tình trạng CÙNG MỘT PHÒNG cùng lúc: khóa dòng phòng (SQL Server) để thao tác sau đọc lại tình trạng mới nhất rồi mới kiểm tra
        // "tình trạng đang thấy" và sơ đồ chuyển hợp lệ - không để người sau ghi đè người trước mà lịch sử vẫn ghi hai lần chuyển từ cùng một trạng thái.
        await using var transaction = _context.Database.IsSqlServer() ? await _context.Database.BeginTransactionAsync() : null;
        var phong = transaction is null
            ? await TimPhongAsync(maPhong)
            : (await _context.Phongs.FromSql($"SELECT * FROM Phongs WITH (UPDLOCK, HOLDLOCK) WHERE MaPhong = {maPhong}").ToListAsync()).FirstOrDefault()
              ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_PHONG", "Không tìm thấy phòng.");
        var moi = ParseTinhTrang(tinhTrang);
        var cu = phong.TinhTrang;

        // Không ghi đè thay đổi của người khác: màn hình của người thao tác đang hiển thị tình trạng cũ, nhưng phòng đã được người khác đổi.
        if (!string.IsNullOrWhiteSpace(tinhTrangCu) && ParseTinhTrang(tinhTrangCu) != cu)
            throw new ApiException(StatusCodes.Status409Conflict, "XUNG_DOT_TINH_TRANG",
                $"Phòng {maPhong} vừa được người khác đổi sang {cu} (bạn đang thấy {tinhTrangCu.Trim().ToUpperInvariant()}). Hãy tải lại sơ đồ phòng rồi thao tác lại.", "tinhTrang");

        await KiemTraChuyenTrangThaiAsync(phong, moi);
        await KiemTraQuyenXacNhanDonAsync(cu, moi, tenDN);
        await KiemTraQuyenKhoaMoAsync(cu, moi, tenDN);
        KiemTraLyDoBaoTri(moi, ghiChu);
        KiemTraLyDoDonLai(cu, moi, ghiChu);

        phong.TinhTrang = moi;
        await CapNhatViecDonAsync(maPhong, cu, moi, tenDN);

        // Lịch sử tình trạng phòng (kèm lý do) do bộ chặn SaveChanges ghi; nhật ký thao tác ghi cùng transaction.
        _audit.LyDoTinhTrangPhong = ghiChu?.Trim();
        var anhHuong = moi == TinhTrangPhong.OOO ? await DatPhongBiAnhHuongAsync(maPhong) : "";
        _audit.Ghi("DOI_TINH_TRANG_PHONG", maPhong, ChiTietDoiTinhTrang(cu, moi, ghiChu) + anhHuong);

        await _context.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();
        return await LayTheoMaAsync(maPhong);
    }

    /// <summary>
    /// Xác nhận "đã dọn / đã kiểm tra" (→ VC, → VI, OD → OC) là việc của buồng phòng/quản lý (quyền XAC_NHAN_DON_PHONG). Lễ tân KHÔNG đổi tình trạng phòng
    /// (không có DOI_TINH_TRANG_PHONG): trả phòng tự đặt phòng thành VD, còn dọn / kiểm tra / khóa phòng là nghiệp vụ của buồng phòng và quản lý.
    /// </summary>
    private async Task KiemTraQuyenXacNhanDonAsync(TinhTrangPhong cu, TinhTrangPhong moi, string? tenDN)
    {
        var laXacNhanDon = moi is TinhTrangPhong.VC or TinhTrangPhong.VI || (cu == TinhTrangPhong.OD && moi == TinhTrangPhong.OC);
        if (!laXacNhanDon) return;
        if (string.IsNullOrEmpty(tenDN) || !await _permissions.CoQuyenAsync(tenDN, Permissions.XacNhanDonPhong))
            throw new ApiException(StatusCodes.Status403Forbidden, "CAN_BUONG_PHONG_XAC_NHAN",
                $"Chuyển phòng từ {cu} sang {moi} là xác nhận đã dọn/kiểm tra phòng, chỉ buồng phòng và quản lý (quyền XAC_NHAN_DON_PHONG) mới thực hiện được.", "tinhTrang");

        // Quản lý kiểm tra chất lượng (VC → VI) chứ không tự dọn: phòng đang bẩn (VD/OD) phải do buồng phòng xác nhận đã dọn xong.
        var dangBan = cu is TinhTrangPhong.VD or TinhTrangPhong.OD;
        if (dangBan && await _permissions.CoQuyenAsync(tenDN, Permissions.DuyetNghiepVu))
            throw new ApiException(StatusCodes.Status403Forbidden, "QUAN_LY_KHONG_TU_DON_PHONG",
                $"Quản lý không tự xác nhận phòng {cu} đã dọn xong: hãy giao việc cho nhân viên buồng phòng, sau khi họ dọn xong thì kiểm tra (VC → VI).", "tinhTrang");
    }

    /// <summary>
    /// Đưa phòng vào bảo trì / khóa (→ OOO) và mở lại phòng đang bảo trì (OOO →) là quyết định vận hành, không phải việc dọn phòng:
    /// cần quyền KHOA_MO_PHONG (chỉ quản lý). Buồng phòng phát hiện hỏng hóc thì báo sự cố để quản lý quyết định, không tự khóa/mở phòng.
    /// </summary>
    private async Task KiemTraQuyenKhoaMoAsync(TinhTrangPhong cu, TinhTrangPhong moi, string? tenDN)
    {
        if (cu != TinhTrangPhong.OOO && moi != TinhTrangPhong.OOO) return;
        if (string.IsNullOrEmpty(tenDN) || !await _permissions.CoQuyenAsync(tenDN, Permissions.KhoaMoPhong))
            throw new ApiException(StatusCodes.Status403Forbidden, "CAN_QUYEN_KHOA_MO_PHONG",
                cu == TinhTrangPhong.OOO
                    ? "Mở lại phòng đang bảo trì cần quyền KHOA_MO_PHONG (chỉ quản lý). Buồng phòng không tự mở phòng đang bảo trì."
                    : "Đưa phòng vào bảo trì / khóa phòng cần quyền KHOA_MO_PHONG (chỉ quản lý). Buồng phòng hãy báo sự cố để quản lý quyết định.", "tinhTrang");
    }

    /// <summary>
    /// Việc dọn phòng: khi phòng rời trạng thái bẩn (VD/OD) thì đóng phiếu dọn đang mở (đã xong nếu phòng thành sạch, hủy nếu phòng bị khóa bảo trì);
    /// nếu chưa ai "nhận dọn" mà phòng được xác nhận sạch thì ghi một phiếu đã xong (không có giờ bắt đầu) để biết ai đã dọn.
    /// </summary>
    private async Task CapNhatViecDonAsync(string maPhong, TinhTrangPhong cu, TinhTrangPhong moi, string? tenDN)
    {
        static bool Ban(TinhTrangPhong t) => t is TinhTrangPhong.VD or TinhTrangPhong.OD;
        if (!Ban(cu) || Ban(moi)) return;

        var xong = moi is TinhTrangPhong.VC or TinhTrangPhong.VI or TinhTrangPhong.OC;
        var bayGio = DateTime.Now;
        var dangDon = await _context.PhieuDonPhongs.Where(x => x.MaPhong == maPhong && x.TrangThai == TrangThaiPhieuDon.DANG_DON).ToListAsync();
        var huyViecDuocGiao = false;
        foreach (var phieu in dangDon)
        {
            // Việc được giao cho người A nhưng A chưa bắt đầu, người B xác nhận phòng sạch: ghi công cho B, việc của A coi như hủy.
            if (xong && phieu.ThoiGianBatDau is null && !string.IsNullOrEmpty(tenDN) && phieu.NguoiDon != tenDN)
            {
                phieu.ThoiGianXong = bayGio;
                phieu.TrangThai = TrangThaiPhieuDon.DA_HUY;
                huyViecDuocGiao = true;
                continue;
            }
            phieu.ThoiGianXong = bayGio;
            phieu.TrangThai = xong ? TrangThaiPhieuDon.DA_XONG : TrangThaiPhieuDon.DA_HUY;
        }
        if ((dangDon.Count == 0 || huyViecDuocGiao) && xong && !string.IsNullOrEmpty(tenDN))
        {
            _context.PhieuDonPhongs.Add(new PhieuDonPhong
            {
                MaPhong = maPhong, NguoiDon = tenDN, TinhTrangBanDau = cu, ThoiGianXong = bayGio, TrangThai = TrangThaiPhieuDon.DA_XONG
            });
        }
    }

    /// <summary>Đưa phòng vào bảo trì / khóa phòng (OOO) là thay đổi ảnh hưởng vận hành: bắt buộc ghi lý do để truy vết.</summary>
    private static void KiemTraLyDoBaoTri(TinhTrangPhong moi, string? ghiChu)
    {
        if (moi == TinhTrangPhong.OOO && string.IsNullOrWhiteSpace(ghiChu))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_LY_DO_BAO_TRI", "Cần nhập lý do khi đưa phòng vào bảo trì / khóa phòng (OOO).", "ghiChu");
    }

    /// <summary>Đưa phòng đã dọn / đã kiểm tra (VC, VI) trở lại bẩn (VD) là việc làm lại, ảnh hưởng buồng phòng và khách sắp đến: bắt buộc ghi lý do (Desktop cũng bắt, API phải bắt để không qua mặt được).</summary>
    private static void KiemTraLyDoDonLai(TinhTrangPhong cu, TinhTrangPhong moi, string? ghiChu)
    {
        if (moi == TinhTrangPhong.VD && cu is TinhTrangPhong.VC or TinhTrangPhong.VI && string.IsNullOrWhiteSpace(ghiChu))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_LY_DO_DON_LAI", "Cần nhập lý do khi đưa phòng đã dọn / đã kiểm tra trở lại bẩn (VD: còn tóc trong phòng tắm).", "ghiChu");
    }

    private static string ChiTietDoiTinhTrang(TinhTrangPhong cu, TinhTrangPhong moi, string? ghiChu)
        => string.IsNullOrWhiteSpace(ghiChu) ? $"{cu} -> {moi}" : $"{cu} -> {moi}; {ghiChu.Trim()}";

    /// <summary>Luật chung cho mọi đường đổi tình trạng phòng bằng tay (PATCH status của buồng phòng/quản lý và PUT của quản lý).</summary>
    private async Task KiemTraChuyenTrangThaiAsync(Phong phong, TinhTrangPhong moi)
    {
        var cu = phong.TinhTrang;
        var maPhong = phong.MaPhong;

        if (!ChuyenTrangThaiHopLe[cu].Contains(moi))
            throw new ApiException(StatusCodes.Status422UnprocessableEntity, "INVALID_STATUS_TRANSITION",
                $"Không thể chuyển phòng từ {cu} sang {moi}. Các trạng thái được phép từ {cu}: {string.Join(", ", ChuyenTrangThaiHopLe[cu])}.", "tinhTrang");

        // [Khối 8 - K8-4] Bảng chuyển trạng thái cho phép OD → VD (đúng lúc trả phòng), nhưng không biết phòng còn khách hay
        // không: buồng phòng bấm tay khi khách CÒN Ở sẽ khiến sơ đồ phòng báo "trống" trong khi lượt lưu trú vẫn DANG_O.
        // Chuyển sang trạng thái TRỐNG khi còn khách chỉ được đi qua nghiệp vụ trả phòng (check-out tự đặt VD).
        var conKhach = await _context.ChiTietPhieuNhans.AnyAsync(x => x.MaPhong == maPhong && x.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O);
        if (moi is TinhTrangPhong.VC or TinhTrangPhong.VD or TinhTrangPhong.VI or TinhTrangPhong.OOO && conKhach)
            throw new ApiException(StatusCodes.Status409Conflict, "PHONG_DANG_CO_KHACH",
                $"Phòng {maPhong} đang có khách lưu trú, không thể chuyển sang {moi}. Hãy làm thủ tục trả phòng trước.", "tinhTrang");
        // Chiều ngược lại: không được đánh dấu "có khách" cho phòng không có lượt lưu trú nào đang ở.
        if (moi is TinhTrangPhong.OC && !conKhach)
            throw new ApiException(StatusCodes.Status409Conflict, "PHONG_KHONG_CO_KHACH",
                $"Phòng {maPhong} không có khách đang lưu trú nên không thể đặt là OC. Hãy làm thủ tục nhận phòng.", "tinhTrang");
    }

    private async Task<Phong> TimPhongAsync(string maPhong)
    {
        var phong = await _context.Phongs.FirstOrDefaultAsync(x => x.MaPhong == maPhong);
        if (phong is null)
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_PHONG", "Không tìm thấy phòng.");
        return phong;
    }

    private async Task<RoomDto> LayTheoMaAsync(string maPhong)
    {
        var phong = await _context.Phongs.Include(x => x.LoaiPhong).FirstAsync(x => x.MaPhong == maPhong);
        return ToDto(phong);
    }

    private async Task KiemTraLoaiPhongTonTaiAsync(string maLoai)
    {
        if (!await _context.LoaiPhongs.AnyAsync(x => x.MaLoai == maLoai))
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LOAI_PHONG", "Không tìm thấy loại phòng.", nameof(maLoai));
    }

    private static TinhTrangPhong ParseTinhTrang(string tinhTrang)
    {
        if (!EnumParser.TryParse<TinhTrangPhong>(tinhTrang, out var ketQua))
            throw new ApiException(StatusCodes.Status400BadRequest, "TINH_TRANG_KHONG_HOP_LE", "Tình trạng phòng không hợp lệ. Chỉ chấp nhận: VC, VD, VI, OC, OD, OOO.", "tinhTrang");
        return ketQua;
    }

    private static RoomDto ToDto(Phong x) => new()
    {
        MaPhong = x.MaPhong,
        TenPhong = x.TenPhong,
        Tang = x.Tang,
        TinhTrang = x.TinhTrang.ToString(),
        MaLoai = x.MaLoai,
        TenLoaiPhong = x.LoaiPhong.TenLoai
    };
}
