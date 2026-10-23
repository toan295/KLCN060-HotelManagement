using KLCN060.Api.Auditing;
using KLCN060.Api.DTOs.Reports;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public partial class AlertService
{
    /// <summary>Thông tin xử lý chuẩn của từng loại cảnh báo: ai xử lý, hạn (giờ) tính từ lúc phát hiện, ảnh hưởng, cách xử lý, nút điều hướng.</summary>
    private sealed record QuyTrinh(string NguoiXuLy, int HanGio, string AnhHuong, string CachXuLy, string NutMo);

    private static readonly Dictionary<string, QuyTrinh> QuyTrinhTheoMa = new()
    {
        ["BAO_TRI_QUA_LAU"] = new("Quản lý + bộ phận bảo trì", 24, "Phòng không bán được: mất doanh thu mỗi đêm và kéo công suất xuống.",
            "1) Xem lý do bảo trì và tiến độ sửa. 2) Giao/nhắc bộ phận sửa chữa, đặt hạn. 3) Khi xong, mở lại phòng (ghi lý do) để đưa vào bán.", "Mở Sơ đồ phòng"),
        ["PHONG_CHUA_DON"] = new("Buồng phòng", 4, "Phòng trống nhưng chưa sẵn sàng nên không nhận khách được; nguy cơ trễ giờ nhận phòng.",
            "1) Giao việc dọn cho nhân viên buồng phòng. 2) Theo dõi đến khi phòng được kiểm tra (VI). 3) Ưu tiên phòng có khách đến sớm.", "Mở Buồng phòng"),
        ["SU_CO_MUC_CAO"] = new("Quản lý + bộ phận bảo trì", 4, "Khách có thể bị ảnh hưởng ngay; có thể phải đổi phòng hoặc đền bù.",
            "1) Tiếp nhận sự cố, xác định phòng có thể tiếp tục bán không. 2) Giao sửa, đặt hạn. 3) Nếu có khách: đổi phòng hoặc thông báo.", "Mở Buồng phòng"),
        ["SU_CO_CHUA_TIEP_NHAN"] = new("Buồng phòng", 4, "Sự cố nhỏ để lâu có thể thành lớn; nhân viên báo mà không ai phản hồi.",
            "1) Tiếp nhận sự cố. 2) Giao người xử lý. 3) Đóng sự cố khi xong.", "Mở Buồng phòng"),
        ["DO_BO_QUEN_DEN_HAN"] = new("Buồng phòng", 72, "Chiếm kho, rủi ro khiếu nại nếu khách quay lại tìm.",
            "1) Liên hệ khách theo thông tin lưu. 2) Nếu không ai nhận: thanh lý theo quy định.", "Mở Buồng phòng"),
        ["BOOKING_VIP"] = new("Lễ tân + Quản lý", 24, "Khách giá trị cao: trải nghiệm kém ảnh hưởng doanh thu và uy tín.",
            "1) Chọn phòng tốt, đảm bảo đã dọn và kiểm tra. 2) Dặn lễ tân chuẩn bị đón tiếp. 3) Xem lại yêu cầu đặc biệt của khách.", "Mở Thông tin đặt phòng"),
        ["KHONG_DEN_CHO_XU_LY"] = new("Lễ tân", 24, "Phòng bị giữ vô ích, mất cơ hội bán lại; sai lệch công suất dự báo.",
            "1) Liên hệ khách xác nhận. 2) Nếu không đến: đánh dấu \"không đến\" (xử lý cọc theo chính sách).", "Mở Thông tin đặt phòng"),
        ["TRA_PHONG_QUA_HAN"] = new("Lễ tân", 4, "Có thể trùng lịch với khách đến sau; phát sinh tiền phòng chưa tính.",
            "1) Liên hệ khách: gia hạn hoặc trả phòng. 2) Nếu gia hạn: cập nhật ngày trả. 3) Kiểm tra phiếu đặt kế tiếp của phòng.", "Mở Bảng điều hành lưu trú"),
        ["OVERBOOKING"] = new("Quản lý + Lễ tân", 2, "Hai khách cùng được hứa một phòng - mất uy tín, phải đền bù.",
            "1) Xác định phiếu nào được giữ phòng. 2) Đổi phòng cho phiếu còn lại (cùng hạng hoặc cao hơn). 3) Thông báo khách.", "Mở Thông tin đặt phòng"),
        ["PHONG_GIU_KHONG_SAN_SANG"] = new("Lễ tân + Buồng phòng", 2, "Khách đến hôm nay không có phòng giao đúng hẹn.",
            "1) Đổi phòng cho phiếu sang phòng sẵn sàng. 2) Hoặc đẩy nhanh dọn / sửa phòng đã giữ.", "Mở Thông tin đặt phòng"),
        ["HOAN_COC_LON"] = new("Quản lý", 24, "Khoản tiền lớn trả lại khách - rủi ro sai chính sách hoặc gian lận.",
            "1) Đối chiếu lý do hủy và chính sách hoàn cọc. 2) Kiểm tra người hủy phiếu. 3) Ghi chú nếu có ngoại lệ.", "Mở Thông tin đặt phòng"),
        ["TY_LE_HUY_TANG"] = new("Quản lý (kinh doanh)", 72, "Doanh thu dự kiến giảm, phòng bỏ trống; có thể do giá / nguồn đặt / chính sách.",
            "1) Xem nguồn đặt và loại phòng bị hủy nhiều. 2) Xem lại giá, chính sách hủy. 3) Liên hệ kênh đặt phòng nếu cần.", "Mở Thông tin đặt phòng"),
        ["DOI_SOAT_LOI"] = new("Kế toán", 24, "Khách đã chuyển tiền nhưng chưa được ghi nhận: nguy cơ khiếu nại, sai công nợ.",
            "1) Mở đối soát, xem nội dung chuyển khoản. 2) Khớp thủ công vào hóa đơn/phiếu hoặc trả lại tiền thừa ngoài hệ thống. 3) Ghi chú đã xử lý.", "Mở Đối soát"),
        ["CHUYEN_KHOAN_HET_HAN"] = new("Lễ tân", 48, "Phiếu đặt / hóa đơn vẫn chưa thanh toán dù khách đã tạo mã QR.",
            "1) Liên hệ khách nhắc thanh toán hoặc tạo mã mới. 2) Nếu hủy: giải phóng phòng.", "Mở Đối soát"),
        ["CONG_NO_QUA_HAN"] = new("Kế toán", 72, "Tiền chưa thu được; rủi ro nợ xấu.",
            "1) Liên hệ khách/đối tác đòi nợ. 2) Ghi thu khi nhận. 3) Báo Quản lý nếu cần xử lý nợ khó đòi.", "Mở Hóa đơn"),
        ["TAI_KHOAN_NHAN_VIEN_BI_KHOA"] = new("Quản lý", 24, "Nhân viên không đăng nhập được, gián đoạn công việc (hoặc là dấu hiệu bị dò mật khẩu).",
            "1) Xác minh với nhân viên. 2) Mở khóa / đặt lại mật khẩu có lý do, hoặc giữ khóa nếu nghi ngờ.", "Mở Phân quyền"),
        ["SAO_LUU"] = new("Quản lý", 24, "Nếu dữ liệu hỏng sẽ mất toàn bộ thay đổi từ lần sao lưu cuối.",
            "1) Sao lưu ngay. 2) Kiểm tra bản vừa tạo. 3) Bật sao lưu tự động.", "Mở Sao lưu"),
    };

    private static QuyTrinh LayQuyTrinh(string ma, string mucDo)
        => QuyTrinhTheoMa.TryGetValue(ma, out var q) ? q
            : new QuyTrinh("Quản lý", mucDo == "NGHIEM_TRONG" ? 4 : mucDo == "CANH_BAO" ? 24 : 72, "Chưa xác định.", "Xem chi tiết và xử lý ở màn hình liên quan.", "Mở màn hình liên quan");

    /// <summary>Đồng bộ vòng đời cảnh báo (đợt mở / đợt đã hết) và gắn thông tin xử lý (ai, hạn, ảnh hưởng, cách xử lý) vào từng cảnh báo.</summary>
    private async Task<List<AlertResolvedDto>> GanTheoDoiAsync(List<AlertDto> canhBaos, DateTime bayGio)
    {
        var dangMo = await _context.CanhBaoTheoDois.Where(x => x.DaXuLyLuc == null).ToListAsync();
        var coThayDoi = false;
        foreach (var a in canhBaos)
        {
            var dong = dangMo.FirstOrDefault(x => x.Ma == a.Ma);
            if (dong is null)
            {
                dong = new CanhBaoTheoDoi { Ma = a.Ma, TieuDe = a.TieuDe, MucDo = a.MucDo, PhatHienLuc = bayGio, SoLuongLucPhatHien = a.SoLuong, SoLuongHienTai = a.SoLuong };
                _context.CanhBaoTheoDois.Add(dong);
                dangMo.Add(dong);
                coThayDoi = true;
            }
            else if (dong.SoLuongHienTai != a.SoLuong || dong.MucDo != a.MucDo)
            {
                dong.SoLuongHienTai = a.SoLuong; dong.MucDo = a.MucDo;
                coThayDoi = true;
            }
        }
        foreach (var dong in dangMo.Where(x => x.DaXuLyLuc == null && x.MaTheoDoi != 0 && canhBaos.All(a => a.Ma != x.Ma)))
        {
            dong.DaXuLyLuc = bayGio;
            coThayDoi = true;
        }

        if (coThayDoi)
        {
            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateException)
            {
                // Hai lần tải cảnh báo đồng thời cùng tạo đợt mới (chỉ mục duy nhất): bỏ phần vừa thêm và đọc lại, không để lỗi này làm hỏng bảng điều hành.
                _context.ChangeTracker.Clear();
                dangMo = await _context.CanhBaoTheoDois.AsNoTracking().Where(x => x.DaXuLyLuc == null).ToListAsync();
            }
        }

        var tenNguoi = dangMo.Where(x => x.NguoiNhan != null).Select(x => x.NguoiNhan!).Distinct().ToList();
        var hoTen = tenNguoi.Count == 0 ? new Dictionary<string, string>()
            : await _context.TaiKhoans.AsNoTracking().Where(x => tenNguoi.Contains(x.TenDN) && x.NhanVien != null).Select(x => new { x.TenDN, x.NhanVien!.HoTen }).ToDictionaryAsync(x => x.TenDN, x => x.HoTen);

        foreach (var a in canhBaos)
        {
            var qt = LayQuyTrinh(a.Ma, a.MucDo);
            var dong = dangMo.FirstOrDefault(x => x.Ma == a.Ma && x.DaXuLyLuc == null);
            a.AnhHuong = qt.AnhHuong; a.NguoiXuLyMacDinh = qt.NguoiXuLy; a.CachXuLy = qt.CachXuLy; a.HanhDongDeXuat = qt.NutMo;
            a.PhatHienLuc = dong?.PhatHienLuc ?? bayGio;
            a.HanXuLy = dong?.HanXuLy ?? a.PhatHienLuc.Value.AddHours(qt.HanGio);
            a.QuaHan = bayGio > a.HanXuLy;
            if (dong?.NguoiNhan is { } nguoi)
            {
                a.TrangThaiXuLy = "DANG_XU_LY"; a.NguoiNhan = nguoi; a.NhanLuc = dong.NhanLuc; a.GhiChuNhan = dong.GhiChuNhan;
                a.HoTenNguoiNhan = hoTen.GetValueOrDefault(nguoi);
            }
        }

        var tu = bayGio.AddHours(-48);
        var xong = await _context.CanhBaoTheoDois.AsNoTracking().Where(x => x.DaXuLyLuc != null && x.DaXuLyLuc >= tu)
            .OrderByDescending(x => x.DaXuLyLuc).Take(15).ToListAsync();
        var tenXong = xong.Where(x => x.NguoiNhan != null).Select(x => x.NguoiNhan!).Distinct().Except(hoTen.Keys).ToList();
        if (tenXong.Count > 0)
            foreach (var kv in await _context.TaiKhoans.AsNoTracking().Where(x => tenXong.Contains(x.TenDN) && x.NhanVien != null).Select(x => new { x.TenDN, x.NhanVien!.HoTen }).ToDictionaryAsync(x => x.TenDN, x => x.HoTen))
                hoTen[kv.Key] = kv.Value;
        return xong.Select(x => new AlertResolvedDto
        {
            Ma = x.Ma, TieuDe = x.TieuDe, MucDo = x.MucDo, PhatHienLuc = x.PhatHienLuc, DaXuLyLuc = x.DaXuLyLuc!.Value,
            SoGioXuLy = Math.Round((x.DaXuLyLuc!.Value - x.PhatHienLuc).TotalHours, 1), NguoiNhan = x.NguoiNhan, HoTenNguoiNhan = x.NguoiNhan is null ? null : hoTen.GetValueOrDefault(x.NguoiNhan)
        }).ToList();
    }

    /// <summary>Giao một cảnh báo cho người xử lý và đặt hạn (có ghi nhật ký). Chỉ giao được cảnh báo đang còn.</summary>
    public async Task<AlertDto> AssignAsync(string ma, AssignAlertRequest request, CurrentUser user)
    {
        var dong = await _context.CanhBaoTheoDois.FirstOrDefaultAsync(x => x.Ma == ma && x.DaXuLyLuc == null);
        if (dong is null)
        {
            // Có thể chưa kịp ghi đợt (cảnh báo vừa phát sinh) - tính lại để tạo đợt, rồi thử lại.
            await GetAlertsAsync();
            dong = await _context.CanhBaoTheoDois.FirstOrDefaultAsync(x => x.Ma == ma && x.DaXuLyLuc == null)
                ?? throw new ApiException(StatusCodes.Status404NotFound, "CANH_BAO_KHONG_CON", "Cảnh báo này không còn (có thể đã được xử lý xong).");
        }

        var nguoi = string.IsNullOrWhiteSpace(request.NguoiXuLy) ? user.TenDN : request.NguoiXuLy.Trim();
        var ok = await _context.TaiKhoans.AsNoTracking().AnyAsync(x => x.TenDN == nguoi && x.LoaiTaiKhoan == LoaiTaiKhoan.NHAN_VIEN && x.TrangThai == TrangThaiTaiKhoan.HOAT_DONG);
        if (!ok) throw new ApiException(StatusCodes.Status400BadRequest, "NGUOI_XU_LY_KHONG_HOP_LE", "Người xử lý phải là nhân viên đang hoạt động.", "nguoiXuLy");
        if (request.HanXuLy is { } han && han <= DateTime.Now)
            throw new ApiException(StatusCodes.Status400BadRequest, "HAN_XU_LY_KHONG_HOP_LE", "Hạn xử lý phải ở tương lai.", "hanXuLy");
        var ghiChu = request.GhiChu?.Trim();
        if (ghiChu is { Length: > 500 }) throw new ApiException(StatusCodes.Status400BadRequest, "GHI_CHU_QUA_DAI", "Ghi chú tối đa 500 ký tự.", "ghiChu");

        var truoc = dong.NguoiNhan is null ? "chưa ai nhận" : dong.NguoiNhan;
        dong.NguoiNhan = nguoi; dong.NguoiGiao = user.TenDN; dong.NhanLuc = DateTime.Now;
        if (request.HanXuLy is { } h) dong.HanXuLy = h;
        dong.GhiChuNhan = string.IsNullOrEmpty(ghiChu) ? null : ghiChu;

        _audit?.Ghi("GIAO_CANH_BAO", ma, $"nguoiXuLy: {truoc} -> {nguoi}; han={(dong.HanXuLy is null ? "mặc định" : dong.HanXuLy.Value.ToString("dd/MM/yyyy HH:mm"))}; mucDo={dong.MucDo}"
            + (string.IsNullOrEmpty(dong.GhiChuNhan) ? "" : $"; lyDo={dong.GhiChuNhan}"));
        await _context.SaveChangesAsync();

        var kq = await GetAlertsAsync();
        return kq.CanhBaoDangCo.FirstOrDefault(x => x.Ma == ma)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "CANH_BAO_KHONG_CON", "Cảnh báo này không còn (có thể đã được xử lý xong).");
    }
}
