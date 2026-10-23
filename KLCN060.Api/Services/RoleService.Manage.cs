using System.Security.Cryptography;
using KLCN060.Api.DTOs.Roles;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public partial class RoleService
{
    private static readonly string[] HanhDongPhanQuyen =
    {
        "TAO_VAI_TRO", "CAP_NHAT_PHAN_QUYEN", "TAO_NHAN_VIEN", "CAP_NHAT_NHAN_VIEN", "DOI_VAI_TRO_NHAN_VIEN",
        "KHOA_TAI_KHOAN_NHAN_VIEN", "MO_KHOA_TAI_KHOAN_NHAN_VIEN", "DAT_LAI_MAT_KHAU_NHAN_VIEN", "TAI_KHOAN_BI_KHOA_TU_DONG"
    };

    private static PermissionDto ToPermissionDto(int maQuyen, string tenQuyen, string? nhom)
    {
        var m = PermissionCatalog.Cua(tenQuyen);
        return new PermissionDto
        {
            MaQuyen = maQuyen, TenQuyen = tenQuyen, NhomChucNang = nhom, MoTa = Permissions.MoTaCua(tenQuyen),
            TenHienThi = m.TenHienThi, NhomHienThi = m.NhomHienThi, MucNhayCam = m.MucNhayCam
        };
    }

    /// <summary>Cảnh báo tách bạch trách nhiệm và cấp thừa quyền cho một tập quyền của vai trò.</summary>
    internal static List<string> PhanTichCanhBao(string tenVaiTro, ISet<string> quyen)
    {
        var ds = new List<string>();
        bool Co(string q) => quyen.Contains(q);
        if (Co(Permissions.DuyetNghiepVu) && (Co(Permissions.ThanhToan) || Co(Permissions.ThaoTacLeTan)))
            ds.Add("Vừa thu tiền / thao tác lễ tân vừa có quyền Quản lý nghiệp vụ (hủy phiếu, sửa phiếu đặc biệt): người thao tác tự quyết định ngoại lệ của chính mình - nên tách thành hai vai trò.");
        if (Co(Permissions.QuanTriHeThong) && tenVaiTro != "QUAN_LY")
            ds.Add("Quyền quản trị hệ thống (phân quyền, tài khoản, sao lưu) đang được gán cho vai trò không phải Quản lý.");
        if (Co(Permissions.XuLyDoiSoat) && Co(Permissions.ThaoTacLeTan))
            ds.Add("Quyền xử lý đối soát đi cùng thao tác lễ tân: người nhận tiền có thể tự xử lý chênh lệch đối soát.");
        return ds;
    }

    private static void BoSungPhanTich(RoleDto dto)
    {
        dto.LaVaiTroHeThong = VaiTroHeThong.Contains(dto.TenVaiTro);
        var ten = dto.Quyen.Select(q => q.TenQuyen).ToHashSet();
        dto.SoQuyenNhayCam = dto.Quyen.Count(q => q.MucNhayCam != PermissionCatalog.Thuong);
        dto.CanhBao = PhanTichCanhBao(dto.TenVaiTro, ten);
        if (Permissions.MacDinhTheoVaiTro.TryGetValue(dto.TenVaiTro, out var macDinh))
        {
            var md = macDinh.ToHashSet();
            dto.CapThemSoVoiMacDinh = ten.Except(md).Select(PermissionCatalog.TenHienThi).OrderBy(x => x).ToList();
            dto.ThieuSoVoiMacDinh = md.Except(ten).Select(PermissionCatalog.TenHienThi).OrderBy(x => x).ToList();
            if (dto.CapThemSoVoiMacDinh.Count > 0) dto.CanhBao.Add($"Có {dto.CapThemSoVoiMacDinh.Count} quyền ngoài bộ quyền chuẩn của vai trò {dto.TenVaiTro}: {string.Join("; ", dto.CapThemSoVoiMacDinh)}.");
            if (dto.ThieuSoVoiMacDinh.Count > 0) dto.CanhBao.Add($"Thiếu {dto.ThieuSoVoiMacDinh.Count} quyền so với bộ quyền chuẩn của vai trò {dto.TenVaiTro}: {string.Join("; ", dto.ThieuSoVoiMacDinh)}.");
        }
    }

    /// <summary>Xem trước tác động của việc đổi tập quyền của vai trò (chưa lưu gì).</summary>
    public async Task<RoleChangePreviewDto> PreviewAsync(int maVaiTro, RoleRequest request)
    {
        var vaiTro = await _context.VaiTros.AsNoTracking().Include(x => x.VaiTroQuyens).ThenInclude(vq => vq.Quyen).Include(x => x.NhanViens.Where(n => n.TaiKhoan == null || n.TaiKhoan.TrangThai != Domain.Enums.TrangThaiTaiKhoan.DA_XOA))
            .FirstOrDefaultAsync(x => x.MaVaiTro == maVaiTro)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_VAI_TRO", "Không tìm thấy vai trò.");
        var moi = (await KiemTraQuyenAsync(request.MaQuyen)).ToHashSet();
        var tatCa = await _context.Quyens.AsNoTracking().ToDictionaryAsync(x => x.MaQuyen);
        var cu = vaiTro.VaiTroQuyens.Select(x => x.MaQuyen).ToHashSet();

        PermissionDto Dto(int ma) => ToPermissionDto(ma, tatCa[ma].TenQuyen, tatCa[ma].NhomChucNang);
        var capThem = moi.Except(cu).Select(Dto).OrderBy(x => x.NhomHienThi).ThenBy(x => x.TenHienThi).ToList();
        var thuHoi = cu.Except(moi).Select(Dto).OrderBy(x => x.NhomHienThi).ThenBy(x => x.TenHienThi).ToList();
        var tenMoi = moi.Select(m => tatCa[m].TenQuyen).ToHashSet();

        string? loi = null;
        if (vaiTro.TenVaiTro == "KHACH_HANG" && moi.Count > 0) loi = "Vai trò KHACH_HANG không được gán quyền của nhân viên.";
        else if (vaiTro.TenVaiTro == "QUAN_LY" && !tenMoi.Contains(Permissions.QuanTriHeThong)) loi = $"Vai trò QUAN_LY phải luôn có quyền \"{PermissionCatalog.TenHienThi(Permissions.QuanTriHeThong)}\", nếu không sẽ không ai quản lý được phân quyền.";

        var canhBaoMoi = PhanTichCanhBao(vaiTro.TenVaiTro, tenMoi);
        var canhBaoCu = PhanTichCanhBao(vaiTro.TenVaiTro, vaiTro.VaiTroQuyens.Select(x => x.Quyen.TenQuyen).ToHashSet());
        var nhayThem = capThem.Where(x => x.MucNhayCam != PermissionCatalog.Thuong).Select(x => x.TenHienThi).ToList();
        var nhayBo = thuHoi.Where(x => x.MucNhayCam != PermissionCatalog.Thuong).Select(x => x.TenHienThi).ToList();
        var dto = new RoleChangePreviewDto
        {
            MaVaiTro = maVaiTro, TenVaiTro = vaiTro.TenVaiTro, KhongCoThayDoi = capThem.Count == 0 && thuHoi.Count == 0,
            SoNhanVienAnhHuong = vaiTro.NhanViens.Count, NhanVienAnhHuong = vaiTro.NhanViens.Select(x => x.HoTen).Take(30).ToList(),
            CapThem = capThem, ThuHoi = thuHoi, CapThemNhayCam = nhayThem, ThuHoiNhayCam = nhayBo,
            CanhBao = canhBaoMoi.Except(canhBaoCu).ToList(), NenCoLyDo = nhayThem.Count > 0 || nhayBo.Count > 0, LoiChan = loi
        };
        dto.TomTat = dto.KhongCoThayDoi ? "Không có thay đổi quyền."
            : $"Cấp thêm {capThem.Count} quyền, thu hồi {thuHoi.Count} quyền cho vai trò {vaiTro.TenVaiTro}; ảnh hưởng {dto.SoNhanVienAnhHuong} nhân viên (có hiệu lực ngay ở thao tác kế tiếp của họ)."
              + (nhayThem.Count > 0 ? $" CẤP THÊM quyền nhạy cảm: {string.Join("; ", nhayThem)}." : "")
              + (nhayBo.Count > 0 ? $" Thu hồi quyền nhạy cảm: {string.Join("; ", nhayBo)}." : "");
        return dto;
    }

    public async Task<List<RoleHistoryEntryDto>> GetHistoryAsync(string? doiTuong)
    {
        var q = _context.NhatKyThaoTacs.AsNoTracking().Where(x => HanhDongPhanQuyen.Contains(x.HanhDong));
        var d = doiTuong?.Trim();
        if (!string.IsNullOrEmpty(d))
        {
            // Mã nhân viên / tên vai trò tối đa 50 ký tự: chuỗi dài hơn không thể khớp (và mẫu LIKE quá dài làm SQL Server báo lỗi).
            if (d.Length > 100) return new List<RoleHistoryEntryDto>();
            // Đối tượng = mã nhân viên (NVxx) hoặc tên vai trò; thao tác khóa tự động ghi theo tên đăng nhập nên tìm thêm trong chi tiết.
            var tenDN = await _context.NhanViens.AsNoTracking().Where(x => x.MaNV == d).Select(x => x.TaiKhoan!.TenDN).FirstOrDefaultAsync();
            q = q.Where(x => x.DoiTuongTacDong == d || (tenDN != null && x.DoiTuongTacDong == tenDN) || (x.ChiTiet != null && x.ChiTiet.Contains(d)));
        }
        var rows = await q.OrderByDescending(x => x.ThoiGian).Take(150).ToListAsync();
        var ten = await TenNguoiDung.HienThiAsync(_context, rows.Select(x => (string?)x.MaTaiKhoan).Distinct());
        return rows.Select(x => new RoleHistoryEntryDto
        {
            ThoiGian = x.ThoiGian, NguoiThucHien = ten.GetValueOrDefault(x.MaTaiKhoan, x.MaTaiKhoan), HanhDong = x.HanhDong, MoTaHanhDong = AuditLogService.MoTaHanhDong(x.HanhDong), DoiTuong = x.DoiTuongTacDong, ChiTiet = x.ChiTiet
        }).ToList();
    }

    /// <summary>Mật khẩu tạm ngẫu nhiên mạnh (14 ký tự, đủ chữ hoa, chữ thường, số, ký hiệu; bỏ ký tự dễ nhầm).</summary>
    internal static string TaoMatKhauTam()
    {
        const string hoa = "ABCDEFGHJKLMNPQRSTUVWXYZ", thuong = "abcdefghijkmnpqrstuvwxyz", so = "23456789", kyHieu = "@#$%&*?";
        var tatCa = hoa + thuong + so + kyHieu;
        var ds = new List<char> { Chon(hoa), Chon(thuong), Chon(so), Chon(kyHieu) };
        while (ds.Count < 14) ds.Add(Chon(tatCa));
        for (var i = ds.Count - 1; i > 0; i--) { var j = RandomNumberGenerator.GetInt32(i + 1); (ds[i], ds[j]) = (ds[j], ds[i]); }
        return new string(ds.ToArray());
        static char Chon(string s) => s[RandomNumberGenerator.GetInt32(s.Length)];
    }

    private static string LyDoChiTiet(string? lyDo) => string.IsNullOrWhiteSpace(lyDo) ? "" : $"; lyDo={lyDo.Trim()}";
}
