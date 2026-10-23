using KLCN060.Api.Auditing;
using KLCN060.Api.DTOs.Roles;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public partial class RoleService : IRoleService
{
    /// <summary>5 vai trò hệ thống: tên được dùng trong [Authorize(Roles=...)] nên không được đổi tên.</summary>
    private static readonly string[] VaiTroHeThong = { "QUAN_LY", "LE_TAN", "BUONG_PHONG", "KE_TOAN", "KHACH_HANG" };

    private const string GhiChuHieuLuc =
        "Vai trò mới có hiệu lực ngay ở lần gọi API kế tiếp của nhân viên (quyền được kiểm tra lại theo CSDL ở mỗi request); nhân viên nên đăng nhập lại để giao diện cập nhật đủ chức năng.";

    private readonly KLCN060DbContext _context;
    private readonly IAuditTrail _audit;

    public RoleService(KLCN060DbContext context, IAuditTrail audit)
    {
        _context = context;
        _audit = audit;
    }

    public async Task<List<RoleDto>> GetAllAsync()
    {
        var vaiTros = await _context.VaiTros
            .AsNoTracking()
            .Include(x => x.VaiTroQuyens).ThenInclude(vq => vq.Quyen)
            .Include(x => x.NhanViens.Where(n => n.TaiKhoan == null || n.TaiKhoan.TrangThai != TrangThaiTaiKhoan.DA_XOA))
            .OrderBy(x => x.MaVaiTro)
            .ToListAsync();

        return vaiTros.Select(ToDto).ToList();
    }

    public async Task<List<PermissionDto>> GetPermissionsAsync()
    {
        var quyens = await _context.Quyens.AsNoTracking().ToListAsync();
        // Thứ tự hiển thị theo danh mục (KLCN060.Domain.Permissions); quyền ngoài danh mục (nếu có) xếp cuối.
        var thuTu = Permissions.DanhMuc.Select((x, i) => (x.TenQuyen, i)).ToDictionary(x => x.TenQuyen, x => x.i);
        return quyens
            .OrderBy(x => thuTu.GetValueOrDefault(x.TenQuyen, int.MaxValue)).ThenBy(x => x.TenQuyen)
            .Select(x => ToPermissionDto(x.MaQuyen, x.TenQuyen, x.NhomChucNang))
            .ToList();
    }

    public async Task<RoleDto> CreateAsync(RoleRequest request)
    {
        var ten = ChuanHoaTen(request.TenVaiTro);
        if (await TenVaiTroDaCoAsync(ten, null))
            throw new ApiException(StatusCodes.Status409Conflict, "VAI_TRO_DA_TON_TAI", "Tên vai trò đã tồn tại.", "tenVaiTro");

        var maQuyens = await KiemTraQuyenAsync(request.MaQuyen);

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var vaiTro = new VaiTro { TenVaiTro = ten, MoTa = request.MoTa };
        _context.VaiTros.Add(vaiTro);
        await _context.SaveChangesAsync();

        foreach (var maQuyen in maQuyens)
            _context.VaiTroQuyens.Add(new VaiTroQuyen { MaVaiTro = vaiTro.MaVaiTro, MaQuyen = maQuyen });
        _audit.Ghi("TAO_VAI_TRO", ten, $"quyen={string.Join(",", await TenQuyenAsync(maQuyens))}");

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "XUNG_DOT_DU_LIEU", "Có xung đột dữ liệu khi tạo vai trò, vui lòng thử lại.");
        }
        await transaction.CommitAsync();

        return await LayAsync(vaiTro.MaVaiTro);
    }

    public async Task<RoleDto> UpdateAsync(int maVaiTro, RoleRequest request)
    {
        // Hai người lưu quyền cùng một vai trò cùng lúc: khóa dòng vai trò TRƯỚC khi đọc tập quyền hiện tại, để người sau đọc
        // đúng kết quả người trước vừa lưu (trước đây cả hai cùng dựa trên bản cũ → xóa trùng dòng gây 500, thêm trùng gây 409 sai).
        await using var transaction = await _context.Database.BeginTransactionAsync();
        await _context.Database.ExecuteSqlAsync($"SELECT MaVaiTro FROM VaiTros WITH (UPDLOCK, HOLDLOCK) WHERE MaVaiTro = {maVaiTro}");

        var vaiTro = await _context.VaiTros.Include(x => x.VaiTroQuyens).Include(x => x.NhanViens.Where(n => n.TaiKhoan == null || n.TaiKhoan.TrangThai != TrangThaiTaiKhoan.DA_XOA)).FirstOrDefaultAsync(x => x.MaVaiTro == maVaiTro)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_VAI_TRO", "Không tìm thấy vai trò.");

        var ten = ChuanHoaTen(request.TenVaiTro);
        if (ten != vaiTro.TenVaiTro)
        {
            if (VaiTroHeThong.Contains(vaiTro.TenVaiTro))
                throw new ApiException(StatusCodes.Status400BadRequest, "VAI_TRO_HE_THONG", $"Không được đổi tên vai trò hệ thống {vaiTro.TenVaiTro}.", "tenVaiTro");
            if (await TenVaiTroDaCoAsync(ten, maVaiTro))
                throw new ApiException(StatusCodes.Status409Conflict, "VAI_TRO_DA_TON_TAI", "Tên vai trò đã tồn tại.", "tenVaiTro");
        }

        var maQuyens = (await KiemTraQuyenAsync(request.MaQuyen)).ToHashSet();

        // Hai chốt an toàn của phân quyền thật: (1) khách hàng không bao giờ mang quyền nhân viên; (2) vai trò QUAN_LY phải luôn giữ
        // QUAN_TRI_HE_THONG - nếu không, sau khi lưu sẽ không còn ai vào được màn hình phân quyền để sửa lại.
        if (vaiTro.TenVaiTro == "KHACH_HANG" && maQuyens.Count > 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHACH_HANG_KHONG_CO_QUYEN_NHAN_VIEN", "Vai trò KHACH_HANG không được gán quyền của nhân viên.", "maQuyen");
        if (vaiTro.TenVaiTro == "QUAN_LY")
        {
            var maQuanTri = await _context.Quyens.Where(x => x.TenQuyen == Permissions.QuanTriHeThong).Select(x => x.MaQuyen).FirstOrDefaultAsync();
            if (!maQuyens.Contains(maQuanTri))
                throw new ApiException(StatusCodes.Status400BadRequest, "QUAN_LY_PHAI_CO_QUYEN_QUAN_TRI",
                    $"Vai trò QUAN_LY phải luôn có quyền {Permissions.QuanTriHeThong}, nếu không sẽ không ai quản lý được phân quyền.", "maQuyen");
        }

        // Cấp / thu hồi quyền RẤT NHẠY CẢM (duyệt không giới hạn, quản trị, xử lý đối soát...) bắt buộc nêu lý do để nhật ký truy vết được.
        var quyenCuIds = vaiTro.VaiTroQuyens.Select(x => x.MaQuyen).ToHashSet();
        var doiRatNhay = (await TenQuyenAsync(maQuyens.Except(quyenCuIds).Concat(quyenCuIds.Except(maQuyens)))).Where(PermissionCatalog.LaRatNhayCam).ToList();
        if (doiRatNhay.Count > 0 && string.IsNullOrWhiteSpace(request.LyDo))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_LY_DO",
                $"Thay đổi có quyền rất nhạy cảm ({string.Join("; ", doiRatNhay.Select(PermissionCatalog.TenHienThi))}): vui lòng nhập lý do để lưu vào nhật ký.", "lyDo");

        var tenCu = vaiTro.TenVaiTro;
        var quyenCu = quyenCuIds;
        vaiTro.TenVaiTro = ten;
        vaiTro.MoTa = request.MoTa;

        // Kết quả tương đương "xóa hết rồi thêm lại": tập quyền cuối cùng đúng bằng danh sách gửi lên.
        foreach (var cu in vaiTro.VaiTroQuyens.Where(x => !maQuyens.Contains(x.MaQuyen)).ToList())
            _context.VaiTroQuyens.Remove(cu);
        var daCo = vaiTro.VaiTroQuyens.Select(x => x.MaQuyen).ToHashSet();
        foreach (var moi in maQuyens.Where(q => !daCo.Contains(q)))
            _context.VaiTroQuyens.Add(new VaiTroQuyen { MaVaiTro = maVaiTro, MaQuyen = moi });

        // Phân quyền là dữ liệu nhạy cảm nhất: ghi rõ quyền nào vừa được cấp / thu hồi cho vai trò nào.
        var themVao = await TenQuyenAsync(maQuyens.Except(quyenCu));
        var goRa = await TenQuyenAsync(quyenCu.Except(maQuyens));
        _audit.Ghi("CAP_NHAT_PHAN_QUYEN", ten,
            $"vaiTro={tenCu}{(tenCu != ten ? $" -> {ten}" : "")}; capThem=[{string.Join(",", themVao)}]; thuHoi=[{string.Join(",", goRa)}]; nhanVienAnhHuong={vaiTro.NhanViens.Count}{LyDoChiTiet(request.LyDo)}");

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return await LayAsync(maVaiTro);
    }

    public async Task<List<EmployeeDto>> GetEmployeesAsync()
    {
        return await _context.NhanViens
            .AsNoTracking()
            .Where(x => x.TaiKhoan == null || x.TaiKhoan.TrangThai != TrangThaiTaiKhoan.DA_XOA)
            .OrderBy(x => x.MaNV)
            .Select(x => new EmployeeDto
            {
                MaNV = x.MaNV,
                HoTen = x.HoTen,
                SoDT = x.SoDT,
                ChucVu = x.ChucVu,
                MaVaiTro = x.MaVaiTro,
                TenVaiTro = x.VaiTro.TenVaiTro,
                TenDangNhap = x.TaiKhoan == null ? null : x.TaiKhoan.TenDN,
                TrangThaiTaiKhoan = x.TaiKhoan == null ? null : x.TaiKhoan.TrangThai.ToString(),
                LanDangNhapCuoi = x.TaiKhoan == null ? null : x.TaiKhoan.LanDangNhapCuoi,
                SoLanDangNhapSai = x.TaiKhoan == null ? 0 : x.TaiKhoan.SoLanDangNhapSai,
                CoPhienDangNhap = x.TaiKhoan != null && x.TaiKhoan.RefreshToken != null && x.TaiKhoan.RefreshTokenExpiry > DateTime.UtcNow,
                PhienHetHan = x.TaiKhoan != null && x.TaiKhoan.RefreshToken != null && x.TaiKhoan.RefreshTokenExpiry > DateTime.UtcNow ? x.TaiKhoan.RefreshTokenExpiry : null
            })
            .ToListAsync();
    }

    public async Task<ChangeEmployeeRoleResultDto> ChangeEmployeeRoleAsync(string maNV, ChangeEmployeeRoleRequest request, CurrentUser user)
    {
        if (maNV == user.MaNV)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHONG_THE_TU_DOI_VAI_TRO_CUA_CHINH_MINH", "Không thể tự đổi vai trò của chính mình.");

        var nhanVien = await _context.NhanViens.Include(x => x.VaiTro).Include(x => x.TaiKhoan).FirstOrDefaultAsync(x => x.MaNV == maNV)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_NHAN_VIEN", "Không tìm thấy nhân viên.");
        KiemTraChuaXoa(nhanVien);

        var vaiTroMoi = await _context.VaiTros.FirstOrDefaultAsync(x => x.MaVaiTro == request.MaVaiTro)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_VAI_TRO", "Không tìm thấy vai trò.", "maVaiTro");
        if (vaiTroMoi.TenVaiTro == "KHACH_HANG")
            throw new ApiException(StatusCodes.Status400BadRequest, "VAI_TRO_KHONG_HOP_LE", "Không thể gán vai trò KHACH_HANG cho nhân viên.", "maVaiTro");

        var vaiTroCu = nhanVien.VaiTro.TenVaiTro;
        nhanVien.MaVaiTro = vaiTroMoi.MaVaiTro;

        _audit.Ghi("DOI_VAI_TRO_NHAN_VIEN", maNV, $"{vaiTroCu} -> {vaiTroMoi.TenVaiTro}{LyDoChiTiet(request.LyDo)}");

        await _context.SaveChangesAsync();

        return new ChangeEmployeeRoleResultDto
        {
            NhanVien = new EmployeeDto
            {
                MaNV = nhanVien.MaNV,
                HoTen = nhanVien.HoTen,
                SoDT = nhanVien.SoDT,
                ChucVu = nhanVien.ChucVu,
                MaVaiTro = vaiTroMoi.MaVaiTro,
                TenVaiTro = vaiTroMoi.TenVaiTro,
                TenDangNhap = nhanVien.TaiKhoan?.TenDN
            },
            VaiTroCu = vaiTroCu,
            GhiChu = GhiChuHieuLuc
        };
    }

    public async Task<EmployeeDto> CreateEmployeeAsync(CreateEmployeeRequest request, CurrentUser user)
    {
        var hoTen = request.HoTen?.Trim();
        var chucVu = request.ChucVu?.Trim();
        KiemTraHoTen(hoTen);
        if (string.IsNullOrEmpty(chucVu))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_CHUC_VU", "Chức vụ là bắt buộc.", "chucVu");
        var soDT = ContactValidator.KiemTraSoDT(request.SoDT, "soDT");
        var tenDN = KiemTraTenDangNhap(request.TenDangNhap);
        PasswordService.KiemTraMatKhauMoi(request.MatKhau);
        KiemTraMatKhauKhongYeu(request.MatKhau!, tenDN);

        var vaiTro = await _context.VaiTros.AsNoTracking().FirstOrDefaultAsync(x => x.MaVaiTro == request.MaVaiTro)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_VAI_TRO", "Không tìm thấy vai trò.", "maVaiTro");
        if (vaiTro.TenVaiTro == "KHACH_HANG")
            throw new ApiException(StatusCodes.Status400BadRequest, "VAI_TRO_KHONG_HOP_LE", "Không thể gán vai trò KHACH_HANG cho nhân viên.", "maVaiTro");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        // Khóa theo tiền tố "NV" (giữ tới hết transaction) TRƯỚC khi kiểm tra trùng SĐT: nếu không, nhiều yêu cầu đồng thời
        // cùng SĐT đều thấy "chưa có" rồi cùng thêm (SĐT nhân viên không có chỉ mục duy nhất).
        await MaCodeGenerator.KhoaTienToAsync(_context, "NV");

        if (await _context.TaiKhoans.AnyAsync(x => x.TenDN == tenDN))
            throw new ApiException(StatusCodes.Status409Conflict, "TEN_DANG_NHAP_DA_TON_TAI", "Tên đăng nhập đã được sử dụng.", "tenDangNhap");
        if (await _context.NhanViens.AnyAsync(x => x.SoDT == soDT))
            throw new ApiException(StatusCodes.Status409Conflict, "SDT_NHAN_VIEN_DA_TON_TAI", "Số điện thoại đã thuộc về một nhân viên khác.", "soDT");

        var maNV = await MaCodeGenerator.GenerateNextAsync(_context, _context.NhanViens.Select(x => x.MaNV), "NV", 2);
        var nhanVien = new NhanVien { MaNV = maNV, HoTen = hoTen, SoDT = soDT, ChucVu = chucVu, MaVaiTro = vaiTro.MaVaiTro };
        var taiKhoan = new TaiKhoan
        {
            TenDN = tenDN,
            MatKhau = BCrypt.Net.BCrypt.HashPassword(request.MatKhau),
            LoaiTaiKhoan = LoaiTaiKhoan.NHAN_VIEN,
            MaNV = maNV,
            TrangThai = TrangThaiTaiKhoan.HOAT_DONG
        };
        _context.NhanViens.Add(nhanVien);
        _context.TaiKhoans.Add(taiKhoan);
        _audit.Ghi("TAO_NHAN_VIEN", maNV, $"tenDangNhap={tenDN}; vaiTro={vaiTro.TenVaiTro}; chucVu={chucVu}");

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "XUNG_DOT_DU_LIEU", "Có xung đột dữ liệu khi tạo nhân viên, vui lòng thử lại.");
        }
        await transaction.CommitAsync();

        nhanVien.VaiTro = vaiTro;
        return ToEmployeeDto(nhanVien, taiKhoan);
    }

    public async Task<EmployeeDto> UpdateEmployeeAsync(string maNV, UpdateEmployeeRequest request, CurrentUser user)
    {
        var hoTen = request.HoTen?.Trim();
        var chucVu = request.ChucVu?.Trim();
        KiemTraHoTen(hoTen);
        if (string.IsNullOrEmpty(chucVu))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_CHUC_VU", "Chức vụ là bắt buộc.", "chucVu");
        var soDT = ContactValidator.KiemTraSoDT(request.SoDT, "soDT");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        await MaCodeGenerator.KhoaTienToAsync(_context, "NV");   // cùng khóa với lúc tạo nhân viên: kiểm tra trùng SĐT không bị lọt khi đồng thời

        var nhanVien = await _context.NhanViens.Include(x => x.VaiTro).Include(x => x.TaiKhoan).FirstOrDefaultAsync(x => x.MaNV == maNV)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_NHAN_VIEN", "Không tìm thấy nhân viên.");
        KiemTraChuaXoa(nhanVien);
        if (await _context.NhanViens.AnyAsync(x => x.SoDT == soDT && x.MaNV != maNV))
            throw new ApiException(StatusCodes.Status409Conflict, "SDT_NHAN_VIEN_DA_TON_TAI", "Số điện thoại đã thuộc về một nhân viên khác.", "soDT");

        _audit.Ghi("CAP_NHAT_NHAN_VIEN", maNV, $"hoTen={nhanVien.HoTen} -> {hoTen}; soDT={nhanVien.SoDT} -> {soDT}; chucVu={nhanVien.ChucVu} -> {chucVu}");
        nhanVien.HoTen = hoTen;
        nhanVien.SoDT = soDT;
        nhanVien.ChucVu = chucVu;
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return ToEmployeeDto(nhanVien, nhanVien.TaiKhoan);
    }

    public async Task<EmployeeAccountResultDto> LockEmployeeAsync(string maNV, CurrentUser user, string? lyDo = null)
    {
        if (maNV == user.MaNV)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHONG_THE_TU_KHOA_TAI_KHOAN", "Không thể tự khóa tài khoản của chính mình.");

        var (nhanVien, taiKhoan) = await TimTaiKhoanNhanVienAsync(maNV);
        if (taiKhoan.TrangThai == TrangThaiTaiKhoan.BI_KHOA)
            throw new ApiException(StatusCodes.Status409Conflict, "TAI_KHOAN_DA_BI_KHOA", "Tài khoản này đã bị khóa.");

        taiKhoan.TrangThai = TrangThaiTaiKhoan.BI_KHOA;
        // Thu hồi phiên đăng nhập: không đăng nhập lại/làm mới token được; các lệnh gọi API đang dùng token cũ cũng bị từ chối ngay (kiểm tra trạng thái mỗi request).
        taiKhoan.RefreshToken = null;
        taiKhoan.RefreshTokenExpiry = null;

        GhiNhatKy(user.TenDN, "KHOA_TAI_KHOAN_NHAN_VIEN", maNV, "HOAT_DONG -> BI_KHOA; thuHoiPhien=true" + LyDoChiTiet(lyDo));
        await _context.SaveChangesAsync();

        return new EmployeeAccountResultDto
        {
            NhanVien = ToEmployeeDto(nhanVien, taiKhoan),
            GhiChu = "Đã khóa tài khoản: nhân viên không thể đăng nhập hay thao tác cho tới khi được mở khóa."
        };
    }

    /// <summary>
    /// Xóa nhân viên: chỉ khi tài khoản ĐANG BỊ KHÓA, bắt buộc lý do, không tự xóa mình. Nhân viên bị ẩn khỏi danh sách và không đăng nhập được; hồ sơ và mọi lịch sử
    /// (hóa đơn đã thu, nhật ký thao tác) được giữ nguyên để truy vết - nên tên đăng nhập / số điện thoại của nhân viên đã xóa vẫn được giữ chỗ.
    /// </summary>
    public async Task<EmployeeAccountResultDto> DeleteEmployeeAsync(string maNV, CurrentUser user, string? lyDo)
    {
        if (maNV == user.MaNV)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHONG_THE_TU_XOA_NHAN_VIEN", "Không thể tự xóa chính mình.");
        if (string.IsNullOrWhiteSpace(lyDo))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_LY_DO", "Cần nhập lý do khi xóa nhân viên (được ghi vào nhật ký).", "lyDo");

        var (nhanVien, taiKhoan) = await TimTaiKhoanNhanVienAsync(maNV);
        if (taiKhoan.TrangThai != TrangThaiTaiKhoan.BI_KHOA)
            throw new ApiException(StatusCodes.Status409Conflict, "CHUA_KHOA_TAI_KHOAN", "Chỉ xóa được nhân viên có tài khoản đã bị khóa. Hãy khóa tài khoản trước, rồi xóa.");

        var ketQua = ToEmployeeDto(nhanVien, taiKhoan);
        taiKhoan.TrangThai = TrangThaiTaiKhoan.DA_XOA;
        taiKhoan.RefreshToken = null;
        taiKhoan.RefreshTokenExpiry = null;

        GhiNhatKy(user.TenDN, "XOA_NHAN_VIEN", maNV,
            $"nhanVien={nhanVien.HoTen}; tenDangNhap={taiKhoan.TenDN}; vaiTro={nhanVien.VaiTro.TenVaiTro}; BI_KHOA -> DA_XOA; giuLichSu=true{LyDoChiTiet(lyDo)}");
        await _context.SaveChangesAsync();

        return new EmployeeAccountResultDto
        {
            NhanVien = ketQua,
            GhiChu = "Đã xóa nhân viên: không còn trong danh sách và không đăng nhập được. Lịch sử thao tác, hóa đơn đã thu của nhân viên này vẫn được giữ để truy vết."
        };
    }

    private static string KiemTraTenDangNhap(string? tenDangNhap)
    {
        var ten = tenDangNhap?.Trim();
        if (string.IsNullOrEmpty(ten))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_TEN_DANG_NHAP", "Tên đăng nhập là bắt buộc.", "tenDangNhap");
        if (ten.Length is < 3 or > 50 || !System.Text.RegularExpressions.Regex.IsMatch(ten, "^[A-Za-z0-9._-]+$"))
            throw new ApiException(StatusCodes.Status400BadRequest, "TEN_DANG_NHAP_KHONG_HOP_LE", "Tên đăng nhập gồm 3-50 ký tự chữ không dấu, số, dấu chấm, gạch dưới hoặc gạch ngang.", "tenDangNhap");
        // Tên đăng nhập của khách hàng là số điện thoại: nhân viên không được dùng chuỗi toàn số để khỏi chiếm chỗ hay nhầm lẫn với khách.
        if (ten.All(char.IsDigit))
            throw new ApiException(StatusCodes.Status400BadRequest, "TEN_DANG_NHAP_KHONG_HOP_LE", "Tên đăng nhập của nhân viên phải có ít nhất một chữ cái (chuỗi toàn số dành cho khách hàng).", "tenDangNhap");
        return ten;
    }

    public async Task<EmployeeAccountResultDto> UnlockEmployeeAsync(string maNV, CurrentUser user, string? lyDo = null)
    {
        var (nhanVien, taiKhoan) = await TimTaiKhoanNhanVienAsync(maNV);

        var daKhoa = taiKhoan.TrangThai == TrangThaiTaiKhoan.BI_KHOA;
        taiKhoan.TrangThai = TrangThaiTaiKhoan.HOAT_DONG;
        taiKhoan.SoLanDangNhapSai = 0;

        GhiNhatKy(user.TenDN, "MO_KHOA_TAI_KHOAN_NHAN_VIEN", maNV, (daKhoa ? "BI_KHOA -> HOAT_DONG" : "tai khoan dang hoat dong, dat lai bo dem nhap sai") + LyDoChiTiet(lyDo));
        await _context.SaveChangesAsync();

        return new EmployeeAccountResultDto
        {
            NhanVien = ToEmployeeDto(nhanVien, taiKhoan),
            GhiChu = daKhoa ? "Đã mở khóa tài khoản." : "Tài khoản không bị khóa; đã đặt lại bộ đếm nhập sai mật khẩu."
        };
    }

    public async Task<EmployeeAccountResultDto> ResetEmployeePasswordAsync(string maNV, ResetEmployeePasswordRequest request, CurrentUser user)
    {
        // Nhân viên không có email để tự khôi phục mật khẩu (quên mật khẩu chỉ dành cho khách hàng), nên quản lý là đường khôi phục duy nhất.
        // Tự đặt lại mật khẩu của chính mình phải qua "Đổi mật khẩu" (yêu cầu mật khẩu hiện tại).
        if (maNV == user.MaNV)
            throw new ApiException(StatusCodes.Status400BadRequest, "DUNG_DOI_MAT_KHAU", "Hãy dùng chức năng Đổi mật khẩu để đổi mật khẩu của chính bạn.");

        // Không nhập mật khẩu → hệ thống sinh mật khẩu ngẫu nhiên mạnh (không để người quản trị tự nghĩ ra mật khẩu yếu); mật khẩu chỉ hiện một lần.
        var tuSinh = string.IsNullOrWhiteSpace(request.MatKhauMoi);
        var matKhauMoi = tuSinh ? TaoMatKhauTam() : request.MatKhauMoi!;
        PasswordService.KiemTraMatKhauMoi(matKhauMoi);

        var (nhanVien, taiKhoan) = await TimTaiKhoanNhanVienAsync(maNV);
        if (!tuSinh) KiemTraMatKhauKhongYeu(matKhauMoi, taiKhoan.TenDN);

        taiKhoan.MatKhau = BCrypt.Net.BCrypt.HashPassword(matKhauMoi);
        taiKhoan.SoLanDangNhapSai = 0;
        taiKhoan.TrangThai = TrangThaiTaiKhoan.HOAT_DONG;
        // Mọi phiên đăng nhập đang có (refresh token) mất hiệu lực: người đang giữ mật khẩu cũ không còn đăng nhập tiếp được.
        taiKhoan.RefreshToken = null;
        taiKhoan.RefreshTokenExpiry = null;

        GhiNhatKy(user.TenDN, "DAT_LAI_MAT_KHAU_NHAN_VIEN", maNV, $"matKhauTuSinh={tuSinh}; thuHoiPhien=true; moKhoa=true{LyDoChiTiet(request.LyDo)}");
        await _context.SaveChangesAsync();

        return new EmployeeAccountResultDto
        {
            NhanVien = ToEmployeeDto(nhanVien, taiKhoan),
            GhiChu = "Đã đặt mật khẩu mới, mở khóa tài khoản và thu hồi các phiên đăng nhập cũ. Hãy yêu cầu nhân viên đổi mật khẩu sau khi đăng nhập.",
            MatKhauTam = tuSinh ? matKhauMoi : null
        };
    }

    private async Task<(NhanVien NhanVien, TaiKhoan TaiKhoan)> TimTaiKhoanNhanVienAsync(string maNV)
    {
        var nhanVien = await _context.NhanViens.Include(x => x.VaiTro).Include(x => x.TaiKhoan).FirstOrDefaultAsync(x => x.MaNV == maNV)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_NHAN_VIEN", "Không tìm thấy nhân viên.");
        var taiKhoan = nhanVien.TaiKhoan
            ?? throw new ApiException(StatusCodes.Status404NotFound, "NHAN_VIEN_CHUA_CO_TAI_KHOAN", "Nhân viên này chưa có tài khoản đăng nhập.");
        if (taiKhoan.TrangThai == TrangThaiTaiKhoan.DA_XOA)
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_NHAN_VIEN", "Nhân viên này đã bị xóa.");
        return (nhanVien, taiKhoan);
    }

    private static readonly string[] MatKhauPhoBien =
    {
        "12345678", "123456789", "1234567890", "11111111", "00000000", "87654321", "password", "password1", "passw0rd", "qwertyui", "qwerty123",
        "abcd1234", "abc12345", "12341234", "matkhau123", "admin123", "admin1234", "iloveyou", "letmein1", "welcome1", "khachsan123"
    };

    /// <summary>Mật khẩu đủ dài nhưng quá dễ đoán (nằm trong danh sách phổ biến, một ký tự lặp lại, hoặc chứa chính tên đăng nhập) vẫn bị từ chối cho nhân viên.</summary>
    private static void KiemTraMatKhauKhongYeu(string matKhau, string tenDangNhap)
    {
        var thap = matKhau.ToLowerInvariant();
        var yeu = MatKhauPhoBien.Contains(thap)
            || matKhau.Distinct().Count() == 1
            || (tenDangNhap.Length >= 3 && thap.Contains(tenDangNhap.ToLowerInvariant()));
        if (yeu)
            throw new ApiException(StatusCodes.Status400BadRequest, "MAT_KHAU_QUA_YEU",
                "Mật khẩu quá dễ đoán (mật khẩu phổ biến, ký tự lặp lại hoặc chứa tên đăng nhập). Hãy chọn mật khẩu khác.", "matKhau");
    }

    private static void KiemTraHoTen(string? hoTen)
    {
        if (string.IsNullOrEmpty(hoTen))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_HO_TEN", "Họ tên là bắt buộc.", "hoTen");
        if (hoTen.Length < 2 || !hoTen.Any(char.IsLetter))
            throw new ApiException(StatusCodes.Status400BadRequest, "HO_TEN_KHONG_HOP_LE", "Họ tên tối thiểu 2 ký tự và phải có chữ cái.", "hoTen");
    }

    /// <summary>Nhân viên đã xóa chỉ còn để giữ lịch sử: mọi thao tác sửa đều như "không tìm thấy" (giống khóa / mở khóa / đặt lại mật khẩu).</summary>
    private static void KiemTraChuaXoa(NhanVien nhanVien)
    {
        if (nhanVien.TaiKhoan?.TrangThai == TrangThaiTaiKhoan.DA_XOA)
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_NHAN_VIEN", "Nhân viên này đã bị xóa.");
    }

    /// <summary>Tên vai trò trùng theo so sánh không phân biệt dấu và hoa/thường ("Trùng" ≡ "Trung").</summary>
    private async Task<bool> TenVaiTroDaCoAsync(string ten, int? boQuaMaVaiTro)
    {
        var cacTen = await _context.VaiTros.AsNoTracking().Where(x => x.MaVaiTro != boQuaMaVaiTro).Select(x => x.TenVaiTro).ToListAsync();
        return cacTen.Any(t => VanBanHelper.CungTen(t, ten));
    }

    private void GhiNhatKy(string tenDN, string hanhDong, string doiTuong, string? chiTiet)
        => _audit.Ghi(hanhDong, doiTuong, chiTiet);

    private async Task<List<string>> TenQuyenAsync(IEnumerable<int> maQuyens)
    {
        var ds = maQuyens.ToList();
        return await _context.Quyens.Where(x => ds.Contains(x.MaQuyen)).OrderBy(x => x.TenQuyen).Select(x => x.TenQuyen).ToListAsync();
    }

    private static EmployeeDto ToEmployeeDto(NhanVien nhanVien, TaiKhoan? taiKhoan) => new()
    {
        MaNV = nhanVien.MaNV,
        HoTen = nhanVien.HoTen,
        SoDT = nhanVien.SoDT,
        ChucVu = nhanVien.ChucVu,
        MaVaiTro = nhanVien.MaVaiTro,
        TenVaiTro = nhanVien.VaiTro.TenVaiTro,
        TenDangNhap = taiKhoan?.TenDN,
        TrangThaiTaiKhoan = taiKhoan?.TrangThai.ToString(),
        LanDangNhapCuoi = taiKhoan?.LanDangNhapCuoi,
        SoLanDangNhapSai = taiKhoan?.SoLanDangNhapSai ?? 0,
        CoPhienDangNhap = taiKhoan is { RefreshToken: not null } && taiKhoan.RefreshTokenExpiry > DateTime.UtcNow,
        PhienHetHan = taiKhoan is { RefreshToken: not null } && taiKhoan.RefreshTokenExpiry > DateTime.UtcNow ? taiKhoan.RefreshTokenExpiry : null
    };

    private async Task<RoleDto> LayAsync(int maVaiTro)
    {
        var vaiTro = await _context.VaiTros
            .AsNoTracking()
            .Include(x => x.VaiTroQuyens).ThenInclude(vq => vq.Quyen)
            .Include(x => x.NhanViens.Where(n => n.TaiKhoan == null || n.TaiKhoan.TrangThai != TrangThaiTaiKhoan.DA_XOA))
            .FirstAsync(x => x.MaVaiTro == maVaiTro);
        return ToDto(vaiTro);
    }

    private async Task<List<int>> KiemTraQuyenAsync(List<int>? maQuyen)
    {
        var distinct = (maQuyen ?? new()).Distinct().ToList();
        var tonTai = await _context.Quyens.Where(x => distinct.Contains(x.MaQuyen)).Select(x => x.MaQuyen).ToListAsync();
        var thieu = distinct.Except(tonTai).ToList();
        if (thieu.Count > 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "QUYEN_KHONG_TON_TAI", $"Không tìm thấy quyền: {string.Join(", ", thieu)}.", "maQuyen");
        return distinct;
    }

    internal static string ChuanHoaTen(string? ten)
    {
        var t = ten?.Trim();
        if (string.IsNullOrEmpty(t))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_TEN_VAI_TRO", "Tên vai trò là bắt buộc.", "tenVaiTro");
        if (t.Length > 50)
            throw new ApiException(StatusCodes.Status400BadRequest, "TEN_VAI_TRO_QUA_DAI", "Tên vai trò tối đa 50 ký tự.", "tenVaiTro");
        return t;
    }

    private static RoleDto ToDto(VaiTro x)
    {
        var dto = new RoleDto
        {
            MaVaiTro = x.MaVaiTro,
            TenVaiTro = x.TenVaiTro,
            MoTa = x.MoTa,
            SoNhanVien = x.NhanViens.Count,
            NhanVienThuoc = x.NhanViens.OrderBy(n => n.MaNV).Select(n => n.HoTen).Take(30).ToList(),
            Quyen = x.VaiTroQuyens
                .OrderBy(vq => vq.Quyen.MaQuyen)
                .Select(vq => ToPermissionDto(vq.Quyen.MaQuyen, vq.Quyen.TenQuyen, vq.Quyen.NhomChucNang))
                .ToList()
        };
        BoSungPhanTich(dto);
        return dto;
    }
}
