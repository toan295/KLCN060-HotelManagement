using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using KLCN060.Api.Auditing;
using KLCN060.Api.DTOs.Audit;
using KLCN060.Api.Middlewares;
using KLCN060.Api.Services.Finance;
using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public partial class AuditLogService
{
    private const int XuatToiDa = 20000;

    // Hành động do hệ thống tự thực hiện (không do một người bấm).
    private static readonly string[] HanhDongHeThong = { "TAI_KHOAN_BI_KHOA_TU_DONG" };

    // ===== Mô tả cho các dòng "chung" do middleware ghi: POST_/api/v1/bookings/BK1/cancel =====
    private static readonly (string Doan, string Ten)[] DanhTu =
    {
        ("bookings", "phiếu đặt phòng"), ("check-ins", "lượt nhận phòng"), ("stays", "lượt lưu trú"), ("rooms", "phòng"), ("room-types", "loại phòng"),
        ("services", "dịch vụ"), ("service-packages", "gói dịch vụ"), ("promotions", "khuyến mãi"), ("facilities", "cơ sở vật chất"), ("invoices", "hóa đơn"),
        ("payments", "thanh toán"), ("guests", "hồ sơ khách"), ("employees", "nhân viên"), ("roles", "vai trò / phân quyền"), ("rates", "bảng giá"),
        ("reconciliation", "đối soát"), ("housekeeping", "buồng phòng"),
        ("incidents", "sự cố"), ("room-checks", "kiểm phòng"), ("lost-found", "đồ bỏ quên"), ("supplies", "đồ dùng"), ("system", "sao lưu / hệ thống"),
        ("backups", "bản sao lưu"), ("audit-logs", "nhật ký hoạt động"), ("reports", "báo cáo"), ("users", "tài khoản")
    };

    private static readonly Dictionary<string, string> DongTuHanhDong = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cancel"] = "Hủy", ["confirm"] = "Xác nhận", ["confirm-deposit"] = "Xác nhận cọc", ["lock"] = "Khóa", ["unlock"] = "Mở khóa", ["pay"] = "Thanh toán",
        ["restore"] = "Phục hồi", ["restore-file"] = "Phục hồi từ file", ["verify"] = "Kiểm tra", ["approve"] = "Duyệt", ["reject"] = "Từ chối", ["check-out"] = "Trả phòng",
        ["export"] = "Xuất", ["preview"] = "Xem trước", ["transfer"] = "Điều chuyển", ["password"] = "Đặt lại mật khẩu", ["roles"] = "Đổi vai trò", ["change-room"] = "Đổi phòng",
        ["no-show"] = "Đánh dấu không đến", ["extend"] = "Gia hạn", ["status"] = "Đổi trạng thái", ["resolve"] = "Xử lý", ["close"] = "Đóng", ["discount"] = "Giảm giá"
    };

    /// <summary>"POST_/api/v1/bookings/BK1/cancel" → "Hủy phiếu đặt phòng (BK1)". Không nhận ra thì giữ nguyên để khỏi diễn giải sai.</summary>
    internal static string MoTaTuDuongDan(string hanhDong)
    {
        var gach = hanhDong.IndexOf('_');
        if (gach <= 0 || gach == hanhDong.Length - 1) return hanhDong;
        var phuongThuc = hanhDong[..gach].ToUpperInvariant();
        var duongDan = hanhDong[(gach + 1)..];
        if (phuongThuc is not ("POST" or "PUT" or "PATCH" or "DELETE" or "GET") || !duongDan.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)) return hanhDong;

        var doan = duongDan.Split('/', StringSplitOptions.RemoveEmptyEntries).Skip(2).ToList();   // bỏ "api", "v1"
        if (doan.Count == 0) return hanhDong;
        var danhTu = DanhTu.FirstOrDefault(x => x.Doan.Equals(doan[0], StringComparison.OrdinalIgnoreCase)).Ten;
        if (danhTu is null) return hanhDong;

        var dongTuRieng = doan.Skip(1).Select(d => DongTuHanhDong.TryGetValue(d, out var v) ? v : null).LastOrDefault(v => v is not null);
        var ma = doan.Skip(1).FirstOrDefault(d => d.Any(char.IsDigit) && !DongTuHanhDong.ContainsKey(d));
        var dongTu = dongTuRieng ?? phuongThuc switch { "POST" => "Tạo / thực hiện", "PUT" or "PATCH" => "Cập nhật", "DELETE" => "Xóa", _ => "Xem" };
        return $"{dongTu} {danhTu}" + (ma is null ? "" : $" ({ma})");
    }

    // ===== Tách "Trước → Sau" từ chi tiết =====
    private static readonly Dictionary<string, string> NhanTruong = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gia"] = "Giá", ["soTien"] = "Số tiền", ["tongTien"] = "Tổng tiền", ["conNo"] = "Còn nợ", ["trangThai"] = "Trạng thái", ["tinhTrang"] = "Tình trạng",
        ["lyDo"] = "Lý do", ["phong"] = "Phòng", ["khach"] = "Khách", ["phieuNhan"] = "Phiếu nhận", ["hoTen"] = "Họ tên", ["soDT"] = "Số điện thoại",
        ["chucVu"] = "Chức vụ", ["tenDangNhap"] = "Tên đăng nhập", ["vaiTro"] = "Vai trò", ["ten"] = "Tên", ["donVi"] = "Đơn vị", ["tienCoc"] = "Tiền cọc",
        ["hinhThuc"] = "Hình thức", ["thuBoi"] = "Thu bởi", ["loai"] = "Loại", ["yeuCau"] = "Yêu cầu", ["capThem"] = "Cấp thêm quyền", ["thuHoi"] = "Thu hồi quyền",
        ["nhanVienAnhHuong"] = "Số nhân viên bị ảnh hưởng", ["viTri"] = "Vị trí", ["soLuong"] = "Số lượng", ["nguoiDon"] = "Người dọn", ["nguoiGiao"] = "Người giao",
        ["thuTu"] = "Thứ tự", ["hienThi"] = "Hiển thị", ["tieuDe"] = "Tiêu đề", ["muc"] = "Mục", ["phucVu"] = "Phục vụ", ["nhomDoanhThu"] = "Nhóm doanh thu",
        ["ngay"] = "Khoảng ngày áp dụng", ["thu"] = "Thứ trong tuần", ["doiTuong"] = "Loại đặt phòng", ["uuTien"] = "Độ ưu tiên", ["bat"] = "Đang bật",
        ["loaiQuyTac"] = "Loại quy tắc", ["kieu"] = "Cách điều chỉnh", ["mucDieuChinh"] = "Mức điều chỉnh", ["loaiPhong"] = "Loại phòng", ["nhanVien"] = "Nhân viên", ["tenDangNhap"] = "Tên đăng nhập",
        ["giuLichSu"] = "Giữ lịch sử", ["tuDong"] = "Sao lưu tự động", ["chuKyGio"] = "Chu kỳ (giờ)", ["baoCao"] = "Báo cáo", ["dinhDang"] = "Định dạng", ["soDong"] = "Số dòng",
        ["tuNgay"] = "Từ ngày", ["denNgay"] = "Đến ngày", ["tong"] = "Tổng số dòng", ["nhom"] = "Nhóm nghiệp vụ", ["taiKhoan"] = "Tài khoản", ["ketQua"] = "Kết quả", ["tuKhoa"] = "Từ khóa",
        ["thuHoiPhien"] = "Thu hồi phiên đăng nhập", ["matKhauTuSinh"] = "Mật khẩu do hệ thống sinh", ["moKhoa"] = "Mở khóa kèm theo", ["batDauViecDuocGiao"] = "Bắt đầu việc được giao",
        ["ngayDonDuKien"] = "Ngày đón dự kiến", ["matCoc"] = "Mất cọc", ["thamSo"] = "Tham số", ["httpStatus"] = "Mã HTTP", ["maLoi"] = "Mã lỗi",
        ["soDongHaoHut"] = "Số loại đồ dùng hao hụt", ["choTinhPhi"] = "Tiền chờ tính phí (đ)", ["mucDo"] = "Mức độ", ["viTriCu"] = "Vị trí cũ", ["nguoiNhat"] = "Người nhặt",
        ["moTa"] = "Mô tả", ["nguoiNhan"] = "Người nhận", ["giayTo"] = "Giấy tờ", ["luuGiu"] = "Thời gian lưu giữ", ["dinhMuc"] = "Định mức", ["vatDung"] = "Vật dụng",
        ["thanhTien"] = "Thành tiền (đ)", ["dongDichVu"] = "Dòng dịch vụ", ["suCo"] = "Sự cố", ["nguoiThucHien"] = "Người thực hiện", ["dichVu"] = "Dịch vụ gắn kèm", ["lyDo/ghiChu"] = "Lý do / ghi chú"
    };

    private static string Nhan(string khoa)
    {
        khoa = khoa.Trim();
        if (NhanTruong.TryGetValue(khoa, out var nhan)) return nhan;
        // Mã viết hoa kiểu DOANH_NGHIEP / giá trị không phải tên trường: đọc thành chữ thường, không tách từng ký tự.
        if (khoa.Contains('_') || !khoa.Any(char.IsLower))
        {
            var chu = khoa.Replace('_', ' ').ToLowerInvariant();
            return char.ToUpperInvariant(chu[0]) + chu[1..];
        }
        var sb = new StringBuilder();
        foreach (var c in khoa) { if (char.IsUpper(c) && sb.Length > 0) sb.Append(' '); sb.Append(sb.Length == 0 ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c)); }
        return sb.ToString();
    }

    private static string GiaTri(string? v)
    {
        v = v?.Trim();
        return v switch
        {
            null => "", "True" or "true" => "Có", "False" or "false" => "Không",
            "HOAT_DONG" => "Đang hoạt động", "BI_KHOA" => "Bị khóa", "DA_XOA" => "Đã xóa",
            "khong co khach" => "Phòng không có khách", "chua luu kho" => "Chưa lưu kho", "tat ca" => "Tất cả", "khong" => "Không", _ => v
        };
    }

    private static List<string> ChiaDoan(string s)
    {
        var kq = new List<string>();
        var sb = new StringBuilder();
        var sau = 0;
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '[' || c == '(') sau++;
            else if ((c == ']' || c == ')') && sau > 0) sau--;
            if (sau == 0 && c == ';' )
            {
                kq.Add(sb.ToString().Trim()); sb.Clear();
                continue;
            }
            sb.Append(c);
        }
        if (sb.Length > 0) kq.Add(sb.ToString().Trim());
        return kq.Where(x => x.Length > 0).ToList();
    }

    private static Dictionary<string, string> CapKhoaGiaTri(string s)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var seg in ChiaDoan(s))
        {
            var eq = seg.IndexOf('=');
            if (eq > 0) d[seg[..eq].Trim()] = seg[(eq + 1)..].Trim();
            else d[seg] = "";
        }
        return d;
    }

    private static readonly Regex TruocSauNgoac = new(@"^truoc\s*[=:]?\s*\[(?<a>.*?)\]\s*(?:;|->|\|)?\s*sau\s*[=:]?\s*\[(?<b>.*)\]\s*(?<r>.*)$", RegexOptions.Singleline | RegexOptions.IgnoreCase);
    private static readonly Regex TruocSauThanh = new(@"^truoc\s*:\s*(?<a>.*?)\s*\|\s*sau\s*:\s*(?<b>.*)$", RegexOptions.Singleline | RegexOptions.IgnoreCase);

    internal static (List<AuditChangeDto> ThayDoi, List<AuditFieldDto> ThongTin, string? LyDo) PhanTichChiTiet(string? chiTiet, string nhanMacDinh = "Giá trị")
    {
        var thayDoi = new List<AuditChangeDto>();
        var thongTin = new List<AuditFieldDto>();
        string? lyDo = null;
        if (string.IsNullOrWhiteSpace(chiTiet)) return (thayDoi, thongTin, lyDo);

        var ct = chiTiet.Trim();
        var m = TruocSauNgoac.Match(ct);
        if (!m.Success) m = TruocSauThanh.Match(ct);
        if (m.Success)
        {
            var a = CapKhoaGiaTri(m.Groups["a"].Value);
            var b = CapKhoaGiaTri(m.Groups["b"].Value);
            foreach (var khoa in a.Keys.Union(b.Keys))
            {
                a.TryGetValue(khoa, out var tr); b.TryGetValue(khoa, out var sa);
                if (khoa.Equals("lyDo", StringComparison.OrdinalIgnoreCase)) { lyDo = sa ?? tr; continue; }
                if (tr != sa) thayDoi.Add(new AuditChangeDto { Truong = Nhan(khoa), Truoc = GiaTri(tr), Sau = GiaTri(sa) });
                else if (!string.IsNullOrWhiteSpace(tr)) thongTin.Add(new AuditFieldDto { Truong = Nhan(khoa), GiaTri = GiaTri(tr) });
            }
            ct = m.Groups.TryGetValue("r", out var r) ? r.Value.Trim(' ', ';') : "";
            if (ct.Length == 0) return (thayDoi, thongTin, lyDo);
        }

        var cacDoan = ChiaDoan(ct);
        var cap = CapKhoaGiaTri(string.Join("; ", cacDoan));
        var daXuLy = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Cặp xxxCu / xxxMoi → một dòng thay đổi
        foreach (var khoa in cap.Keys.Where(k => k.EndsWith("Cu", StringComparison.Ordinal) && k.Length > 2).ToList())
        {
            var goc = khoa[..^2];
            if (cap.TryGetValue(goc + "Moi", out var moi))
            {
                thayDoi.Add(new AuditChangeDto { Truong = Nhan(goc), Truoc = GiaTri(cap[khoa]), Sau = GiaTri(moi) });
                daXuLy.Add(khoa); daXuLy.Add(goc + "Moi");
            }
        }

        foreach (var doan in cacDoan)
        {
            var eq = doan.IndexOf('=');
            var khoaTho = eq > 0 ? doan[..eq].Trim() : "";
            if (khoaTho.Length > 0 && daXuLy.Contains(khoaTho)) continue;

            var mui = doan.IndexOf("->", StringComparison.Ordinal);
            if (mui > 0)
            {
                var trai = doan[..mui].Trim();
                var phai = doan[(mui + 2)..].Trim();
                var nhan = nhanMacDinh; var truoc = trai;
                var hai = trai.IndexOf(": ", StringComparison.Ordinal);
                var bang = trai.IndexOf('=');
                if (bang > 0 && (hai < 0 || bang < hai)) { nhan = Nhan(trai[..bang]); truoc = trai[(bang + 1)..]; }
                else if (hai > 0) { nhan = Nhan(trai[..hai]); truoc = trai[(hai + 2)..]; }
                thayDoi.Add(new AuditChangeDto { Truong = nhan, Truoc = GiaTri(truoc), Sau = GiaTri(phai) });
                continue;
            }
            if (eq > 0)
            {
                var khoa = doan[..eq].Trim(); var giaTri = doan[(eq + 1)..].Trim();
                if (khoa.Equals("lyDo", StringComparison.OrdinalIgnoreCase)) { lyDo = giaTri; continue; }
                if (giaTri.Length == 0) continue;   // trường để trống (ví dụ bộ lọc không chọn) thì không hiển thị
                thongTin.Add(new AuditFieldDto { Truong = Nhan(khoa), GiaTri = GiaTri(giaTri) });
                continue;
            }
            thongTin.Add(new AuditFieldDto { Truong = "Ghi chú", GiaTri = doan });
        }
        return (thayDoi, thongTin, lyDo);
    }

    private static readonly Regex MaDoiTuong = new(@"\b(BK\d+|HD\d+|CI\d+|PN\d+|KH\d+|NV\d+|QT\d+|DQ\d+|KQ\d+|SC\d+|HT\d+|GD\w{3,}|P\d{3,4})\b", RegexOptions.Compiled);

    internal static List<string> TrichDoiTuong(string? doiTuong, string? chiTiet)
    {
        var kq = new List<string>();
        void Them(string? s)
        {
            if (string.IsNullOrEmpty(s)) return;
            foreach (Match m in MaDoiTuong.Matches(s)) if (!kq.Contains(m.Value, StringComparer.OrdinalIgnoreCase)) kq.Add(m.Value);
        }
        Them(doiTuong); Them(chiTiet);
        return kq.Take(12).ToList();
    }

    private static string NhanMacDinhTheoHanhDong(string hanhDong) => hanhDong switch
    {
        "KHOA_TAI_KHOAN_NHAN_VIEN" or "MO_KHOA_TAI_KHOAN_NHAN_VIEN" or "XOA_NHAN_VIEN" => "Trạng thái tài khoản",
        "DOI_VAI_TRO_NHAN_VIEN" => "Vai trò",
        "DOI_TRANG_THAI_DICH_VU" or "DOI_TRANG_THAI_GOI_DICH_VU" or "DOI_TRANG_THAI_SU_CO" => "Trạng thái",
        "DOI_NHOM_DOANH_THU_DICH_VU" => "Nhóm doanh thu",
        "DOI_TINH_TRANG_CSVC" or "DOI_TINH_TRANG_PHONG" => "Tình trạng",
        "GIAO_DON_PHONG" => "Người dọn",
        _ => "Giá trị"
    };

    private void DienChiTiet(AuditLogDto dto)
    {
        var (thayDoi, thongTin, lyDo) = PhanTichChiTiet(dto.ChiTiet, NhanMacDinhTheoHanhDong(dto.HanhDong));
        dto.ThayDoi = thayDoi; dto.ThongTin = thongTin; dto.LyDo = lyDo;
        dto.DoiTuongLienQuan = TrichDoiTuong(dto.DoiTuongTacDong, dto.ChiTiet);
    }

    // ===== Chuỗi sự kiện + tình trạng phòng của cùng đối tượng =====
    public async Task<AuditRelatedDto?> GetRelatedAsync(long maNhatKy, bool chiTaiChinh = false)
    {
        var goc = await Loc(_context.NhatKyThaoTacs.AsNoTracking(), chiTaiChinh).FirstOrDefaultAsync(x => x.MaNhatKy == maNhatKy);
        if (goc is null) return null;

        var kq = new AuditRelatedDto { MaNhatKy = maNhatKy, DoiTuong = goc.DoiTuongTacDong };
        var lienQuan = TrichDoiTuong(goc.DoiTuongTacDong, goc.ChiTiet);
        kq.DoiTuongLienQuan = lienQuan;

        // Khóa chuỗi sự kiện: mã đối tượng của dòng (nếu là mã nghiệp vụ, không phải đường dẫn API); không có thì mã đầu tiên trích được.
        var khoa = !string.IsNullOrEmpty(goc.DoiTuongTacDong) && !goc.DoiTuongTacDong.StartsWith('/') ? goc.DoiTuongTacDong : lienQuan.FirstOrDefault();
        if (!string.IsNullOrEmpty(khoa))
        {
            var dong = await Loc(_context.NhatKyThaoTacs.AsNoTracking(), chiTaiChinh)
                .Where(x => x.DoiTuongTacDong == khoa || (x.DoiTuongTacDong != null && x.DoiTuongTacDong.Contains("/" + khoa)) || (x.ChiTiet != null && x.ChiTiet.Contains(khoa)))
                .OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.MaNhatKy).Take(100).ToListAsync();
            kq.ChuoiSuKien = (await ToDtosAsync(dong)).OrderBy(x => x.ThoiGian).ThenBy(x => x.MaNhatKy).ToList();

            // Tình trạng phòng: nếu đối tượng là mã phòng
            if (!chiTaiChinh)
            {
                var ls = await _context.LichSuTinhTrangPhongs.AsNoTracking().Where(x => x.MaPhong == khoa).OrderByDescending(x => x.ThoiGian).Take(30).ToListAsync();
                kq.LichSuTinhTrangPhong = ls.OrderBy(x => x.ThoiGian).Select(x => new AuditRoomStatusDto
                {
                    MaPhong = x.MaPhong, TuTinhTrang = x.TuTinhTrang.ToString(), DenTinhTrang = x.DenTinhTrang.ToString(), ThoiGian = x.ThoiGian, NguoiThucHien = x.MaTaiKhoan, LyDo = x.LyDo
                }).ToList();
            }
        }
        else kq.ChuoiSuKien = (await ToDtosAsync(new List<NhatKyThaoTac> { goc }));
        return kq;
    }

    // ===== Xuất nhật ký (có kiểm soát quyền; việc xuất cũng được ghi lại) =====
    /// <summary>
    /// CSV UTF-8 có BOM, mở thẳng bằng Excel: khối tiêu đề (kỳ, bộ lọc, người / giờ xuất), khối TỔNG HỢP (kết quả, nguồn, số người), bảng THEO NHÓM NGHIỆP VỤ,
    /// rồi bảng CHI TIẾT có STT, người thực hiện "Họ tên (Mã NV)", nhóm nghiệp vụ, kết quả, mã lỗi / lý do thất bại, lý do thao tác, thay đổi trước → sau và thông tin thao tác
    /// (cột nào không có dữ liệu ghi "—"). Không xuất địa chỉ IP.
    /// </summary>
    public async Task<(byte[] Noi, int SoDong, bool BiCat)> XuatCsvAsync(AuditLogQuery query, string tenDN, bool chiTaiChinh = false, bool xlsx = false)
    {
        var q = LocBoLoc(query, chiTaiChinh);
        var tong = await q.CountAsync();
        var dong = await q.OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.MaNhatKy).Take(XuatToiDa).ToListAsync();
        var dtos = await ToDtosAsync(dong);
        var tenNguoiXuat = await TenNguoiDung.HienThiAsync(_context, tenDN) ?? tenDN;
        var vi = CultureInfo.GetCultureInfo("vi-VN");
        const string KhongCo = "—";
        static string N(string? s) => string.IsNullOrWhiteSpace(s) ? KhongCo : s;
        string Nguoi(AuditLogDto d) => string.IsNullOrEmpty(d.HoTenNguoiThucHien) ? d.TaiKhoan : $"{d.HoTenNguoiThucHien} ({d.MaNguoiThucHien ?? d.TaiKhoan})";
        string TenNhom(string ma) => Nhoms.FirstOrDefault(x => x.Ma == ma).Ten ?? (ma == "KHAC" ? "Khác (chưa phân nhóm)" : ma == "HE_THONG" ? "Hệ thống" : ma);

        var loc = new List<string>();
        if (!string.IsNullOrWhiteSpace(query.Nhom)) loc.Add("Nhóm: " + TenNhom(query.Nhom));
        if (!string.IsNullOrWhiteSpace(query.TaiKhoan)) loc.Add("Tài khoản: " + query.TaiKhoan);
        if (!string.IsNullOrWhiteSpace(query.KetQua)) loc.Add("Kết quả: " + KetQuaChu(query.KetQua.ToUpperInvariant()));
        if (!string.IsNullOrWhiteSpace(query.Nguon)) loc.Add("Nguồn: " + (query.Nguon.Equals("HE_THONG", StringComparison.OrdinalIgnoreCase) ? "Hệ thống tự động" : "Người dùng"));
        if (!string.IsNullOrWhiteSpace(query.DoiTuong)) loc.Add("Đối tượng: " + query.DoiTuong);
        if (!string.IsNullOrWhiteSpace(query.Q)) loc.Add("Từ khóa: " + query.Q);

        var sb = new StringBuilder();
        void Hang(params string[] o) => sb.AppendLine(string.Join(",", o));
        Hang(Csv("NHẬT KÝ HOẠT ĐỘNG HỆ THỐNG"));
        Hang(Csv("Kỳ báo cáo"), Csv($"{(query.TuNgay is null ? "từ đầu" : query.TuNgay.Value.ToString("dd/MM/yyyy"))} - {(query.DenNgay is null ? "nay" : query.DenNgay.Value.ToString("dd/MM/yyyy"))}"));
        Hang(Csv("Bộ lọc"), Csv(loc.Count == 0 ? "Không lọc (tất cả)" : string.Join("; ", loc)));
        Hang(Csv("Xuất lúc"), Csv(DateTime.Now.ToString("dd/MM/yyyy HH:mm", vi)), Csv("Người xuất"), Csv(tenNguoiXuat));
        Hang(Csv("Số dòng trong file"), dtos.Count.ToString(CultureInfo.InvariantCulture), Csv("Tổng số dòng khớp bộ lọc"), tong.ToString(CultureInfo.InvariantCulture));
        if (tong > dtos.Count) Hang(Csv($"Lưu ý: chỉ xuất {XuatToiDa:N0} dòng mới nhất, hãy thu hẹp bộ lọc / khoảng ngày để lấy phần còn lại."));
        sb.AppendLine();

        Hang(Csv("TỔNG HỢP"), Csv("Số dòng"));
        Hang(Csv("Thành công"), dtos.Count(x => x.KetQua == "THANH_CONG").ToString(CultureInfo.InvariantCulture));
        Hang(Csv("Thất bại"), dtos.Count(x => x.KetQua == "THAT_BAI").ToString(CultureInfo.InvariantCulture));
        Hang(Csv("Bị từ chối (không đủ quyền)"), dtos.Count(x => x.KetQua == "TU_CHOI").ToString(CultureInfo.InvariantCulture));
        Hang(Csv("Do người dùng thực hiện"), dtos.Count(x => x.NguonThucHien != "HE_THONG").ToString(CultureInfo.InvariantCulture));
        Hang(Csv("Do hệ thống tự động"), dtos.Count(x => x.NguonThucHien == "HE_THONG").ToString(CultureInfo.InvariantCulture));
        Hang(Csv("Số người thực hiện khác nhau"), dtos.Select(x => x.TaiKhoan).Distinct().Count().ToString(CultureInfo.InvariantCulture));
        sb.AppendLine();

        Hang(Csv("THEO NHÓM NGHIỆP VỤ"), Csv("Số dòng"), Csv("Thất bại / từ chối"));
        foreach (var g in dtos.GroupBy(x => x.Nhom).OrderByDescending(g => g.Count()))
            Hang(Csv(TenNhom(g.Key)), g.Count().ToString(CultureInfo.InvariantCulture), g.Count(x => x.KetQua != "THANH_CONG").ToString(CultureInfo.InvariantCulture));
        sb.AppendLine();

        Hang(Csv("CHI TIẾT"));
        Hang(new[] { "STT", "Mã nhật ký", "Thời gian", "Người thực hiện", "Vai trò", "Nguồn", "Nhóm nghiệp vụ", "Hành động", "Đối tượng", "Kết quả", "Mã lỗi / lý do thất bại", "Lý do thao tác", "Thay đổi (trước → sau)", "Thông tin thao tác", "Mã hành động" }.Select(Csv).ToArray());
        var stt = 0;
        foreach (var d in dtos)
        {
            var thayDoi = string.Join(" | ", d.ThayDoi.Select(t => $"{t.Truong}: {(string.IsNullOrEmpty(t.Truoc) ? "(trống)" : t.Truoc)} → {(string.IsNullOrEmpty(t.Sau) ? "(trống)" : t.Sau)}"));
            var thongTin = string.Join(" | ", d.ThongTin.Select(t => $"{t.Truong}: {t.GiaTri}"));
            var maLoi = d.KetQua == "THANH_CONG" ? KhongCo : N(d.MaLoi);
            Hang(
                (++stt).ToString(CultureInfo.InvariantCulture), d.MaNhatKy.ToString(CultureInfo.InvariantCulture), Csv(d.ThoiGian.ToString("dd/MM/yyyy HH:mm:ss", vi)), Csv(Nguoi(d)),
                Csv(string.IsNullOrEmpty(d.VaiTro) ? KhongCo : TenVaiTro(d.VaiTro)), Csv(d.NguonThucHien == "HE_THONG" ? "Hệ thống" : "Người dùng"), Csv(TenNhom(d.Nhom)), Csv(d.MoTaHanhDong),
                Csv(N(d.DoiTuongTacDong)), Csv(KetQuaChu(d.KetQua)), Csv(maLoi), Csv(N(d.LyDo)), Csv(N(thayDoi)), Csv(N(thongTin)), Csv(d.HanhDong));
        }

        // Việc xuất nhật ký tự nó cũng là một thao tác nhạy cảm: ghi lại ai xuất, bộ lọc, số dòng.
        _context.NhatKyThaoTacs.Add(new NhatKyThaoTac
        {
            MaTaiKhoan = tenDN, HanhDong = "XUAT_NHAT_KY", DoiTuongTacDong = "audit-logs", ThoiGian = DateTime.Now,
            ChiTiet = $"soDong={dtos.Count}; tong={tong}; tuNgay={query.TuNgay:yyyy-MM-dd}; denNgay={query.DenNgay:yyyy-MM-dd}; nhom={query.Nhom}; taiKhoan={query.TaiKhoan}; ketQua={query.KetQua}; tuKhoa={query.Q}",
            KetQua = "THANH_CONG"
        });
        await _context.SaveChangesAsync();
        if (xlsx) return (DungXlsx(query, dtos, tong, loc, tenNguoiXuat, TenNhom, Nguoi), dtos.Count, tong > dtos.Count);
        return (new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(), dtos.Count, tong > dtos.Count);
    }

    /// <summary>Excel 2 trang tính: "Tổng hợp" (thẻ chỉ số + theo nhóm nghiệp vụ) và "Chi tiết" (từng dòng, có nút lọc, cố định dòng đầu cột, thời gian là ngày giờ thật).</summary>
    private static byte[] DungXlsx(AuditLogQuery query, List<AuditLogDto> dtos, int tong, List<string> loc, string tenNguoiXuat, Func<string, string> tenNhom, Func<AuditLogDto, string> nguoi)
    {
        const string KhongCo = "—";
        string N(string? s) => string.IsNullOrWhiteSpace(s) ? KhongCo : s;
        var tl = new TaiLieuBaoCao
        {
            TieuDe = "Nhật ký hoạt động hệ thống",
            KyBaoCao = $"Kỳ: {(query.TuNgay is null ? "từ đầu" : query.TuNgay.Value.ToString("dd/MM/yyyy"))} - {(query.DenNgay is null ? "nay" : query.DenNgay.Value.ToString("dd/MM/yyyy"))} · Bộ lọc: {(loc.Count == 0 ? "không lọc (tất cả)" : string.Join("; ", loc))}",
            NguoiXuat = tenNguoiXuat,
            XuatLuc = DateTime.Now
        };
        int Dem(Func<AuditLogDto, bool> f) => dtos.Count(f);
        var thatBai = Dem(x => x.KetQua != "THANH_CONG");
        var th = new PhanBaoCao("Tổng hợp", "Tổng hợp", "Số dòng nhật ký theo kết quả, nguồn và nhóm nghiệp vụ trong phạm vi đã lọc.", new List<KhoiBaoCao>());
        th.Khois.Add(new KhoiThe(new List<TheSo>
        {
            new("Số dòng trong file", $"{dtos.Count:N0}", tong > dtos.Count ? $"Chỉ xuất {XuatToiDa:N0} dòng mới nhất trong {tong:N0} dòng khớp bộ lọc" : $"Bằng tổng số dòng khớp bộ lọc ({tong:N0})", tong > dtos.Count ? "xau" : ""),
            new("Thành công", $"{Dem(x => x.KetQua == "THANH_CONG"):N0}", null, "tot"),
            new("Thất bại", $"{Dem(x => x.KetQua == "THAT_BAI"):N0}", null, Dem(x => x.KetQua == "THAT_BAI") > 0 ? "xau" : ""),
            new("Bị từ chối (không đủ quyền)", $"{Dem(x => x.KetQua == "TU_CHOI"):N0}", null, Dem(x => x.KetQua == "TU_CHOI") > 0 ? "xau" : ""),
            new("Do người dùng thực hiện", $"{Dem(x => x.NguonThucHien != "HE_THONG"):N0}"),
            new("Do hệ thống tự động", $"{Dem(x => x.NguonThucHien == "HE_THONG"):N0}"),
            new("Số người thực hiện khác nhau", $"{dtos.Select(x => x.TaiKhoan).Distinct().Count():N0}")
        }));
        var nhom = new BangBaoCao
        {
            TieuDe = "Theo nhóm nghiệp vụ",
            PhuDe = "Sắp theo số dòng giảm dần.",
            Cot = { new("Nhóm nghiệp vụ", KieuCot.Chu), new("Số dòng", KieuCot.So), new("Thất bại / từ chối", KieuCot.So) }
        };
        foreach (var g in dtos.GroupBy(x => x.Nhom).OrderByDescending(g => g.Count()))
            nhom.ThemDong(tenNhom(g.Key), g.Count(), g.Count(x => x.KetQua != "THANH_CONG"));
        nhom.Tong = new object?[] { "Tổng", dtos.Count, thatBai };
        th.Khois.Add(new KhoiBang(nhom));
        th.Khois.Add(new KhoiGhiChu("Cách đọc", new List<string>
        {
            "Trang \"Chi tiết\" liệt kê từng thao tác, mới nhất ở trên; dùng nút lọc ở dòng đầu cột để lọc theo người, nhóm, kết quả...",
            "Cột \"Thay đổi\" ghi giá trị trước → sau của từng trường; cột \"Thông tin thao tác\" ghi các thông tin đi kèm.",
            "Ô ghi \"—\" nghĩa là thao tác đó không có nội dung ở cột này (ví dụ thành công thì không có mã lỗi)."
        }));
        tl.Phans.Add(th);

        var ct = new BangBaoCao
        {
            TieuDe = "Chi tiết từng dòng nhật ký",
            PhuDe = "Mới nhất ở trên. Không có địa chỉ IP.",
            Cot =
            {
                new("STT", KieuCot.So), new("Mã nhật ký", KieuCot.So), new("Thời gian", KieuCot.NgayGio), new("Người thực hiện"), new("Vai trò"), new("Nguồn"), new("Nhóm nghiệp vụ"),
                new("Hành động"), new("Đối tượng"), new("Kết quả"), new("Mã lỗi / lý do thất bại"), new("Lý do thao tác"), new("Thay đổi (trước → sau)"), new("Thông tin thao tác"), new("Mã hành động")
            }
        };
        var stt = 0;
        foreach (var d in dtos)
        {
            var thayDoi = string.Join("\n", d.ThayDoi.Select(t => $"{t.Truong}: {(string.IsNullOrEmpty(t.Truoc) ? "(trống)" : t.Truoc)} → {(string.IsNullOrEmpty(t.Sau) ? "(trống)" : t.Sau)}"));
            var thongTin = string.Join("\n", d.ThongTin.Select(t => $"{t.Truong}: {t.GiaTri}"));
            ct.ThemDong(++stt, (int)Math.Min(d.MaNhatKy, int.MaxValue), d.ThoiGian, nguoi(d), string.IsNullOrEmpty(d.VaiTro) ? KhongCo : TenVaiTro(d.VaiTro),
                d.NguonThucHien == "HE_THONG" ? "Hệ thống" : "Người dùng", tenNhom(d.Nhom), d.MoTaHanhDong, N(d.DoiTuongTacDong), KetQuaChu(d.KetQua),
                d.KetQua == "THANH_CONG" ? KhongCo : N(d.MaLoi), N(d.LyDo), N(thayDoi), N(thongTin), d.HanhDong);
        }
        tl.Phans.Add(new PhanBaoCao("Chi tiết", "Chi tiết", $"{dtos.Count:N0} dòng.", new List<KhoiBaoCao> { new KhoiBang(ct) }, true));
        return ManagementReportWriter.ToXlsx(tl);
    }

    private static string TenVaiTro(string? v) => v switch { "QUAN_LY" => "Quản lý", "LE_TAN" => "Lễ tân", "KE_TOAN" => "Kế toán", "BUONG_PHONG" => "Buồng phòng", "KHACH_HANG" => "Khách hàng", _ => v ?? "" };

    internal static string KetQuaChu(string ketQua) => ketQua switch { "THANH_CONG" => "Thành công", "THAT_BAI" => "Thất bại", "TU_CHOI" => "Bị từ chối (không đủ quyền)", _ => ketQua };

    // Chống "CSV injection": ô bắt đầu bằng = + - @ bị Excel hiểu là công thức.
    private static string Csv(string v)
    {
        v ??= "";
        if (v.Length > 0 && "=+-@\t\r".Contains(v[0])) v = "'" + v;
        return "\"" + v.Replace("\"", "\"\"") + "\"";
    }

    // ===== Kiểm tra toàn vẹn =====
    public async Task<AuditIntegrityDto> KiemTraToanVenAsync(DateOnly? tuNgay = null)
    {
        var khoa = AuditSaveChangesInterceptor.KhoaKy(_configuration ?? new ConfigurationBuilder().Build());
        var q = _context.NhatKyThaoTacs.AsNoTracking().AsQueryable();
        if (tuNgay is { } tu) { var tuLuc = tu.ToDateTime(TimeOnly.MinValue); q = q.Where(x => x.ThoiGian >= tuLuc); }

        var dto = new AuditIntegrityDto { KiemTraLuc = DateTime.Now };
        // Dòng "cũ" (chưa có chữ ký) chỉ hợp lệ khi nằm TRƯỚC dòng có chữ ký đầu tiên; dòng không chữ ký nằm sau đó
        // nghĩa là chữ ký đã bị gỡ để che việc sửa nội dung -> tính là sai.
        var maKyDauTien = await _context.NhatKyThaoTacs.AsNoTracking().Where(x => x.ChuKy != null && x.ChuKy != "").Select(x => (long?)x.MaNhatKy).MinAsync();
        long truoc = 0;
        // Duyệt theo lô theo mã tăng dần: không nạp cả bảng vào bộ nhớ.
        const int lo = 2000;
        long sauMa = 0;
        while (true)
        {
            var ds = await q.Where(x => x.MaNhatKy > sauMa).OrderBy(x => x.MaNhatKy).Take(lo).ToListAsync();
            if (ds.Count == 0) break;
            foreach (var x in ds)
            {
                dto.TongSoDong++;
                if (string.IsNullOrEmpty(x.ChuKy))
                {
                    if (maKyDauTien is { } ky && x.MaNhatKy > ky) { dto.ChuKySai++; if (dto.MaDongSai.Count < 50) dto.MaDongSai.Add(x.MaNhatKy); }
                    else dto.ChuaCoChuKy++;
                }
                else if (string.Equals(x.ChuKy, AuditSaveChangesInterceptor.TinhChuKy(khoa, x), StringComparison.OrdinalIgnoreCase)) dto.ChuKyHopLe++;
                else { dto.ChuKySai++; if (dto.MaDongSai.Count < 50) dto.MaDongSai.Add(x.MaNhatKy); }
                if (truoc != 0 && x.MaNhatKy - truoc > 1) dto.SoKhoangTrongMa++;
                truoc = x.MaNhatKy;
            }
            sauMa = ds[^1].MaNhatKy;
        }
        dto.ToanVen = dto.ChuKySai == 0;
        dto.KetLuan = dto.ChuKySai > 0
            ? $"PHÁT HIỆN {dto.ChuKySai} dòng nhật ký có nội dung KHÔNG khớp chữ ký - có thể đã bị sửa trực tiếp trong CSDL."
            : $"Toàn vẹn: {dto.ChuKyHopLe:N0}/{dto.TongSoDong:N0} dòng có chữ ký đúng" + (dto.ChuaCoChuKy > 0 ? $"; {dto.ChuaCoChuKy:N0} dòng cũ (trước khi có chữ ký) không kiểm tra được" : "")
              + (dto.SoKhoangTrongMa > 0 ? $"; có {dto.SoKhoangTrongMa} khoảng trống trong dãy mã (dòng đã bị xóa, hoặc giao dịch bị hủy giữa chừng)" : "") + ".";
        return dto;
    }
}
