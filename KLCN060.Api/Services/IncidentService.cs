using KLCN060.Api.Auditing;
using KLCN060.Api.DTOs.Housekeeping;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public interface IIncidentService
{
    Task<List<IncidentDto>> GetAllAsync(string? trangThai, string? maPhong, string? mucDo, string? loai, string? nguoiBao);
    Task<IncidentDto> GetAsync(int maSuCo);
    Task<IncidentDto> CreateAsync(CreateIncidentRequest request, string tenDN);
    Task<IncidentDto> UpdateStatusAsync(int maSuCo, UpdateIncidentStatusRequest request, string tenDN);
}

/// <summary>
/// Sự cố trong phòng: buồng phòng/lễ tân báo (phòng - loại - tiêu đề - mức độ), Quản lý (quyền XU_LY_SU_CO) tiếp nhận → xử lý → xong.
/// Báo sự cố KHÔNG tự khóa phòng: khóa/mở phòng là quyết định vận hành riêng (quyền KHOA_MO_PHONG) nên danh sách cho biết tình trạng hiện tại của phòng.
/// </summary>
public class IncidentService : IIncidentService
{
    private const int SoDongToiDa = 300;
    private static readonly TrangThaiSuCo[] DangMo = { TrangThaiSuCo.MOI, TrangThaiSuCo.DA_TIEP_NHAN, TrangThaiSuCo.DANG_XU_LY };

    private static readonly Dictionary<TrangThaiSuCo, TrangThaiSuCo[]> ChuyenHopLe = new()
    {
        [TrangThaiSuCo.MOI] = new[] { TrangThaiSuCo.DA_TIEP_NHAN, TrangThaiSuCo.DANG_XU_LY, TrangThaiSuCo.DA_XU_LY, TrangThaiSuCo.DA_HUY },
        [TrangThaiSuCo.DA_TIEP_NHAN] = new[] { TrangThaiSuCo.DANG_XU_LY, TrangThaiSuCo.DA_XU_LY, TrangThaiSuCo.DA_HUY },
        [TrangThaiSuCo.DANG_XU_LY] = new[] { TrangThaiSuCo.DA_XU_LY, TrangThaiSuCo.DA_HUY },
        [TrangThaiSuCo.DA_XU_LY] = new[] { TrangThaiSuCo.DANG_XU_LY },
        [TrangThaiSuCo.DA_HUY] = Array.Empty<TrangThaiSuCo>()
    };

    private readonly KLCN060DbContext _context;
    private readonly IAuditTrail _audit;
    private readonly IPermissionService _permissions;

    public IncidentService(KLCN060DbContext context, IAuditTrail audit, IPermissionService permissions)
    {
        _context = context;
        _audit = audit;
        _permissions = permissions;
    }

    public async Task<List<IncidentDto>> GetAllAsync(string? trangThai, string? maPhong, string? mucDo, string? loai, string? nguoiBao)
    {
        var q = _context.SuCoPhongs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(trangThai))
        {
            if (trangThai.Trim().Equals("DANG_MO", StringComparison.OrdinalIgnoreCase))
                q = q.Where(x => DangMo.Contains(x.TrangThai));
            else
            {
                var tt = ParseEnum<TrangThaiSuCo>(trangThai, "trangThai", "Trạng thái sự cố không hợp lệ (MOI, DA_TIEP_NHAN, DANG_XU_LY, DA_XU_LY, DA_HUY hoặc DANG_MO).");
                q = q.Where(x => x.TrangThai == tt);
            }
        }
        if (!string.IsNullOrWhiteSpace(maPhong)) q = q.Where(x => x.MaPhong == maPhong.Trim());
        if (!string.IsNullOrWhiteSpace(mucDo))
        {
            var md = ParseEnum<MucDoSuCo>(mucDo, "mucDo", "Mức độ không hợp lệ (THAP, TRUNG_BINH, CAO, KHAN_CAP).");
            q = q.Where(x => x.MucDo == md);
        }
        if (!string.IsNullOrWhiteSpace(loai))
        {
            var l = ParseEnum<LoaiSuCo>(loai, "loai", "Loại sự cố không hợp lệ.");
            q = q.Where(x => x.Loai == l);
        }
        if (!string.IsNullOrWhiteSpace(nguoiBao)) q = q.Where(x => x.NguoiBao == nguoiBao.Trim());

        var dong = await q.Select(x => new Row(x, x.Phong.TenPhong, x.Phong.TinhTrang)).ToListAsync();
        // Sự cố còn mở lên trước, nặng trước, cũ trước (cần xử lý gấp); đã đóng sau cùng, mới trước.
        var sapXep = dong
            .OrderBy(x => x.SuCo.TrangThai is TrangThaiSuCo.DA_XU_LY or TrangThaiSuCo.DA_HUY ? 1 : 0)
            .ThenByDescending(x => x.SuCo.TrangThai is TrangThaiSuCo.DA_XU_LY or TrangThaiSuCo.DA_HUY ? 0 : (int)x.SuCo.MucDo)
            .ThenBy(x => x.SuCo.TrangThai is TrangThaiSuCo.DA_XU_LY or TrangThaiSuCo.DA_HUY ? DateTime.MaxValue : x.SuCo.ThoiGianBao)
            .ThenByDescending(x => x.SuCo.ThoiGianBao)
            .Take(SoDongToiDa).ToList();
        return await ToDtosAsync(sapXep);
    }

    public async Task<IncidentDto> GetAsync(int maSuCo)
    {
        var dong = await _context.SuCoPhongs.AsNoTracking().Where(x => x.MaSuCo == maSuCo)
            .Select(x => new Row(x, x.Phong.TenPhong, x.Phong.TinhTrang)).FirstOrDefaultAsync()
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_SU_CO", "Không tìm thấy sự cố.");
        return (await ToDtosAsync(new List<Row> { dong }))[0];
    }

    public async Task<IncidentDto> CreateAsync(CreateIncidentRequest request, string tenDN)
    {
        // Người có quyền xử lý sự cố (Quản lý) là bên tiếp nhận: sự cố do nhân viên buồng phòng báo, Quản lý không tự báo rồi tự xử lý.
        if (await _permissions.CoQuyenAsync(tenDN, Permissions.XuLySuCo))
            throw new ApiException(StatusCodes.Status403Forbidden, "QUAN_LY_KHONG_BAO_SU_CO",
                "Quản lý là người tiếp nhận và xử lý sự cố, không báo sự cố. Sự cố do nhân viên buồng phòng báo.");
        var maPhong = request.MaPhong.Trim();
        if (!await _context.Phongs.AnyAsync(x => x.MaPhong == maPhong))
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_PHONG", "Không tìm thấy phòng.", "maPhong");
        var loai = ParseEnum<LoaiSuCo>(request.Loai, "loai", "Loại sự cố không hợp lệ (DIEU_HOA, DIEN_CHIEU_SANG, NUOC, TV_THIET_BI_DIEN_TU, NOI_THAT, THIEU_TRANG_THIET_BI, VE_SINH, KHAC).");
        var mucDo = ParseEnum<MucDoSuCo>(request.MucDo, "mucDo", "Mức độ không hợp lệ (THAP, TRUNG_BINH, CAO, KHAN_CAP).");
        var tieuDe = request.TieuDe.Trim();
        if (tieuDe.Length == 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_TIEU_DE", "Chưa nhập tiêu đề sự cố.", "tieuDe");

        // Cùng phòng, cùng loại, cùng tiêu đề mà sự cố trước chưa đóng: báo trùng (tránh nhiều người báo cùng một hỏng hóc).
        var tieuDeThuong = tieuDe.ToLower();
        var trung = await _context.SuCoPhongs.AsNoTracking()
            .Where(x => x.MaPhong == maPhong && x.Loai == loai && DangMo.Contains(x.TrangThai) && x.TieuDe.ToLower() == tieuDeThuong)
            .Select(x => (int?)x.MaSuCo).FirstOrDefaultAsync();
        if (trung is not null)
            throw new ApiException(StatusCodes.Status409Conflict, "SU_CO_DA_TON_TAI", $"Sự cố này đã được báo (SC{trung:0000}) và chưa xử lý xong.", "tieuDe");

        var suCo = new SuCoPhong
        {
            MaPhong = maPhong, Loai = loai, TieuDe = tieuDe, MoTa = string.IsNullOrWhiteSpace(request.MoTa) ? null : request.MoTa.Trim(),
            MucDo = mucDo, TrangThai = TrangThaiSuCo.MOI, NguoiBao = tenDN, ThoiGianBao = DateTime.Now
        };
        _context.SuCoPhongs.Add(suCo);
        _audit.Ghi("BAO_SU_CO", maPhong, $"loai={loai}; mucDo={mucDo}; tieuDe={tieuDe}");
        await _context.SaveChangesAsync();
        return await GetAsync(suCo.MaSuCo);
    }

    public async Task<IncidentDto> UpdateStatusAsync(int maSuCo, UpdateIncidentStatusRequest request, string tenDN)
    {
        var suCo = await _context.SuCoPhongs.AsNoTracking().FirstOrDefaultAsync(x => x.MaSuCo == maSuCo)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_SU_CO", "Không tìm thấy sự cố.");
        var moi = ParseEnum<TrangThaiSuCo>(request.TrangThai, "trangThai", "Trạng thái không hợp lệ (DA_TIEP_NHAN, DANG_XU_LY, DA_XU_LY, DA_HUY).");
        var cu = suCo.TrangThai;
        var ghiChu = string.IsNullOrWhiteSpace(request.GhiChu) ? null : request.GhiChu.Trim();

        // Người báo chỉ được hủy sự cố báo nhầm khi chưa ai tiếp nhận; mọi thay đổi khác cần quyền XU_LY_SU_CO.
        var xuLy = await _permissions.CoQuyenAsync(tenDN, Permissions.XuLySuCo);
        var nguoiBaoHuy = moi == TrangThaiSuCo.DA_HUY && cu == TrangThaiSuCo.MOI && suCo.NguoiBao == tenDN;
        if (!xuLy && !nguoiBaoHuy)
            throw new ApiException(StatusCodes.Status403Forbidden, "CAN_QUYEN_XU_LY_SU_CO",
                "Chỉ quản lý (quyền XU_LY_SU_CO) mới tiếp nhận/xử lý sự cố; người báo chỉ hủy được sự cố báo nhầm khi chưa ai tiếp nhận.");

        if (cu == moi || !ChuyenHopLe[cu].Contains(moi))
            throw new ApiException(StatusCodes.Status422UnprocessableEntity, "INVALID_STATUS_TRANSITION",
                $"Không thể chuyển sự cố từ {cu} sang {moi}." + (ChuyenHopLe[cu].Length == 0 ? " Sự cố đã hủy không mở lại được." : $" Các trạng thái được phép: {string.Join(", ", ChuyenHopLe[cu])}."), "trangThai");
        var moLai = cu == TrangThaiSuCo.DA_XU_LY && moi == TrangThaiSuCo.DANG_XU_LY;
        if ((moi is TrangThaiSuCo.DA_XU_LY or TrangThaiSuCo.DA_HUY || moLai) && ghiChu is null)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_GHI_CHU_XU_LY",
                moi == TrangThaiSuCo.DA_XU_LY ? "Cần ghi chú cách đã xử lý sự cố." : moLai ? "Cần ghi lý do mở lại sự cố (chưa khắc phục được)." : "Cần ghi lý do hủy sự cố.", "ghiChu");

        var bayGio = DateTime.Now;
        var ghiChuMoi = ghiChu ?? suCo.GhiChuXuLy;
        string? nguoiXuLy; DateTime? tiepNhan; DateTime? xong; string? nguoiDong;
        if (moi is TrangThaiSuCo.DA_TIEP_NHAN or TrangThaiSuCo.DANG_XU_LY)
        {
            nguoiXuLy = tenDN;
            tiepNhan = suCo.ThoiGianTiepNhan ?? bayGio;
            xong = null;   // mở lại
            nguoiDong = null;
        }
        else
        {
            nguoiXuLy = suCo.NguoiXuLy ?? (xuLy ? tenDN : null);
            tiepNhan = suCo.ThoiGianTiepNhan;
            xong = bayGio;
            nguoiDong = tenDN;     // ai đóng sự cố (xong hoặc hủy) - truy vết khi có tranh chấp
        }

        // Chuyển trạng thái bằng MỘT câu lệnh có điều kiện "đang ở trạng thái cũ": hai người xử lý cùng lúc thì chỉ một người thắng, người sau nhận 409 (không ghi đè, không ghi nhật ký giả).
        int soDong;
        if (_context.Database.IsRelational())
        {
            soDong = await _context.SuCoPhongs.Where(x => x.MaSuCo == maSuCo && x.TrangThai == cu)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.TrangThai, moi).SetProperty(x => x.GhiChuXuLy, ghiChuMoi)
                    .SetProperty(x => x.NguoiXuLy, nguoiXuLy).SetProperty(x => x.ThoiGianTiepNhan, tiepNhan)
                    .SetProperty(x => x.ThoiGianXong, xong).SetProperty(x => x.NguoiDong, nguoiDong));
        }
        else
        {
            // CSDL trong bộ nhớ (test đơn vị) không hỗ trợ cập nhật hàng loạt: cập nhật thực thể như bình thường.
            var tracked = await _context.SuCoPhongs.FirstAsync(x => x.MaSuCo == maSuCo);
            soDong = tracked.TrangThai == cu ? 1 : 0;
            if (soDong == 1)
            {
                tracked.TrangThai = moi; tracked.GhiChuXuLy = ghiChuMoi; tracked.NguoiXuLy = nguoiXuLy;
                tracked.ThoiGianTiepNhan = tiepNhan; tracked.ThoiGianXong = xong; tracked.NguoiDong = nguoiDong;
            }
        }
        if (soDong == 0)
            throw new ApiException(StatusCodes.Status409Conflict, "SU_CO_DA_THAY_DOI", "Sự cố vừa được người khác cập nhật. Hãy tải lại danh sách rồi thao tác lại.", "trangThai");
        _audit.Ghi("DOI_TRANG_THAI_SU_CO", suCo.MaPhong, $"suCo=SC{suCo.MaSuCo:0000}; {cu} -> {moi}; nguoiThucHien={tenDN}" + (ghiChu is null ? "" : $"; lyDo/ghiChu={ghiChu}"));
        await _context.SaveChangesAsync();
        return await GetAsync(maSuCo);
    }

    private sealed record Row(SuCoPhong SuCo, string TenPhong, TinhTrangPhong TinhTrangPhong);

    private async Task<List<IncidentDto>> ToDtosAsync(List<Row> dong)
    {
        var ten = await TenNhanVien.TraCuuAsync(_context, dong.SelectMany(x => new[] { x.SuCo.NguoiBao, x.SuCo.NguoiXuLy, x.SuCo.NguoiDong }));
        var bayGio = DateTime.Now;
        return dong.Select(x => new IncidentDto
        {
            MaSuCo = x.SuCo.MaSuCo, MaPhong = x.SuCo.MaPhong, TenPhong = x.TenPhong, TinhTrangPhong = x.TinhTrangPhong.ToString(),
            Loai = x.SuCo.Loai.ToString(), TieuDe = x.SuCo.TieuDe, MoTa = x.SuCo.MoTa, MucDo = x.SuCo.MucDo.ToString(), TrangThai = x.SuCo.TrangThai.ToString(),
            NguoiBao = x.SuCo.NguoiBao, HoTenNguoiBao = ten.GetValueOrDefault(x.SuCo.NguoiBao),
            ThoiGianBao = x.SuCo.ThoiGianBao,
            NguoiXuLy = x.SuCo.NguoiXuLy, HoTenNguoiXuLy = x.SuCo.NguoiXuLy is null ? null : ten.GetValueOrDefault(x.SuCo.NguoiXuLy),
            ThoiGianTiepNhan = x.SuCo.ThoiGianTiepNhan, ThoiGianXong = x.SuCo.ThoiGianXong, GhiChuXuLy = x.SuCo.GhiChuXuLy,
            NguoiDong = x.SuCo.NguoiDong, HoTenNguoiDong = x.SuCo.NguoiDong is null ? null : ten.GetValueOrDefault(x.SuCo.NguoiDong),
            SoGio = Math.Round(((x.SuCo.ThoiGianXong ?? bayGio) - x.SuCo.ThoiGianBao).TotalHours, 1)
        }).ToList();
    }

    private static T ParseEnum<T>(string giaTri, string truong, string thongDiep) where T : struct, Enum
        => EnumParser.Parse<T>(giaTri, truong, thongDiep);
}
