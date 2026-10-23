using KLCN060.Api.Auditing;
using KLCN060.Api.DTOs.Facilities;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

/// <summary>
/// Cơ sở vật chất / thiết bị: vòng đời Thêm → Tốt → Hỏng chờ sửa → Đang sửa → (Đã thay mới) → Ngừng sử dụng → Đã thanh lý, cộng Điều chuyển giữa phòng/kho.
/// Mọi thay đổi ghi nhật ký (ai, lúc nào, trước → sau, lý do); thiết bị đã có lịch sử không xóa được - chỉ ngừng dùng/thanh lý.
/// </summary>
public static class TinhTrangCsvc
{
    public const string Tot = "TOT";
    public const string HongChoSua = "HONG_CHO_SUA";
    public const string HongDangSua = "HONG_DANG_SUA";
    public const string DaThayMoi = "DA_THAY_MOI";
    public const string NgungSuDung = "NGUNG_SU_DUNG";
    public const string DaThanhLy = "DA_THANH_LY";

    public static readonly string[] TatCa = { Tot, HongChoSua, HongDangSua, DaThayMoi, NgungSuDung, DaThanhLy };

    public static bool LaHong(string t) => t is HongChoSua or HongDangSua;
    /// <summary>Đang dùng được trong phòng / kho (tính vào "số thiết bị đang dùng").</summary>
    public static bool DangDung(string t) => t is Tot or DaThayMoi;
    public static bool KhongConDung(string t) => t is NgungSuDung or DaThanhLy;

    public static string HienThi(string t) => t switch
    {
        Tot => "Tốt", HongChoSua => "Hỏng, chờ sửa", HongDangSua => "Hỏng, đang sửa", DaThayMoi => "Đã thay mới",
        NgungSuDung => "Ngừng sử dụng", DaThanhLy => "Đã thanh lý", _ => t
    };
}

public class FacilityService : IFacilityService
{
    /// <summary>Thiết bị hỏng quá số ngày này mà chưa xử lý xong thì đưa vào danh sách cần chú ý.</summary>
    public const int HongQuaHanNgay = 7;

    private static readonly string[] HanhDongCsvc =
    {
        "TAO_CO_SO_VAT_CHAT", "SUA_CO_SO_VAT_CHAT", "DOI_TINH_TRANG_CSVC", "DIEU_CHUYEN_CSVC", "XOA_CO_SO_VAT_CHAT"
    };

    private readonly KLCN060DbContext _context;
    private readonly IAuditTrail _audit;

    public FacilityService(KLCN060DbContext context, IAuditTrail audit)
    {
        _context = context;
        _audit = audit;
    }

    public async Task<List<FacilityDto>> GetByRoomAsync(string maPhong)
    {
        if (!await _context.Phongs.AnyAsync(x => x.MaPhong == maPhong))
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_PHONG", "Không tìm thấy phòng.");

        return await _context.CoSoVatChats
            .Where(x => x.MaPhong == maPhong)
            .OrderBy(x => x.MaSo)
            .Select(x => ToDto(x))
            .ToListAsync();
    }

    // ------------------------------------------------------------------------------------------ thêm / sửa / xóa

    public Task<FacilityDto> CreateForRoomAsync(string maPhong, FacilityRequest request) => TaoAsync(maPhong, request);

    public Task<FacilityDto> CreateAsync(FacilityRequest request) => TaoAsync(string.IsNullOrWhiteSpace(request.MaPhong) ? null : request.MaPhong.Trim(), request);

    private async Task<FacilityDto> TaoAsync(string? maPhong, FacilityRequest request)
    {
        var (ten, tinhTrang, ghiChu, lyDo) = ChuanHoa(request, null);
        if (maPhong is not null && !await _context.Phongs.AnyAsync(x => x.MaPhong == maPhong))
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_PHONG", "Không tìm thấy phòng.");
        if (tinhTrang == TinhTrangCsvc.DaThanhLy)
            throw new ApiException(StatusCodes.Status400BadRequest, "TINH_TRANG_KHONG_HOP_LE", "Không thêm mới thiết bị ở tình trạng \"Đã thanh lý\".", "tinhTrang");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        // Khóa theo tiền tố mã giữ tới hết transaction: kiểm tra trùng + sinh mã nằm TRONG khóa nên 2 yêu cầu đồng thời
        // cùng tên cùng vị trí không cùng lọt qua bước kiểm tra (trước đây 8 yêu cầu tạo ra 8 dòng trùng).
        var maSo = await MaMoiAsync();
        await KiemTraTrungAsync(ten, maPhong, tinhTrang, null);
        var bayGio = DateTime.Now;

        var coSoVatChat = new CoSoVatChat
        {
            MaSo = maSo, Ten = ten, SoLuong = request.SoLuong, TinhTrang = tinhTrang, MaPhong = maPhong, GhiChu = ghiChu,
            TinhTrangTuLuc = bayGio, LyDoTinhTrang = lyDo, NgayTao = bayGio, CapNhatLuc = bayGio, NguoiCapNhat = _audit.TenDN
        };
        _context.CoSoVatChats.Add(coSoVatChat);
        _audit.Ghi("TAO_CO_SO_VAT_CHAT", maSo, $"ten={ten}; soLuong={request.SoLuong}; tinhTrang={tinhTrang}; viTri={ViTri(maPhong)}");

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "XUNG_DOT_DU_LIEU", "Có xung đột dữ liệu khi tạo cơ sở vật chất, vui lòng thử lại.");
        }

        await transaction.CommitAsync();
        return ToDto(coSoVatChat);
    }

    public async Task<FacilityDto> UpdateAsync(string maSo, FacilityRequest request)
    {
        var x = await TimCoSoVatChatAsync(maSo);
        if (x.TinhTrang == TinhTrangCsvc.DaThanhLy)
            throw new ApiException(StatusCodes.Status409Conflict, "THIET_BI_DA_THANH_LY", "Thiết bị đã thanh lý, không sửa được nữa.");
        var (ten, tinhTrang, ghiChu, lyDo) = ChuanHoa(request, x.TinhTrang);
        await KiemTraTrungAsync(ten, x.MaPhong, tinhTrang, maSo);

        var bayGio = DateTime.Now;
        var doi = new List<string>();
        if (x.Ten != ten) doi.Add($"ten: {x.Ten} -> {ten}");
        if (x.SoLuong != request.SoLuong) doi.Add($"soLuong: {x.SoLuong} -> {request.SoLuong}");
        if ((x.GhiChu ?? "") != (ghiChu ?? "")) doi.Add("ghiChuDoi=true");

        var doiTinhTrang = x.TinhTrang != tinhTrang;
        if (doiTinhTrang)
        {
            _audit.Ghi("DOI_TINH_TRANG_CSVC", maSo, $"{TinhTrangCsvc.HienThi(x.TinhTrang)} -> {TinhTrangCsvc.HienThi(tinhTrang)}; ten={ten}; phong={x.MaPhong ?? "kho"}" + (lyDo is null ? "" : $"; lyDo={lyDo}"));
            x.TinhTrang = tinhTrang; x.TinhTrangTuLuc = bayGio; x.LyDoTinhTrang = lyDo;
        }
        if (doi.Count > 0) _audit.Ghi("SUA_CO_SO_VAT_CHAT", maSo, string.Join("; ", doi) + $"; phong={x.MaPhong ?? "kho"}");

        x.Ten = ten; x.SoLuong = request.SoLuong; x.GhiChu = ghiChu;
        if (doi.Count > 0 || doiTinhTrang) { x.CapNhatLuc = bayGio; x.NguoiCapNhat = _audit.TenDN; }

        await _context.SaveChangesAsync();
        return ToDto(x);
    }

    public async Task DeleteAsync(string maSo, string? lyDo = null)
    {
        var x = await TimCoSoVatChatAsync(maSo);
        var khongXoa = await LyDoKhongXoaAsync(maSo, x);
        if (khongXoa is not null)
            throw new ApiException(StatusCodes.Status409Conflict, "CO_SO_VAT_CHAT_DANG_DUOC_SU_DUNG", khongXoa);

        _context.CoSoVatChats.Remove(x);
        _audit.Ghi("XOA_CO_SO_VAT_CHAT", maSo, $"ten={x.Ten}; soLuong={x.SoLuong}; tinhTrang={x.TinhTrang}; viTri={ViTri(x.MaPhong)}" + (string.IsNullOrWhiteSpace(lyDo) ? "" : $"; lyDo={lyDo.Trim()}"));

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "CO_SO_VAT_CHAT_DANG_DUOC_SU_DUNG", "Không thể xóa cơ sở vật chất vì đang có dữ liệu liên quan.");
        }
    }

    // ------------------------------------------------------------------------------------------ điều chuyển

    public async Task<FacilityDto> TransferAsync(string maSo, FacilityTransferRequest request)
    {
        var x = await TimCoSoVatChatAsync(maSo);
        if (x.TinhTrang == TinhTrangCsvc.DaThanhLy)
            throw new ApiException(StatusCodes.Status409Conflict, "THIET_BI_DA_THANH_LY", "Thiết bị đã thanh lý, không điều chuyển được.");
        var lyDo = request.LyDo?.Trim() ?? "";
        if (lyDo.Length == 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_LY_DO", "Nhập lý do điều chuyển.", "lyDo");
        var dich = string.IsNullOrWhiteSpace(request.MaPhongMoi) ? null : request.MaPhongMoi.Trim();
        if (dich == x.MaPhong)
            throw new ApiException(StatusCodes.Status400BadRequest, "VI_TRI_KHONG_DOI", "Thiết bị đã ở vị trí này rồi.", "maPhongMoi");
        if (dich is not null && !await _context.Phongs.AnyAsync(x => x.MaPhong == dich))
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_PHONG", "Không tìm thấy phòng đích.", "maPhongMoi");

        var soLuong = request.SoLuong ?? x.SoLuong;
        if (soLuong < 1 || soLuong > x.SoLuong)
            throw new ApiException(StatusCodes.Status400BadRequest, "SO_LUONG_KHONG_HOP_LE", $"Số lượng điều chuyển phải từ 1 đến {x.SoLuong}.", "soLuong");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        if (_context.Database.IsRelational())
        {
            // Tuần tự hóa các lần điều chuyển (khóa ứng dụng theo giao dịch) rồi đọc lại số liệu mới nhất,
            // tránh hai yêu cầu cùng đọc số lượng cũ và cùng cộng/trừ làm sai tổng số.
            await _context.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r = sp_getapplock @Resource = N'KLCN060_DIEU_CHUYEN_CSVC', @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 15000; IF @r < 0 THROW 51000, N'Không lấy được khóa điều chuyển.', 1;");
            await _context.Entry(x).ReloadAsync();
            if (_context.Entry(x).State == EntityState.Detached)
                throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_CO_SO_VAT_CHAT", "Không tìm thấy cơ sở vật chất (có thể vừa bị xóa).");
            if (x.TinhTrang == TinhTrangCsvc.DaThanhLy)
                throw new ApiException(StatusCodes.Status409Conflict, "THIET_BI_DA_THANH_LY", "Thiết bị đã thanh lý, không điều chuyển được.");
            if (dich == x.MaPhong)
                throw new ApiException(StatusCodes.Status400BadRequest, "VI_TRI_KHONG_DOI", "Thiết bị đã ở vị trí này rồi.", "maPhongMoi");
            if (soLuong > x.SoLuong)
                throw new ApiException(StatusCodes.Status409Conflict, "SO_LUONG_DA_THAY_DOI", $"Số lượng thiết bị vừa thay đổi (còn {x.SoLuong}), vui lòng tải lại và thử lại.", "soLuong");
        }
        var bayGio = DateTime.Now;
        var cu = x.MaPhong;
        // Dòng cùng tên + cùng tình trạng ở nơi đến thì gộp số lượng, không tạo dòng trùng.
        var trungDich = await _context.CoSoVatChats.FirstOrDefaultAsync(c => c.MaPhong == dich && c.Ten == x.Ten && c.TinhTrang == x.TinhTrang && c.MaSo != x.MaSo);
        CoSoVatChat ketQua;
        if (soLuong == x.SoLuong)
        {
            if (trungDich is not null)
            {
                trungDich.SoLuong += soLuong; trungDich.CapNhatLuc = bayGio; trungDich.NguoiCapNhat = _audit.TenDN;
                _context.CoSoVatChats.Remove(x);
                ketQua = trungDich;
            }
            else { x.MaPhong = dich; x.CapNhatLuc = bayGio; x.NguoiCapNhat = _audit.TenDN; ketQua = x; }
        }
        else
        {
            x.SoLuong -= soLuong; x.CapNhatLuc = bayGio; x.NguoiCapNhat = _audit.TenDN;
            if (trungDich is not null)
            {
                trungDich.SoLuong += soLuong; trungDich.CapNhatLuc = bayGio; trungDich.NguoiCapNhat = _audit.TenDN;
                ketQua = trungDich;
            }
            else
            {
                var maMoi = await MaMoiAsync();
                ketQua = new CoSoVatChat
                {
                    MaSo = maMoi, Ten = x.Ten, SoLuong = soLuong, TinhTrang = x.TinhTrang, MaPhong = dich, GhiChu = x.GhiChu,
                    TinhTrangTuLuc = x.TinhTrangTuLuc, LyDoTinhTrang = x.LyDoTinhTrang, NgayTao = bayGio, CapNhatLuc = bayGio, NguoiCapNhat = _audit.TenDN
                };
                _context.CoSoVatChats.Add(ketQua);
            }
        }

        var chiTiet = $"ten={x.Ten}; soLuong={soLuong}; tu={ViTri(cu)} -> den={ViTri(dich)}; ketQua={ketQua.MaSo}; lyDo={lyDo}";
        _audit.Ghi("DIEU_CHUYEN_CSVC", maSo, chiTiet);
        if (ketQua.MaSo != maSo) _audit.Ghi("DIEU_CHUYEN_CSVC", ketQua.MaSo, chiTiet + $"; nguon={maSo}");

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "XUNG_DOT_DU_LIEU", "Có xung đột dữ liệu khi điều chuyển, vui lòng tải lại và thử lại.");
        }
        await transaction.CommitAsync();
        return ToDto(ketQua);
    }

    // ------------------------------------------------------------------------------------------ tổng quan + lịch sử

    public async Task<FacilityOverviewDto> GetOverviewAsync()
    {
        var phongs = await _context.Phongs.AsNoTracking().OrderBy(p => p.MaPhong).ToListAsync();
        var phongTheoMa = phongs.ToDictionary(p => p.MaPhong);
        var csvc = await _context.CoSoVatChats.AsNoTracking().OrderBy(x => x.MaSo).ToListAsync();
        var coLichSu = await MaSoCoLichSuAsync();
        var homNay = DateTime.Now;

        var items = csvc.Select(x =>
        {
            phongTheoMa.TryGetValue(x.MaPhong ?? "", out var p);
            var hong = TinhTrangCsvc.LaHong(x.TinhTrang);
            int? soNgay = hong && x.TinhTrangTuLuc is { } tu ? Math.Max(0, (int)(homNay - tu).TotalDays) : null;
            var quaHan = hong && soNgay >= HongQuaHanNgay;
            var khongXoa = coLichSu.Contains(x.MaSo) ? "Thiết bị đã có lịch sử thay đổi/điều chuyển nên không xóa được - hãy chuyển sang \"Ngừng sử dụng\" hoặc \"Đã thanh lý\" để giữ lịch sử." : null;
            return new FacilityManageItemDto
            {
                MaSo = x.MaSo, Ten = x.Ten, SoLuong = x.SoLuong, TinhTrang = x.TinhTrang, MaPhong = x.MaPhong, TenPhong = p?.TenPhong, Tang = p?.Tang,
                GhiChu = x.GhiChu, TinhTrangTuLuc = x.TinhTrangTuLuc, LyDoTinhTrang = x.LyDoTinhTrang, NgayTao = x.NgayTao, CapNhatLuc = x.CapNhatLuc, NguoiCapNhat = x.NguoiCapNhat,
                SoNgayHong = soNgay, CanXuLy = hong,
                GoiY = x.TinhTrang switch
                {
                    TinhTrangCsvc.HongChoSua => quaHan ? $"Hỏng {soNgay} ngày chưa sửa - cần xử lý hoặc thay thế." : "Cần sắp xếp sửa chữa; nếu không sửa được hãy thay mới hoặc ngừng sử dụng.",
                    TinhTrangCsvc.HongDangSua => quaHan ? $"Đang sửa {soNgay} ngày - kiểm tra tiến độ." : "Đang sửa; cập nhật \"Tốt\" khi xong.",
                    TinhTrangCsvc.NgungSuDung => "Không dùng nữa nhưng còn trong sổ; thanh lý khi đã xử lý xong.",
                    _ => null
                },
                CoTheXoa = khongXoa is null, LyDoKhongXoa = khongXoa
            };
        }).ToList();

        var suCoMo = (await _context.SuCoPhongs.AsNoTracking()
            .Where(s => s.TrangThai != TrangThaiSuCo.DA_XU_LY && s.TrangThai != TrangThaiSuCo.DA_HUY)
            .GroupBy(s => s.MaPhong).Select(g => new { MaPhong = g.Key, N = g.Count() }).ToListAsync()).ToDictionary(x => x.MaPhong, x => x.N);

        var phongCanChuY = items.Where(i => i.CanXuLy && i.MaPhong != null).GroupBy(i => i.MaPhong!)
            .Select(g =>
            {
                var p = phongTheoMa[g.Key];
                return new FacilityRoomIssueDto
                {
                    MaPhong = p.MaPhong, TenPhong = p.TenPhong, Tang = p.Tang, TinhTrangPhong = p.TinhTrang.ToString(), SoThietBiHong = g.Sum(i => i.SoLuong),
                    SuCoDangMo = suCoMo.GetValueOrDefault(p.MaPhong), ThietBi = string.Join(", ", g.Select(i => $"{i.Ten} ({TinhTrangCsvc.HienThi(i.TinhTrang).ToLower()})"))
                };
            }).OrderByDescending(r => r.SoThietBiHong).ThenBy(r => r.MaPhong).ToList();

        var phongCoThietBi = items.Where(i => i.MaPhong != null && !TinhTrangCsvc.KhongConDung(i.TinhTrang)).Select(i => i.MaPhong!).ToHashSet();
        var chuaKhaiBao = phongs.Where(p => !phongCoThietBi.Contains(p.MaPhong)).Select(p => p.TenPhong).ToList();

        var theoTinhTrang = TinhTrangCsvc.TatCa.Select(t => new FacilityStatusCountDto
        {
            TinhTrang = t, SoDong = items.Count(i => i.TinhTrang == t), SoLuong = items.Where(i => i.TinhTrang == t).Sum(i => i.SoLuong)
        }).ToList();

        return new FacilityOverviewDto
        {
            CapNhatLuc = DateTime.Now,
            PhongCanChuY = phongCanChuY,
            PhongChuaKhaiBao = chuaKhaiBao,
            ThietBis = items,
            TongHop = new FacilitySummaryDto
            {
                TongDong = items.Count(i => i.TinhTrang != TinhTrangCsvc.DaThanhLy), TongSoLuong = items.Where(i => i.TinhTrang != TinhTrangCsvc.DaThanhLy).Sum(i => i.SoLuong),
                SoLuongDangDung = items.Where(i => TinhTrangCsvc.DangDung(i.TinhTrang)).Sum(i => i.SoLuong),
                SoLuongHong = items.Where(i => i.CanXuLy).Sum(i => i.SoLuong),
                SoLuongTrongKho = items.Where(i => i.MaPhong == null && !TinhTrangCsvc.KhongConDung(i.TinhTrang)).Sum(i => i.SoLuong),
                SoPhongCoThietBiHong = phongCanChuY.Count, SoPhongChuaKhaiBao = chuaKhaiBao.Count,
                HongQuaHan = items.Count(i => i.CanXuLy && i.SoNgayHong >= HongQuaHanNgay), HongQuaHanNgay = HongQuaHanNgay, TheoTinhTrang = theoTinhTrang
            }
        };
    }

    public async Task<List<FacilityHistoryEntryDto>> GetHistoryAsync(string maSo)
    {
        var rows = await _context.NhatKyThaoTacs.AsNoTracking()
            .Where(x => x.DoiTuongTacDong == maSo && HanhDongCsvc.Contains(x.HanhDong))
            .OrderByDescending(x => x.ThoiGian).Take(100).ToListAsync();
        // Thiết bị đã bị gộp/xóa vẫn xem được lịch sử từ nhật ký; chỉ báo không tìm thấy khi chưa từng có dòng nào.
        if (rows.Count == 0) await TimCoSoVatChatAsync(maSo);
        return rows.Select(x => new FacilityHistoryEntryDto
        {
            ThoiGian = x.ThoiGian, NguoiThucHien = x.MaTaiKhoan, HanhDong = x.HanhDong, MoTaHanhDong = AuditLogService.MoTaHanhDong(x.HanhDong), ChiTiet = x.ChiTiet
        }).ToList();
    }

    // ------------------------------------------------------------------------------------------ hỗ trợ

    /// <summary>Thiết bị có dòng nhật ký nào ngoài lúc tạo (sửa, đổi tình trạng, điều chuyển) thì không xóa được.</summary>
    private async Task<HashSet<string>> MaSoCoLichSuAsync()
        => (await _context.NhatKyThaoTacs.AsNoTracking()
            .Where(x => x.DoiTuongTacDong != null && (x.HanhDong == "SUA_CO_SO_VAT_CHAT" || x.HanhDong == "DOI_TINH_TRANG_CSVC" || x.HanhDong == "DIEU_CHUYEN_CSVC"))
            .Select(x => x.DoiTuongTacDong!).Distinct().ToListAsync()).ToHashSet();

    private async Task<string?> LyDoKhongXoaAsync(string maSo, CoSoVatChat x)
    {
        if (x.TinhTrang == TinhTrangCsvc.DaThanhLy) return "Thiết bị đã thanh lý được giữ trong sổ để truy vết, không xóa.";
        if ((await MaSoCoLichSuAsync()).Contains(maSo))
            return "Thiết bị đã có lịch sử thay đổi/điều chuyển nên không xóa được - hãy chuyển sang \"Ngừng sử dụng\" hoặc \"Đã thanh lý\" để giữ lịch sử.";
        return null;
    }

    private static (string Ten, string TinhTrang, string? GhiChu, string? LyDo) ChuanHoa(FacilityRequest r, string? tinhTrangCu)
    {
        var ten = r.Ten?.Trim() ?? "";
        if (ten.Length < 2)
            throw new ApiException(StatusCodes.Status400BadRequest, "TEN_THIET_BI_KHONG_HOP_LE", "Tên thiết bị tối thiểu 2 ký tự.", "ten");
        if (r.SoLuong < 1 || r.SoLuong > 9999)
            throw new ApiException(StatusCodes.Status400BadRequest, "SO_LUONG_KHONG_HOP_LE", "Số lượng phải từ 1 đến 9.999.", "soLuong");
        var tinhTrang = r.TinhTrang?.Trim().ToUpperInvariant() ?? "";
        if (!TinhTrangCsvc.TatCa.Contains(tinhTrang))
            throw new ApiException(StatusCodes.Status400BadRequest, "TINH_TRANG_KHONG_HOP_LE", "Tình trạng chỉ nhận: TOT, HONG_CHO_SUA, HONG_DANG_SUA, DA_THAY_MOI, NGUNG_SU_DUNG, DA_THANH_LY.", "tinhTrang");
        var lyDo = string.IsNullOrWhiteSpace(r.LyDo) ? null : r.LyDo.Trim();
        if ((tinhTrangCu is null || tinhTrangCu != tinhTrang) && tinhTrang is TinhTrangCsvc.NgungSuDung or TinhTrangCsvc.DaThanhLy && lyDo is null)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_LY_DO", "Nhập lý do khi chuyển thiết bị sang \"Ngừng sử dụng\" hoặc \"Đã thanh lý\".", "lyDo");
        return (ten, tinhTrang, string.IsNullOrWhiteSpace(r.GhiChu) ? null : r.GhiChu.Trim(), lyDo);
    }

    /// <summary>
    /// Sinh mã mới và KHÔNG tái sử dụng mã đã từng cấp: ngoài các dòng còn trong bảng, tính cả mã xuất hiện trong nhật ký
    /// (thiết bị đã xóa) - nếu không, thiết bị mới nhận lại mã cũ và lịch sử của hai thiết bị lẫn vào nhau.
    /// </summary>
    private async Task<string> MaMoiAsync()
    {
        await MaCodeGenerator.KhoaTienToAsync(_context, "CSVC");
        var conTrongBang = await _context.CoSoVatChats.Select(c => c.MaSo).ToListAsync();
        var daTungCap = await _context.NhatKyThaoTacs.AsNoTracking()
            .Where(n => n.DoiTuongTacDong != null && n.DoiTuongTacDong.StartsWith("CSVC"))
            .Select(n => n.DoiTuongTacDong!).Distinct().ToListAsync();
        return MaCodeGenerator.GenerateNext(conTrongBang.Concat(daTungCap), "CSVC", 3);
    }

    /// <summary>Duy nhất theo (vị trí, tên không phân biệt dấu/hoa thường, tình trạng): cho phép "3 bóng tốt + 2 bóng hỏng" cùng tên.</summary>
    private async Task KiemTraTrungAsync(string ten, string? maPhong, string tinhTrang, string? boQuaMaSo)
    {
        var cungViTri = await _context.CoSoVatChats.AsNoTracking()
            .Where(x => x.MaPhong == maPhong && x.MaSo != boQuaMaSo && x.TinhTrang != TinhTrangCsvc.DaThanhLy)
            .ToListAsync();
        var trung = cungViTri.FirstOrDefault(x => x.TinhTrang == tinhTrang && VanBanHelper.CungTen(x.Ten, ten));
        if (trung is not null)
            throw new ApiException(StatusCodes.Status409Conflict, "CO_SO_VAT_CHAT_TRUNG",
                $"{(maPhong is null ? "Kho" : $"Phòng {maPhong}")} đã có thiết bị \"{ten}\" cùng tình trạng ({trung.MaSo}). Hãy sửa số lượng của thiết bị đó thay vì thêm dòng trùng.", "ten");
    }

    private static string ViTri(string? maPhong) => maPhong is null ? "kho" : $"phòng {maPhong}";

    private async Task<CoSoVatChat> TimCoSoVatChatAsync(string maSo)
    {
        var coSoVatChat = await _context.CoSoVatChats.FirstOrDefaultAsync(x => x.MaSo == maSo);
        if (coSoVatChat is null)
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_CO_SO_VAT_CHAT", "Không tìm thấy cơ sở vật chất.");
        return coSoVatChat;
    }

    private static FacilityDto ToDto(CoSoVatChat x) => new()
    {
        MaSo = x.MaSo,
        Ten = x.Ten,
        SoLuong = x.SoLuong,
        TinhTrang = x.TinhTrang,
        MaPhong = x.MaPhong
    };
}
