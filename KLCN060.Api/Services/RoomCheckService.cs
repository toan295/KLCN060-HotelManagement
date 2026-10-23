using KLCN060.Api.Auditing;
using KLCN060.Api.DTOs.Housekeeping;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public interface IRoomCheckService
{
    Task<RoomCheckTemplateDto> GetTemplateAsync(string maPhong);
    Task<RoomCheckDto> CreateAsync(CreateRoomCheckRequest request, string tenDN);
    Task<List<RoomCheckDto>> GetAllAsync(string? maPhong, DateOnly? tuNgay, DateOnly? denNgay, string? nguoiKiem);
    Task<RoomCheckDto> GetAsync(int maPhieuKiem);
    Task DeleteAsync(int maPhieuKiem, string lyDo, string tenDN);
    Task<List<MinibarChargeDto>> GetChargesAsync(string? trangThai, string? maPhong);
    Task<MinibarChargeDto> ChargeAsync(int maChiTiet, string tenDN);
    Task<MinibarChargeDto> WaiveAsync(int maChiTiet, string lyDo, string tenDN);
    Task<List<SupplyLossRowDto>> GetLossReportAsync(DateOnly? tuNgay, DateOnly? denNgay);
}

/// <summary>
/// Kiểm đồ dùng / minibar của phòng: nhân viên nhập số lượng trước → sau (VD Coca-Cola 2 → 1); phần chênh lệch là hao hụt (tiêu thụ / mất thiếu / hư hỏng).
/// Tiêu thụ của đồ dùng gắn dịch vụ (minibar, nước) trong phòng đang có khách thành khoản "chờ tính phí" gửi cho lễ tân; lễ tân tính phí (ghi dịch vụ vào lượt lưu trú)
/// hoặc bỏ qua kèm lý do, và chưa xử lý hết thì chưa trả phòng được. Số lượng "trước" gợi ý theo kết quả kiểm trước trong cùng lượt ở nên không tính phí trùng.
/// </summary>
public class RoomCheckService : IRoomCheckService
{
    private const int SoLuongToiDa = 1000;
    private const int SoDongToiDa = 200;
    private const int PhutChoPhepHuy = 60;
    private const int SoNgayBaoCaoMacDinh = 30;
    private const int SoNgayBaoCaoToiDa = 366;

    private readonly KLCN060DbContext _context;
    private readonly IAuditTrail _audit;
    private readonly IPermissionService _permissions;

    public RoomCheckService(KLCN060DbContext context, IAuditTrail audit, IPermissionService permissions)
    {
        _context = context;
        _audit = audit;
        _permissions = permissions;
    }

    // ------------------------------------------------------------------ gợi ý số lượng

    public async Task<RoomCheckTemplateDto> GetTemplateAsync(string maPhong)
    {
        var phong = await _context.Phongs.AsNoTracking().FirstOrDefaultAsync(x => x.MaPhong == maPhong)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_PHONG", "Không tìm thấy phòng.");
        var maPhieuNhan = await LayLuotDangOAsync(maPhong);

        var loaiPhong = await _context.LoaiPhongs.AsNoTracking().FirstAsync(x => x.MaLoai == phong.MaLoai);
        // Chỉ đồ dùng áp dụng cho loại phòng này (áo choàng tắm chỉ có ở Deluxe...) và định mức thực tế theo số giường / số người của loại phòng.
        var vatDungs = (await _context.VatDungs.AsNoTracking().Include(x => x.LoaiPhongApDungs).Where(x => x.HoatDong)
            .Select(x => new { VatDung = x, Gia = x.DichVu != null ? (decimal?)x.DichVu.GiaDV : null }).ToListAsync())
            .Where(x => x.VatDung.ApDungChoLoai(phong.MaLoai)).ToList();
        // Mỗi đồ dùng lấy kết quả của lần kiểm GẦN NHẤT có kiểm đồ đó trong cùng lượt ở (một phiếu không nhất thiết kiểm đủ mọi đồ).
        var cacLan = await _context.PhieuKiemPhongs.AsNoTracking().Include(x => x.ChiTiets)
            .Where(x => x.MaPhong == maPhong && x.MaPhieuNhan == maPhieuNhan)
            .OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.MaPhieuKiem).ToListAsync();
        var lanCuoi = cacLan.FirstOrDefault();
        var conLai = new Dictionary<string, int>();
        foreach (var lan in cacLan)
            foreach (var ct in lan.ChiTiets)
                conLai.TryAdd(ct.MaVatDung, ct.SoLuongSau + ct.SoLuongBoSung);

        return new RoomCheckTemplateDto
        {
            MaPhong = maPhong, TinhTrangPhong = phong.TinhTrang.ToString(), MaPhieuNhan = maPhieuNhan,
            KiemGanNhatLuc = lanCuoi?.ThoiGian,
            KetQuaKiemGanNhat = lanCuoi is null ? null : $"PK{lanCuoi.MaPhieuKiem:00000}",
            Dong = vatDungs.OrderBy(x => x.VatDung.MaVatDung.Length).ThenBy(x => x.VatDung.MaVatDung, StringComparer.Ordinal).Select(x => new RoomCheckTemplateLineDto
            {
                MaVatDung = x.VatDung.MaVatDung, TenVatDung = x.VatDung.TenVatDung, Nhom = x.VatDung.Nhom.ToString(), DonViTinh = x.VatDung.DonViTinh,
                SoLuongChuan = x.VatDung.DinhMucCho(loaiPhong),
                SoLuongTruoc = conLai.TryGetValue(x.VatDung.MaVatDung, out var c) ? c : x.VatDung.DinhMucCho(loaiPhong),
                TinhPhi = x.VatDung.MaDV != null, GiaTinhPhi = x.Gia
            }).ToList()
        };
    }

    // ------------------------------------------------------------------ ghi nhận

    public async Task<RoomCheckDto> CreateAsync(CreateRoomCheckRequest request, string tenDN)
    {
        // Kiểm đồ dùng/minibar là việc tại phòng của buồng phòng; Quản lý chỉ giám sát (xem báo cáo hao hụt, bỏ qua khoản minibar có lý do).
        if (await _permissions.CoQuyenAsync(tenDN, Permissions.DuyetNghiepVu))
            throw new ApiException(StatusCodes.Status403Forbidden, "QUAN_LY_KHONG_KIEM_PHONG",
                "Kiểm đồ dùng/minibar trong phòng là việc của buồng phòng. Quản lý xem kết quả ở báo cáo và nhật ký buồng phòng.");
        var maPhong = request.MaPhong.Trim();
        if (!await _context.Phongs.AnyAsync(x => x.MaPhong == maPhong))
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_PHONG", "Không tìm thấy phòng.", "maPhong");
        if (request.Dong.Count == 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_DONG_KIEM", "Chưa có dòng đồ dùng nào để ghi nhận.", "dong");
        if (request.Dong.Count > SoDongToiDa)
            throw new ApiException(StatusCodes.Status400BadRequest, "QUA_NHIEU_DONG", $"Một phiếu kiểm tối đa {SoDongToiDa} dòng.", "dong");
        var trung = request.Dong.GroupBy(x => x.MaVatDung.Trim(), StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (trung is not null)
            throw new ApiException(StatusCodes.Status400BadRequest, "VAT_DUNG_TRUNG", $"Đồ dùng {trung.Key} xuất hiện nhiều lần trong một phiếu kiểm.", "dong");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        // Hai người cùng kiểm một phòng: xử lý lần lượt để số lượng "trước" của người sau là kết quả của người trước (không tính phí trùng).
        await MaCodeGenerator.KhoaTienToAsync(_context, $"KIEMPHONG-{maPhong}");

        var maPhieuNhan = await LayLuotDangOAsync(maPhong);
        var mau = (await GetTemplateAsync(maPhong)).Dong.ToDictionary(x => x.MaVatDung, StringComparer.OrdinalIgnoreCase);
        var maVatDungs = request.Dong.Select(x => x.MaVatDung.Trim()).ToList();
        var vatDungs = await _context.VatDungs.Include(x => x.DichVu).Where(x => maVatDungs.Contains(x.MaVatDung)).ToDictionaryAsync(x => x.MaVatDung, StringComparer.OrdinalIgnoreCase);

        var phieu = new PhieuKiemPhong { MaPhong = maPhong, MaPhieuNhan = maPhieuNhan, NguoiKiem = tenDN, ThoiGian = DateTime.Now, GhiChu = string.IsNullOrWhiteSpace(request.GhiChu) ? null : request.GhiChu.Trim() };
        decimal tongChoTinhPhi = 0;
        foreach (var d in request.Dong)
        {
            var ma = d.MaVatDung.Trim();
            if (!vatDungs.TryGetValue(ma, out var vatDung))
                throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_VAT_DUNG", $"Không tìm thấy đồ dùng {ma}.", "dong");
            if (!vatDung.HoatDong)
                throw new ApiException(StatusCodes.Status409Conflict, "VAT_DUNG_NGUNG_SU_DUNG", $"Đồ dùng \"{vatDung.TenVatDung}\" đã ngừng sử dụng, không kiểm nữa.", "dong");

            if (!mau.TryGetValue(vatDung.MaVatDung, out var dongMau))
                throw new ApiException(StatusCodes.Status409Conflict, "VAT_DUNG_KHONG_AP_DUNG_CHO_PHONG",
                    $"Đồ dùng \"{vatDung.TenVatDung}\" không áp dụng cho loại phòng của phòng {maPhong}, không kiểm ở phòng này.", "dong");
            var truoc = d.SoLuongTruoc ?? dongMau.SoLuongTruoc;
            if (truoc < 0 || truoc > SoLuongToiDa || d.SoLuongSau < 0 || d.SoLuongSau > SoLuongToiDa || d.SoLuongBoSung < 0 || d.SoLuongBoSung > SoLuongToiDa)
                throw new ApiException(StatusCodes.Status400BadRequest, "SO_LUONG_KHONG_HOP_LE", $"Số lượng của \"{vatDung.TenVatDung}\" phải từ 0 đến {SoLuongToiDa}.", "dong");
            if (d.SoLuongSau > truoc)
                throw new ApiException(StatusCodes.Status422UnprocessableEntity, "SO_LUONG_SAU_LON_HON_TRUOC",
                    $"\"{vatDung.TenVatDung}\": số còn lại ({d.SoLuongSau}) không được lớn hơn số trước khi kiểm ({truoc}). Nếu đã bổ sung thêm đồ, hãy nhập ở cột \"bổ sung\".", "dong");

            var haoHut = truoc - d.SoLuongSau;
            LyDoHaoHut? lyDo = null;
            if (haoHut > 0)
                lyDo = string.IsNullOrWhiteSpace(d.LyDo)
                    ? MacDinhLyDo(vatDung.Nhom)
                    : EnumParser.Parse<LyDoHaoHut>(d.LyDo, "dong", "Lý do hao hụt không hợp lệ (TIEU_THU, MAT_THIEU, HU_HONG).");
            if (lyDo == LyDoHaoHut.TIEU_THU && vatDung.Nhom == NhomVatDung.KHAN_GA_GOI)
                throw new ApiException(StatusCodes.Status422UnprocessableEntity, "LY_DO_KHONG_HOP_LE", $"\"{vatDung.TenVatDung}\" là khăn/ga/gối: hao hụt chỉ là mất/thiếu hoặc hư hỏng, không phải tiêu thụ.", "dong");

            var tinhPhi = haoHut > 0 && lyDo == LyDoHaoHut.TIEU_THU && vatDung.DichVu is not null;
            var soTinhPhi = tinhPhi ? haoHut : 0;
            var thanhTien = tinhPhi ? vatDung.DichVu!.GiaDV * haoHut : 0m;
            // Chỉ tính phí được khi phòng đang có khách; phòng trống thì chỉ ghi nhận hao hụt.
            var trangThai = tinhPhi && maPhieuNhan is not null ? TrangThaiTinhPhi.CHO_TINH_PHI : TrangThaiTinhPhi.KHONG_TINH_PHI;
            if (trangThai == TrangThaiTinhPhi.CHO_TINH_PHI) tongChoTinhPhi += thanhTien;

            phieu.ChiTiets.Add(new ChiTietKiemPhong
            {
                MaVatDung = vatDung.MaVatDung, SoLuongTruoc = truoc, SoLuongSau = d.SoLuongSau, SoLuongBoSung = d.SoLuongBoSung, LyDo = lyDo,
                GhiChu = string.IsNullOrWhiteSpace(d.GhiChu) ? null : d.GhiChu.Trim(),
                SoLuongTinhPhi = soTinhPhi, ThanhTien = thanhTien, TrangThaiTinhPhi = trangThai
            });
        }

        _context.PhieuKiemPhongs.Add(phieu);
        var haoHutTong = phieu.ChiTiets.Count(x => x.SoLuongTruoc > x.SoLuongSau);
        _audit.Ghi("KIEM_PHONG", maPhong, $"phieuNhan={maPhieuNhan ?? "khong co khach"}; soDong={phieu.ChiTiets.Count}; soDongHaoHut={haoHutTong}; choTinhPhi={Math.Round(tongChoTinhPhi, 0)}");
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return await GetAsync(phieu.MaPhieuKiem);
    }

    // ------------------------------------------------------------------ xem

    public async Task<List<RoomCheckDto>> GetAllAsync(string? maPhong, DateOnly? tuNgay, DateOnly? denNgay, string? nguoiKiem)
    {
        var q = _context.PhieuKiemPhongs.AsNoTracking().Include(x => x.ChiTiets).ThenInclude(c => c.VatDung).AsQueryable();
        if (!string.IsNullOrWhiteSpace(maPhong)) q = q.Where(x => x.MaPhong == maPhong.Trim());
        if (!string.IsNullOrWhiteSpace(nguoiKiem)) q = q.Where(x => x.NguoiKiem == nguoiKiem.Trim());
        if (tuNgay is not null) { var tu = tuNgay.Value.ToDateTime(TimeOnly.MinValue); q = q.Where(x => x.ThoiGian >= tu); }
        if (denNgay is not null) { var den = denNgay.Value.AddDays(1).ToDateTime(TimeOnly.MinValue); q = q.Where(x => x.ThoiGian < den); }
        var dong = await q.OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.MaPhieuKiem).Take(SoDongToiDa).ToListAsync();
        return await ToDtosAsync(dong);
    }

    public async Task<RoomCheckDto> GetAsync(int maPhieuKiem)
    {
        var phieu = await _context.PhieuKiemPhongs.AsNoTracking().Include(x => x.ChiTiets).ThenInclude(c => c.VatDung)
            .FirstOrDefaultAsync(x => x.MaPhieuKiem == maPhieuKiem)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_PHIEU_KIEM", "Không tìm thấy phiếu kiểm.");
        return (await ToDtosAsync(new List<PhieuKiemPhong> { phieu }))[0];
    }

    /// <summary>
    /// Hủy phiếu kiểm nhập nhầm: người nhập trong vòng 60 phút, hoặc quản lý; bắt buộc lý do và có nhật ký.
    /// Không hủy được khi phiếu đã có khoản được xử lý (đã tính phí cho khách hoặc đã bỏ qua kèm lý do) - đó là giao dịch đã phát sinh, không được xóa dấu vết.
    /// </summary>
    public async Task DeleteAsync(int maPhieuKiem, string lyDo, string tenDN)
    {
        if (string.IsNullOrWhiteSpace(lyDo))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_LY_DO", "Cần nhập lý do hủy phiếu kiểm (có ghi nhật ký).", "lyDo");

        var phieu = await _context.PhieuKiemPhongs.Include(x => x.ChiTiets).ThenInclude(c => c.VatDung).FirstOrDefaultAsync(x => x.MaPhieuKiem == maPhieuKiem)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_PHIEU_KIEM", "Không tìm thấy phiếu kiểm.");
        var quanLy = await _permissions.CoQuyenAsync(tenDN, Permissions.DuyetNghiepVu);
        var nguoiNhapConHan = phieu.NguoiKiem == tenDN && (DateTime.Now - phieu.ThoiGian).TotalMinutes <= PhutChoPhepHuy;
        if (!quanLy && !nguoiNhapConHan)
            throw new ApiException(StatusCodes.Status403Forbidden, "KHONG_DUOC_HUY_PHIEU_KIEM", $"Chỉ người nhập (trong {PhutChoPhepHuy} phút) hoặc quản lý mới hủy được phiếu kiểm.");
        if (phieu.ChiTiets.Any(x => x.TrangThaiTinhPhi == TrangThaiTinhPhi.DA_TINH_PHI))
            throw new ApiException(StatusCodes.Status409Conflict, "PHIEU_KIEM_DA_TINH_PHI", "Phiếu kiểm đã có khoản tính phí cho khách nên không hủy được (lễ tân hủy dòng dịch vụ trước).");
        if (phieu.ChiTiets.Any(x => x.TrangThaiTinhPhi == TrangThaiTinhPhi.BO_QUA))
            throw new ApiException(StatusCodes.Status409Conflict, "PHIEU_KIEM_DA_XU_LY", "Phiếu kiểm có khoản minibar đã được xử lý (bỏ qua kèm lý do) nên không hủy được: giao dịch đã phát sinh phải được giữ lại để truy vết.");

        var tomTat = string.Join(", ", phieu.ChiTiets.Where(x => x.SoLuongTruoc > x.SoLuongSau).Select(x => $"{x.VatDung.TenVatDung} {x.SoLuongTruoc}->{x.SoLuongSau}"));
        _context.PhieuKiemPhongs.Remove(phieu);
        _audit.Ghi("HUY_PHIEU_KIEM", phieu.MaPhong,
            $"phieuKiem=PK{phieu.MaPhieuKiem:00000}; nguoiKiem={phieu.NguoiKiem}; luc={phieu.ThoiGian:dd/MM/yyyy HH:mm}; soDong={phieu.ChiTiets.Count}; haoHut=[{(tomTat.Length == 0 ? "khong" : tomTat)}]; lyDo={lyDo.Trim()}");
        await _context.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ gửi lễ tân tính phí

    public async Task<List<MinibarChargeDto>> GetChargesAsync(string? trangThai, string? maPhong)
    {
        var tt = string.IsNullOrWhiteSpace(trangThai)
            ? TrangThaiTinhPhi.CHO_TINH_PHI
            : EnumParser.Parse<TrangThaiTinhPhi>(trangThai, "trangThai", "Trạng thái không hợp lệ (CHO_TINH_PHI, DA_TINH_PHI, BO_QUA).");
        if (tt == TrangThaiTinhPhi.KHONG_TINH_PHI)
            throw new ApiException(StatusCodes.Status400BadRequest, "GIA_TRI_KHONG_HOP_LE", "Trạng thái không hợp lệ (CHO_TINH_PHI, DA_TINH_PHI, BO_QUA).", "trangThai");

        var q = _context.ChiTietKiemPhongs.AsNoTracking().Where(x => x.TrangThaiTinhPhi == tt && x.PhieuKiemPhong.MaPhieuNhan != null);
        if (!string.IsNullOrWhiteSpace(maPhong)) q = q.Where(x => x.PhieuKiemPhong.MaPhong == maPhong.Trim());
        var dong = await q.OrderByDescending(x => x.PhieuKiemPhong.ThoiGian).ThenBy(x => x.MaChiTiet).Take(SoDongToiDa)
            .Select(x => new
            {
                Ct = x, x.VatDung.TenVatDung, x.VatDung.DonViTinh,
                x.PhieuKiemPhong.MaPhong, x.PhieuKiemPhong.MaPhieuNhan, x.PhieuKiemPhong.NguoiKiem, x.PhieuKiemPhong.ThoiGian,
                HoTenKhach = _context.PhieuNhanPhongs.Where(p => p.MaPhieuNhan == x.PhieuKiemPhong.MaPhieuNhan).Select(p => p.Khach != null ? p.Khach.HoTen : null).FirstOrDefault()
            }).ToListAsync();
        var ten = await TenNhanVien.TraCuuAsync(_context, dong.Select(x => x.NguoiKiem));

        return dong.Select(x => new MinibarChargeDto
        {
            MaChiTiet = x.Ct.MaChiTiet, MaPhieuKiem = x.Ct.MaPhieuKiem, MaPhong = x.MaPhong, MaPhieuNhan = x.MaPhieuNhan!, HoTenKhach = x.HoTenKhach,
            MaVatDung = x.Ct.MaVatDung, TenVatDung = x.TenVatDung, DonViTinh = x.DonViTinh,
            SoLuongTruoc = x.Ct.SoLuongTruoc, SoLuongSau = x.Ct.SoLuongSau, SoLuongTinhPhi = x.Ct.SoLuongTinhPhi, ThanhTien = x.Ct.ThanhTien,
            TrangThaiTinhPhi = x.Ct.TrangThaiTinhPhi.ToString(), NguoiKiem = x.NguoiKiem, HoTenNguoiKiem = ten.GetValueOrDefault(x.NguoiKiem), ThoiGianKiem = x.ThoiGian,
            NguoiXuLy = x.Ct.NguoiXuLy, ThoiGianXuLy = x.Ct.ThoiGianXuLy, LyDoBoQua = x.Ct.LyDoBoQua
        }).ToList();
    }

    public async Task<MinibarChargeDto> ChargeAsync(int maChiTiet, string tenDN)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        var ct = await _context.ChiTietKiemPhongs.Include(x => x.PhieuKiemPhong).Include(x => x.VatDung)
            .FirstOrDefaultAsync(x => x.MaChiTiet == maChiTiet)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_KHOAN_PHI", "Không tìm thấy khoản minibar.");
        var maPhieuNhan = ct.PhieuKiemPhong.MaPhieuNhan
            ?? throw new ApiException(StatusCodes.Status409Conflict, "KHOAN_PHI_KHONG_CO_KHACH", "Khoản này ghi nhận khi phòng không có khách nên không tính phí được.");

        // Khóa phiếu nhận như mọi thao tác ghi dịch vụ/trả phòng để dòng phí không lọt khỏi hóa đơn.
        var conPhieu = await PhieuNhanLock.KhoaAsync(_context, maPhieuNhan);
        if (!conPhieu)
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LUOT_LUU_TRU", "Không tìm thấy lượt lưu trú.");
        await _context.Entry(ct).ReloadAsync();
        if (ct.TrangThaiTinhPhi != TrangThaiTinhPhi.CHO_TINH_PHI)
            throw new ApiException(StatusCodes.Status409Conflict, "KHOAN_PHI_DA_XU_LY", "Khoản minibar này đã được xử lý (tính phí hoặc bỏ qua).");
        if (!await _context.ChiTietPhieuNhans.AnyAsync(x => x.MaPhieuNhan == maPhieuNhan && x.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O))
            throw new ApiException(StatusCodes.Status409Conflict, "LUOT_LUU_TRU_DA_KET_THUC", "Khách đã trả phòng, không tính thêm được. Hãy bỏ qua khoản này kèm lý do.");

        var dvMinibar = await _context.DichVus.AsNoTracking().FirstOrDefaultAsync(x => x.MaDV == ct.VatDung.MaDV);
        var dong = new ChiTietSuDungDV
        {
            MaPhieuNhan = maPhieuNhan, MaDV = ct.VatDung.MaDV!, SoLuong = ct.SoLuongTinhPhi, NgaySuDung = DateTime.Now, ThanhTien = ct.ThanhTien,
            TenDichVu = dvMinibar?.TenDV, DonViTinh = dvMinibar?.DonViTinh,
            // Người ghi = lễ tân bấm "Tính phí" (để màn Dịch vụ không còn dòng bỏ trống người ghi); phòng = phòng buồng phòng đã kiểm.
            NguoiGhi = tenDN, MaPhong = ct.PhieuKiemPhong.MaPhong
        };
        _context.ChiTietSuDungDVs.Add(dong);
        await _context.SaveChangesAsync();

        // Chuyển trạng thái có điều kiện (chỉ khi vẫn đang chờ): nếu một người khác vừa bỏ qua khoản này thì dòng dịch vụ vừa thêm bị hủy cùng giao dịch,
        // không để khoản vừa "bỏ qua" vừa "đã tính phí" (tính tiền khách hai lần / sai lệch).
        var bayGio = DateTime.Now;
        var maDong = dong.MaChiTietDV;
        var daCapNhat = await _context.ChiTietKiemPhongs.Where(x => x.MaChiTiet == maChiTiet && x.TrangThaiTinhPhi == TrangThaiTinhPhi.CHO_TINH_PHI)
            .CapNhatCoDieuKienAsync(_context, u => u.SetProperty(x => x.TrangThaiTinhPhi, TrangThaiTinhPhi.DA_TINH_PHI).SetProperty(x => x.MaChiTietDV, maDong)
                .SetProperty(x => x.NguoiXuLy, tenDN).SetProperty(x => x.ThoiGianXuLy, bayGio),
                x => { x.TrangThaiTinhPhi = TrangThaiTinhPhi.DA_TINH_PHI; x.MaChiTietDV = maDong; x.NguoiXuLy = tenDN; x.ThoiGianXuLy = bayGio; });
        if (daCapNhat != 1)
        {
            await transaction.RollbackAsync();
            throw new ApiException(StatusCodes.Status409Conflict, "KHOAN_PHI_DA_XU_LY", "Khoản minibar này vừa được người khác xử lý (tính phí hoặc bỏ qua). Không có khoản nào bị tính thêm.");
        }
        _audit.Ghi("TINH_PHI_MINIBAR", maPhieuNhan, $"phong={ct.PhieuKiemPhong.MaPhong}; vatDung={ct.VatDung.TenVatDung}; soLuong={ct.SoLuongTinhPhi}; thanhTien={Math.Round(ct.ThanhTien, 0)}; dongDichVu={dong.MaChiTietDV}");
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return await LayKhoanAsync(maChiTiet);
    }

    public async Task<MinibarChargeDto> WaiveAsync(int maChiTiet, string lyDo, string tenDN)
    {
        if (string.IsNullOrWhiteSpace(lyDo))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_LY_DO", "Cần nhập lý do bỏ qua khoản phí (ảnh hưởng số tiền của khách).", "lyDo");

        var ct = await _context.ChiTietKiemPhongs.Include(x => x.PhieuKiemPhong).Include(x => x.VatDung).FirstOrDefaultAsync(x => x.MaChiTiet == maChiTiet)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_KHOAN_PHI", "Không tìm thấy khoản minibar.");
        if (ct.TrangThaiTinhPhi != TrangThaiTinhPhi.CHO_TINH_PHI)
            throw new ApiException(StatusCodes.Status409Conflict, "KHOAN_PHI_DA_XU_LY", "Khoản minibar này đã được xử lý (tính phí hoặc bỏ qua).");

        var lyDoBoQua = lyDo.Trim();
        var bayGio = DateTime.Now;
        var daCapNhat = await _context.ChiTietKiemPhongs.Where(x => x.MaChiTiet == maChiTiet && x.TrangThaiTinhPhi == TrangThaiTinhPhi.CHO_TINH_PHI)
            .CapNhatCoDieuKienAsync(_context, u => u.SetProperty(x => x.TrangThaiTinhPhi, TrangThaiTinhPhi.BO_QUA).SetProperty(x => x.LyDoBoQua, lyDoBoQua)
                .SetProperty(x => x.NguoiXuLy, tenDN).SetProperty(x => x.ThoiGianXuLy, bayGio),
                x => { x.TrangThaiTinhPhi = TrangThaiTinhPhi.BO_QUA; x.LyDoBoQua = lyDoBoQua; x.NguoiXuLy = tenDN; x.ThoiGianXuLy = bayGio; });
        if (daCapNhat != 1)
            throw new ApiException(StatusCodes.Status409Conflict, "KHOAN_PHI_DA_XU_LY", "Khoản minibar này vừa được người khác xử lý (tính phí hoặc bỏ qua).");
        _audit.Ghi("BO_QUA_MINIBAR", ct.PhieuKiemPhong.MaPhieuNhan, $"phong={ct.PhieuKiemPhong.MaPhong}; vatDung={ct.VatDung.TenVatDung}; soLuong={ct.SoLuongTinhPhi}; thanhTien={Math.Round(ct.ThanhTien, 0)}; lyDo={lyDoBoQua}");
        await _context.SaveChangesAsync();
        return await LayKhoanAsync(maChiTiet);
    }

    // ------------------------------------------------------------------ báo cáo hao hụt

    public async Task<List<SupplyLossRowDto>> GetLossReportAsync(DateOnly? tuNgay, DateOnly? denNgay)
    {
        var den = denNgay ?? DateOnly.FromDateTime(DateTime.Now);
        var tu = tuNgay ?? den.AddDays(-(SoNgayBaoCaoMacDinh - 1));
        if (tu > den)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_KHONG_HOP_LE", "Từ ngày phải trước hoặc bằng đến ngày.", "tuNgay");
        if (den.DayNumber - tu.DayNumber + 1 > SoNgayBaoCaoToiDa)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_QUA_DAI", $"Chỉ xem tối đa {SoNgayBaoCaoToiDa} ngày một lần.", "tuNgay");

        var tuLuc = tu.ToDateTime(TimeOnly.MinValue);
        var denLuc = den.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var dong = await _context.ChiTietKiemPhongs.AsNoTracking()
            .Where(x => x.PhieuKiemPhong.ThoiGian >= tuLuc && x.PhieuKiemPhong.ThoiGian < denLuc)
            .Select(x => new { x.MaVatDung, x.VatDung.TenVatDung, x.VatDung.DonViTinh, x.SoLuongTruoc, x.SoLuongSau, x.LyDo }).ToListAsync();

        return dong.GroupBy(x => new { x.MaVatDung, x.TenVatDung, x.DonViTinh }).Select(g => new SupplyLossRowDto
        {
            MaVatDung = g.Key.MaVatDung, TenVatDung = g.Key.TenVatDung, DonViTinh = g.Key.DonViTinh, SoLanKiem = g.Count(),
            TieuThu = g.Where(x => x.LyDo == LyDoHaoHut.TIEU_THU).Sum(x => x.SoLuongTruoc - x.SoLuongSau),
            MatThieu = g.Where(x => x.LyDo == LyDoHaoHut.MAT_THIEU).Sum(x => x.SoLuongTruoc - x.SoLuongSau),
            HuHong = g.Where(x => x.LyDo == LyDoHaoHut.HU_HONG).Sum(x => x.SoLuongTruoc - x.SoLuongSau)
        }).OrderByDescending(x => x.MatThieu + x.HuHong).ThenByDescending(x => x.TieuThu).ThenBy(x => x.TenVatDung, StringComparer.CurrentCulture).ToList();
    }

    // ------------------------------------------------------------------ nội bộ

    private async Task<string?> LayLuotDangOAsync(string maPhong)
        => await _context.ChiTietPhieuNhans.AsNoTracking().Where(x => x.MaPhong == maPhong && x.TrangThai == TrangThaiChiTietPhieuNhan.DANG_O)
            .Select(x => x.MaPhieuNhan).FirstOrDefaultAsync();

    private static LyDoHaoHut MacDinhLyDo(NhomVatDung nhom) => nhom switch
    {
        NhomVatDung.MINIBAR or NhomVatDung.NUOC_UONG or NhomVatDung.AMENITIES => LyDoHaoHut.TIEU_THU,
        NhomVatDung.KHAN_GA_GOI => LyDoHaoHut.MAT_THIEU,
        _ => LyDoHaoHut.HU_HONG
    };

    private async Task<MinibarChargeDto> LayKhoanAsync(int maChiTiet)
    {
        foreach (var tt in new[] { TrangThaiTinhPhi.CHO_TINH_PHI, TrangThaiTinhPhi.DA_TINH_PHI, TrangThaiTinhPhi.BO_QUA })
        {
            var kq = (await GetChargesAsync(tt.ToString(), null)).FirstOrDefault(x => x.MaChiTiet == maChiTiet);
            if (kq is not null) return kq;
        }
        throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_KHOAN_PHI", "Không tìm thấy khoản minibar.");
    }

    private async Task<List<RoomCheckDto>> ToDtosAsync(List<PhieuKiemPhong> phieus)
    {
        var ten = await TenNhanVien.TraCuuAsync(_context, phieus.Select(x => x.NguoiKiem));
        return phieus.Select(p => new RoomCheckDto
        {
            MaPhieuKiem = p.MaPhieuKiem, MaPhong = p.MaPhong, MaPhieuNhan = p.MaPhieuNhan, NguoiKiem = p.NguoiKiem,
            HoTenNguoiKiem = ten.GetValueOrDefault(p.NguoiKiem), ThoiGian = p.ThoiGian, GhiChu = p.GhiChu,
            Dong = p.ChiTiets.OrderBy(c => c.MaChiTiet).Select(c => new RoomCheckLineDto
            {
                MaChiTiet = c.MaChiTiet, MaVatDung = c.MaVatDung, TenVatDung = c.VatDung.TenVatDung, Nhom = c.VatDung.Nhom.ToString(), DonViTinh = c.VatDung.DonViTinh,
                SoLuongTruoc = c.SoLuongTruoc, SoLuongSau = c.SoLuongSau, SoLuongBoSung = c.SoLuongBoSung, HaoHut = c.SoLuongTruoc - c.SoLuongSau,
                LyDo = c.LyDo?.ToString(), GhiChu = c.GhiChu, SoLuongTinhPhi = c.SoLuongTinhPhi, ThanhTien = c.ThanhTien,
                TrangThaiTinhPhi = c.TrangThaiTinhPhi.ToString(), LyDoBoQua = c.LyDoBoQua
            }).ToList()
        }).ToList();
    }
}
