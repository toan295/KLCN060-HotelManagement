using KLCN060.Api.Auditing;
using KLCN060.Api.DTOs.Housekeeping;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

/// <summary>Vị trí lưu ảnh đồ bỏ quên: cấu hình Media:LostFoundPath, mặc định {thư mục dự án API}/Uploads/lostfound. Ảnh KHÔNG phục vụ công khai - chỉ tải qua API có xác thực.</summary>
public static class LostFoundImageStorage
{
    public static string ResolveRoot(IConfiguration configuration, IHostEnvironment env)
    {
        var cauHinh = configuration["Media:LostFoundPath"];
        return string.IsNullOrWhiteSpace(cauHinh)
            ? Path.Combine(env.ContentRootPath, "Uploads", "lostfound")
            : Path.GetFullPath(cauHinh.Trim());
    }
}

public interface ILostFoundService
{
    Task<List<LostFoundDto>> GetAllAsync(string? trangThai, string? maPhong, string? tuKhoa, DateOnly? tuNgay, DateOnly? denNgay, string tenDN);
    Task<LostFoundDto> GetAsync(int maDo, string tenDN);
    Task<LostFoundDto> CreateAsync(CreateLostFoundRequest request, string tenDN);
    Task<LostFoundDto> UpdateAsync(int maDo, UpdateLostFoundRequest request, string tenDN);
    Task<LostFoundDto> StoreAsync(int maDo, StoreLostFoundRequest request, string tenDN);
    Task<LostFoundDto> ReturnAsync(int maDo, ReturnLostFoundRequest request, string tenDN);
    Task<LostFoundDto> DisposeAsync(int maDo, DisposeLostFoundRequest request, string tenDN);
    Task<LostFoundDto> AddImageAsync(int maDo, IFormFile? file, string tenDN);
    Task<(string DuongDan, string ContentType)> GetImageAsync(int maDo, int maAnh);
    Task<LostFoundDto> DeleteImageAsync(int maDo, int maAnh, string tenDN);
}

/// <summary>
/// Đồ khách bỏ quên (Lost &amp; Found). Vòng đời: Đã tìm thấy → (nhập vị trí) Đang lưu giữ → Đã trả khách (lễ tân, có người nhận + giấy tờ đối chiếu)
/// hoặc Đã thanh lý (quản lý, sau tối thiểu 30 ngày lưu giữ, có lý do). Buồng phòng ghi nhận/lưu kho nhưng không trả, không thanh lý và không thấy thông tin khách.
/// </summary>
public class LostFoundService : ILostFoundService
{
    public const int SoNgayLuuGiuToiThieu = 30;
    private const int SoAnhToiDa = 3;
    private const long KichThuocAnhToiDa = 5 * 1024 * 1024;
    private const int SoNgayLuiToiDa = 365;
    private const int SoDongToiDa = 300;

    private readonly KLCN060DbContext _context;
    private readonly IAuditTrail _audit;
    private readonly IPermissionService _permissions;
    private readonly string _root;

    public LostFoundService(KLCN060DbContext context, IAuditTrail audit, IPermissionService permissions, IConfiguration configuration, IHostEnvironment env)
    {
        _context = context;
        _audit = audit;
        _permissions = permissions;
        _root = LostFoundImageStorage.ResolveRoot(configuration, env);
    }

    // ------------------------------------------------------------------ xem

    public async Task<List<LostFoundDto>> GetAllAsync(string? trangThai, string? maPhong, string? tuKhoa, DateOnly? tuNgay, DateOnly? denNgay, string tenDN)
    {
        var q = _context.DoBoQuens.AsNoTracking().Include(x => x.Anhs).AsQueryable();
        if (!string.IsNullOrWhiteSpace(trangThai))
        {
            if (trangThai.Trim().Equals("DANG_CON", StringComparison.OrdinalIgnoreCase))
                q = q.Where(x => x.TrangThai == TrangThaiDoBoQuen.DA_TIM_THAY || x.TrangThai == TrangThaiDoBoQuen.DANG_LUU_GIU);
            else
            {
                var tt = EnumParser.Parse<TrangThaiDoBoQuen>(trangThai, "trangThai", "Trạng thái không hợp lệ (DA_TIM_THAY, DANG_LUU_GIU, DA_TRA_KHACH, DA_THANH_LY hoặc DANG_CON).");
                q = q.Where(x => x.TrangThai == tt);
            }
        }
        if (!string.IsNullOrWhiteSpace(maPhong)) q = q.Where(x => x.MaPhong == maPhong.Trim());
        if (!string.IsNullOrWhiteSpace(tuKhoa))
        {
            var k = tuKhoa.Trim();
            q = q.Where(x => x.MoTa.Contains(k) || (x.ViTriLuuGiu != null && x.ViTriLuuGiu.Contains(k)) || (x.KhuVuc != null && x.KhuVuc.Contains(k)) || x.NguoiNhat.Contains(k));
        }
        if (tuNgay is not null) { var tu = tuNgay.Value.ToDateTime(TimeOnly.MinValue); q = q.Where(x => x.ThoiGianPhatHien >= tu); }
        if (denNgay is not null) { var den = denNgay.Value.AddDays(1).ToDateTime(TimeOnly.MinValue); q = q.Where(x => x.ThoiGianPhatHien < den); }

        var dong = await q.OrderByDescending(x => x.ThoiGianPhatHien).ThenByDescending(x => x.MaDo).Take(SoDongToiDa).ToListAsync();
        return await ToDtosAsync(dong, await CoXemKhachAsync(tenDN));
    }

    public async Task<LostFoundDto> GetAsync(int maDo, string tenDN)
    {
        var x = await _context.DoBoQuens.AsNoTracking().Include(d => d.Anhs).FirstOrDefaultAsync(d => d.MaDo == maDo)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_DO_BO_QUEN", "Không tìm thấy đồ bỏ quên.");
        return (await ToDtosAsync(new List<DoBoQuen> { x }, await CoXemKhachAsync(tenDN)))[0];
    }

    // ------------------------------------------------------------------ ghi nhận / cập nhật

    public async Task<LostFoundDto> CreateAsync(CreateLostFoundRequest request, string tenDN)
    {
        var bayGio = DateTime.Now;
        var maPhong = string.IsNullOrWhiteSpace(request.MaPhong) ? null : request.MaPhong.Trim();
        var khuVuc = string.IsNullOrWhiteSpace(request.KhuVuc) ? null : request.KhuVuc.Trim();
        await KiemTraViTriAsync(maPhong, khuVuc);
        var loai = EnumParser.Parse<LoaiDoBoQuen>(request.Loai, "loai", LoiLoai);

        var phatHien = request.ThoiGianPhatHien ?? bayGio;
        if (phatHien > bayGio.AddMinutes(5))
            throw new ApiException(StatusCodes.Status400BadRequest, "THOI_GIAN_TUONG_LAI", "Thời gian phát hiện không được ở tương lai.", "thoiGianPhatHien");
        if (phatHien < bayGio.AddDays(-SoNgayLuiToiDa))
            throw new ApiException(StatusCodes.Status400BadRequest, "THOI_GIAN_QUA_CU", $"Thời gian phát hiện không được cũ hơn {SoNgayLuiToiDa} ngày.", "thoiGianPhatHien");

        var moTa = request.MoTa.Trim();
        if (moTa.Length == 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_MO_TA", "Chưa nhập mô tả đồ vật.", "moTa");
        var viTri = string.IsNullOrWhiteSpace(request.ViTriLuuGiu) ? null : request.ViTriLuuGiu.Trim();

        var doVat = new DoBoQuen
        {
            MaPhong = maPhong, KhuVuc = khuVuc, ThoiGianPhatHien = phatHien, Loai = loai, MoTa = moTa,
            NguoiNhat = string.IsNullOrWhiteSpace(request.NguoiNhat) ? await HoTenAsync(tenDN) : request.NguoiNhat.Trim(),
            NguoiGhiNhan = tenDN, ViTriLuuGiu = viTri,
            TrangThai = viTri is null ? TrangThaiDoBoQuen.DA_TIM_THAY : TrangThaiDoBoQuen.DANG_LUU_GIU,
            MaPhieuNhan = await LuotOGanNhatAsync(maPhong, phatHien)
        };
        _context.DoBoQuens.Add(doVat);
        _audit.Ghi("GHI_NHAN_DO_BO_QUEN", maPhong ?? khuVuc, $"loai={loai}; moTa={Cat(moTa, 80)}; viTri={viTri ?? "chua luu kho"}; nguoiNhat={doVat.NguoiNhat}");
        await _context.SaveChangesAsync();
        return await GetAsync(doVat.MaDo, tenDN);
    }

    public async Task<LostFoundDto> UpdateAsync(int maDo, UpdateLostFoundRequest request, string tenDN)
    {
        var doVat = await TimDangMoAsync(maDo);
        var maPhong = string.IsNullOrWhiteSpace(request.MaPhong) ? null : request.MaPhong.Trim();
        var khuVuc = string.IsNullOrWhiteSpace(request.KhuVuc) ? null : request.KhuVuc.Trim();
        await KiemTraViTriAsync(maPhong, khuVuc);
        var loai = EnumParser.Parse<LoaiDoBoQuen>(request.Loai, "loai", LoiLoai);
        var moTa = request.MoTa.Trim();
        if (moTa.Length == 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_MO_TA", "Chưa nhập mô tả đồ vật.", "moTa");

        var truoc = $"phong={doVat.MaPhong ?? doVat.KhuVuc}; loai={doVat.Loai}; moTa={Cat(doVat.MoTa, 60)}";
        if (doVat.MaPhong != maPhong) doVat.MaPhieuNhan = await LuotOGanNhatAsync(maPhong, doVat.ThoiGianPhatHien);
        doVat.MaPhong = maPhong;
        doVat.KhuVuc = khuVuc;
        doVat.Loai = loai;
        doVat.MoTa = moTa;
        if (!string.IsNullOrWhiteSpace(request.NguoiNhat)) doVat.NguoiNhat = request.NguoiNhat.Trim();
        _audit.Ghi("SUA_DO_BO_QUEN", $"DQ{maDo:0000}", $"truoc: {truoc} | sau: phong={maPhong ?? khuVuc}; loai={loai}; moTa={Cat(moTa, 60)}");
        await _context.SaveChangesAsync();
        return await GetAsync(maDo, tenDN);
    }

    public async Task<LostFoundDto> StoreAsync(int maDo, StoreLostFoundRequest request, string tenDN)
    {
        var doVat = await TimDangMoAsync(maDo);
        var viTri = request.ViTriLuuGiu.Trim();
        if (viTri.Length == 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_VI_TRI", "Chưa nhập vị trí lưu giữ.", "viTriLuuGiu");

        var truoc = doVat.ViTriLuuGiu;
        // Chuyển trạng thái có điều kiện: nếu món đồ vừa được người khác trả khách/thanh lý thì không "mở lại" nó.
        var daLuu = await _context.DoBoQuens.Where(x => x.MaDo == maDo && (x.TrangThai == TrangThaiDoBoQuen.DA_TIM_THAY || x.TrangThai == TrangThaiDoBoQuen.DANG_LUU_GIU))
            .CapNhatCoDieuKienAsync(_context, u => u.SetProperty(x => x.ViTriLuuGiu, viTri).SetProperty(x => x.TrangThai, TrangThaiDoBoQuen.DANG_LUU_GIU),
                x => { x.ViTriLuuGiu = viTri; x.TrangThai = TrangThaiDoBoQuen.DANG_LUU_GIU; });
        if (daLuu != 1) throw DaDong();
        _audit.Ghi("LUU_KHO_DO_BO_QUEN", $"DQ{maDo:0000}", $"viTri={viTri}" + (truoc is null ? "" : $"; viTriCu={truoc}"));
        await _context.SaveChangesAsync();
        return await GetAsync(maDo, tenDN);
    }

    public async Task<LostFoundDto> ReturnAsync(int maDo, ReturnLostFoundRequest request, string tenDN)
    {
        var doVat = await TimDangMoAsync(maDo);
        var nguoiNhan = request.NguoiNhanLai.Trim();
        var giayTo = request.GiayToNguoiNhan.Trim();
        if (nguoiNhan.Length == 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_NGUOI_NHAN", "Chưa nhập tên người nhận lại.", "nguoiNhanLai");
        if (giayTo.Length == 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_GIAY_TO", "Chưa nhập giấy tờ đối chiếu của người nhận (CCCD, hộ chiếu...).", "giayToNguoiNhan");

        // Chỉ một người trả được: hai lễ tân bấm cùng lúc thì người sau nhận 409, không ghi đè tên người nhận/giấy tờ của lần trả đầu.
        var luc = DateTime.Now;
        var ghiChuTra = string.IsNullOrWhiteSpace(request.GhiChu) ? null : request.GhiChu.Trim();
        var daTra = await _context.DoBoQuens.Where(x => x.MaDo == maDo && (x.TrangThai == TrangThaiDoBoQuen.DA_TIM_THAY || x.TrangThai == TrangThaiDoBoQuen.DANG_LUU_GIU))
            .CapNhatCoDieuKienAsync(_context, u => u.SetProperty(x => x.TrangThai, TrangThaiDoBoQuen.DA_TRA_KHACH).SetProperty(x => x.ThoiGianTra, luc)
                .SetProperty(x => x.NguoiNhanLai, nguoiNhan).SetProperty(x => x.GiayToNguoiNhan, giayTo)
                .SetProperty(x => x.NguoiTra, tenDN).SetProperty(x => x.GhiChuTra, ghiChuTra),
                x => { x.TrangThai = TrangThaiDoBoQuen.DA_TRA_KHACH; x.ThoiGianTra = luc; x.NguoiNhanLai = nguoiNhan; x.GiayToNguoiNhan = giayTo; x.NguoiTra = tenDN; x.GhiChuTra = ghiChuTra; });
        if (daTra != 1) throw DaDong();
        _audit.Ghi("TRA_DO_BO_QUEN", $"DQ{maDo:0000}", $"nguoiNhan={nguoiNhan}; giayTo={giayTo}; moTa={Cat(doVat.MoTa, 60)}");
        await _context.SaveChangesAsync();
        return await GetAsync(maDo, tenDN);
    }

    public async Task<LostFoundDto> DisposeAsync(int maDo, DisposeLostFoundRequest request, string tenDN)
    {
        var doVat = await TimDangMoAsync(maDo);
        var lyDo = request.LyDo.Trim();
        if (lyDo.Length == 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_LY_DO", "Cần nhập lý do thanh lý.", "lyDo");
        if (doVat.TrangThai != TrangThaiDoBoQuen.DANG_LUU_GIU)
            throw new ApiException(StatusCodes.Status409Conflict, "CHUA_LUU_KHO", "Đồ chưa được lưu kho (chưa có vị trí lưu giữ) nên chưa thanh lý được.");
        var soNgay = SoNgayLuuGiu(doVat);
        if (soNgay < SoNgayLuuGiuToiThieu)
            throw new ApiException(StatusCodes.Status422UnprocessableEntity, "CHUA_DEN_HAN_THANH_LY",
                $"Đồ mới lưu giữ {soNgay} ngày; phải giữ tối thiểu {SoNgayLuuGiuToiThieu} ngày mới được thanh lý (để khách kịp nhận lại).");

        var lucThanhLy = DateTime.Now;
        var daThanhLy = await _context.DoBoQuens.Where(x => x.MaDo == maDo && x.TrangThai == TrangThaiDoBoQuen.DANG_LUU_GIU)
            .CapNhatCoDieuKienAsync(_context, u => u.SetProperty(x => x.TrangThai, TrangThaiDoBoQuen.DA_THANH_LY).SetProperty(x => x.ThoiGianThanhLy, lucThanhLy)
                .SetProperty(x => x.NguoiThanhLy, tenDN).SetProperty(x => x.LyDoThanhLy, lyDo),
                x => { x.TrangThai = TrangThaiDoBoQuen.DA_THANH_LY; x.ThoiGianThanhLy = lucThanhLy; x.NguoiThanhLy = tenDN; x.LyDoThanhLy = lyDo; });
        if (daThanhLy != 1) throw DaDong();
        _audit.Ghi("THANH_LY_DO_BO_QUEN", $"DQ{maDo:0000}", $"moTa={Cat(doVat.MoTa, 60)}; luuGiu={soNgay} ngay; lyDo={lyDo}");
        await _context.SaveChangesAsync();
        return await GetAsync(maDo, tenDN);
    }

    // ------------------------------------------------------------------ hình ảnh

    public async Task<LostFoundDto> AddImageAsync(int maDo, IFormFile? file, string tenDN)
    {
        var doVat = await TimDangMoAsync(maDo);
        if (file is null || file.Length == 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "ANH_KHONG_HOP_LE", "Chưa chọn file ảnh.", "file");
        if (file.Length > KichThuocAnhToiDa)
            throw new ApiException(StatusCodes.Status400BadRequest, "ANH_QUA_LON", $"Ảnh quá lớn (tối đa {KichThuocAnhToiDa / 1024 / 1024} MB).", "file");
        if (await _context.AnhDoBoQuens.CountAsync(x => x.MaDo == maDo) >= SoAnhToiDa)
            throw new ApiException(StatusCodes.Status409Conflict, "QUA_SO_ANH", $"Mỗi món đồ tối đa {SoAnhToiDa} ảnh.");

        string duoi;
        await using (var luong = file.OpenReadStream())
        {
            var dau = new byte[12];
            var doc = await luong.ReadAsync(dau.AsMemory(0, dau.Length));
            duoi = RoomImageService.NhanDangDuoi(dau, doc)
                ?? throw new ApiException(StatusCodes.Status400BadRequest, "ANH_KHONG_HOP_LE", "File không phải ảnh JPG, PNG hoặc WEBP hợp lệ.", "file");
        }

        Directory.CreateDirectory(_root);
        var tenFile = $"{maDo}-{Guid.NewGuid():N}.{duoi}";
        var dich = Path.Combine(_root, tenFile);
        await using (var ra = File.Create(dich))
        await using (var vao = file.OpenReadStream())
            await vao.CopyToAsync(ra);

        try
        {
            _context.AnhDoBoQuens.Add(new AnhDoBoQuen { MaDo = maDo, TenFile = tenFile, KichThuocByte = file.Length, ThoiGian = DateTime.Now });
            _audit.Ghi("THEM_ANH_DO_BO_QUEN", $"DQ{maDo:0000}", $"file={tenFile}; kichThuoc={file.Length}");
            await _context.SaveChangesAsync();
        }
        catch
        {
            if (File.Exists(dich)) File.Delete(dich);
            throw;
        }
        return await GetAsync(maDo, tenDN);
    }

    public async Task<(string DuongDan, string ContentType)> GetImageAsync(int maDo, int maAnh)
    {
        var anh = await _context.AnhDoBoQuens.AsNoTracking().FirstOrDefaultAsync(x => x.MaAnh == maAnh && x.MaDo == maDo)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_ANH", "Không tìm thấy ảnh.");
        var duongDan = Path.Combine(_root, Path.GetFileName(anh.TenFile));
        if (!File.Exists(duongDan))
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_ANH", "File ảnh không còn trên máy chủ.");
        var loai = Path.GetExtension(anh.TenFile).TrimStart('.').ToLowerInvariant() switch { "png" => "image/png", "webp" => "image/webp", _ => "image/jpeg" };
        return (duongDan, loai);
    }

    public async Task<LostFoundDto> DeleteImageAsync(int maDo, int maAnh, string tenDN)
    {
        await TimDangMoAsync(maDo);
        var anh = await _context.AnhDoBoQuens.FirstOrDefaultAsync(x => x.MaAnh == maAnh && x.MaDo == maDo)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_ANH", "Không tìm thấy ảnh.");
        _context.AnhDoBoQuens.Remove(anh);
        _audit.Ghi("XOA_ANH_DO_BO_QUEN", $"DQ{maDo:0000}", $"file={anh.TenFile}");
        await _context.SaveChangesAsync();
        var duongDan = Path.Combine(_root, Path.GetFileName(anh.TenFile));
        if (File.Exists(duongDan)) File.Delete(duongDan);
        return await GetAsync(maDo, tenDN);
    }

    // ------------------------------------------------------------------ nội bộ

    private const string LoiLoai = "Loại đồ không hợp lệ (GIAY_TO, DIEN_TU, VI_TIEN, TRANG_SUC, QUAN_AO, Y_TE, KHAC).";

    private static string Cat(string s, int n) => s.Length <= n ? s : s[..n];

    private static int SoNgayLuuGiu(DoBoQuen x) => Math.Max(0, (int)(DateTime.Now - x.ThoiGianPhatHien).TotalDays);

    private async Task<bool> CoXemKhachAsync(string tenDN) => await _permissions.CoQuyenAsync(tenDN, Permissions.XemKhachHang);

    private async Task<string> HoTenAsync(string tenDN)
        => (await TenNhanVien.TraCuuAsync(_context, new[] { tenDN })).GetValueOrDefault(tenDN) ?? tenDN;

    private static ApiException DaDong() => new(StatusCodes.Status409Conflict, "DO_BO_QUEN_DA_DONG", "Món đồ này vừa được người khác trả khách, thanh lý hoặc thay đổi. Hãy tải lại danh sách.");

    /// <summary>Đồ chưa trả/thanh lý mới sửa được; đã đóng thì 409.</summary>
    private async Task<DoBoQuen> TimDangMoAsync(int maDo)
    {
        var doVat = await _context.DoBoQuens.FirstOrDefaultAsync(x => x.MaDo == maDo)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_DO_BO_QUEN", "Không tìm thấy đồ bỏ quên.");
        if (doVat.TrangThai is TrangThaiDoBoQuen.DA_TRA_KHACH or TrangThaiDoBoQuen.DA_THANH_LY)
            throw new ApiException(StatusCodes.Status409Conflict, "DO_BO_QUEN_DA_DONG", "Món đồ này đã trả khách hoặc đã thanh lý, không thay đổi được nữa.");
        return doVat;
    }

    private async Task KiemTraViTriAsync(string? maPhong, string? khuVuc)
    {
        if (maPhong is null && khuVuc is null)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_VI_TRI_PHAT_HIEN", "Cần chọn phòng phát hiện hoặc nhập khu vực (sảnh, nhà hàng...).", "maPhong");
        if (maPhong is not null && !await _context.Phongs.AnyAsync(x => x.MaPhong == maPhong))
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_PHONG", "Không tìm thấy phòng.", "maPhong");
    }

    /// <summary>Lượt lưu trú gần nhất của phòng tính đến lúc phát hiện (đang ở hoặc vừa trả) - gợi ý khách để lễ tân liên hệ.</summary>
    private async Task<string?> LuotOGanNhatAsync(string? maPhong, DateTime luc)
    {
        if (maPhong is null) return null;
        var ngay = DateOnly.FromDateTime(luc);
        return await _context.ChiTietPhieuNhans.AsNoTracking()
            .Where(x => x.MaPhong == maPhong && x.NgayNhan <= ngay)
            .OrderByDescending(x => x.NgayNhan).ThenByDescending(x => x.MaPhieuNhan)
            .Select(x => x.MaPhieuNhan).FirstOrDefaultAsync();
    }

    private async Task<List<LostFoundDto>> ToDtosAsync(List<DoBoQuen> dong, bool coXemKhach)
    {
        var ten = await TenNhanVien.TraCuuAsync(_context, dong.SelectMany(x => new[] { x.NguoiGhiNhan, x.NguoiTra, x.NguoiThanhLy }));
        var khach = new Dictionary<string, (string HoTen, string SoDT)>();
        if (coXemKhach)
        {
            var maPhieus = dong.Where(x => x.MaPhieuNhan != null).Select(x => x.MaPhieuNhan!).Distinct().ToList();
            if (maPhieus.Count > 0)
                khach = await _context.PhieuNhanPhongs.AsNoTracking().Where(p => maPhieus.Contains(p.MaPhieuNhan) && p.Khach != null)
                    .Select(p => new { p.MaPhieuNhan, p.Khach!.HoTen, p.Khach.SoDT }).ToDictionaryAsync(p => p.MaPhieuNhan, p => (p.HoTen, p.SoDT));
        }

        return dong.Select(x =>
        {
            var soNgay = x.TrangThai is TrangThaiDoBoQuen.DA_TRA_KHACH or TrangThaiDoBoQuen.DA_THANH_LY
                ? Math.Max(0, (int)(((x.ThoiGianTra ?? x.ThoiGianThanhLy) ?? DateTime.Now) - x.ThoiGianPhatHien).TotalDays)
                : SoNgayLuuGiu(x);
            var coKhach = coXemKhach && x.MaPhieuNhan is not null && khach.ContainsKey(x.MaPhieuNhan);
            return new LostFoundDto
            {
                MaDo = x.MaDo, MaPhong = x.MaPhong, KhuVuc = x.KhuVuc, ThoiGianPhatHien = x.ThoiGianPhatHien, Loai = x.Loai.ToString(), MoTa = x.MoTa,
                NguoiNhat = x.NguoiNhat, NguoiGhiNhan = x.NguoiGhiNhan, ViTriLuuGiu = x.ViTriLuuGiu, TrangThai = x.TrangThai.ToString(),
                SoNgayLuuGiu = soNgay,
                DuDieuKienThanhLy = x.TrangThai == TrangThaiDoBoQuen.DANG_LUU_GIU && soNgay >= SoNgayLuuGiuToiThieu,
                ThoiGianTra = x.ThoiGianTra, NguoiNhanLai = x.NguoiNhanLai, GiayToNguoiNhan = x.GiayToNguoiNhan,
                NguoiTra = x.NguoiTra is null ? null : ten.GetValueOrDefault(x.NguoiTra) ?? x.NguoiTra, GhiChuTra = x.GhiChuTra,
                ThoiGianThanhLy = x.ThoiGianThanhLy, NguoiThanhLy = x.NguoiThanhLy is null ? null : ten.GetValueOrDefault(x.NguoiThanhLy) ?? x.NguoiThanhLy, LyDoThanhLy = x.LyDoThanhLy,
                MaPhieuNhan = coKhach ? x.MaPhieuNhan : null,
                HoTenKhach = coKhach ? khach[x.MaPhieuNhan!].HoTen : null,
                SoDTKhach = coKhach ? khach[x.MaPhieuNhan!].SoDT : null,
                Anhs = x.Anhs.OrderBy(a => a.MaAnh).Select(a => new LostFoundImageDto { MaAnh = a.MaAnh, TenFile = a.TenFile, KichThuocByte = a.KichThuocByte, ThoiGian = a.ThoiGian }).ToList()
            };
        }).ToList();
    }
}
