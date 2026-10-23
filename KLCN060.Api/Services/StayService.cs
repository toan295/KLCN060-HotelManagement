using KLCN060.Api.Auditing;
using KLCN060.Api.DTOs.Stays;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public partial class StayService : IStayService
{
    private readonly KLCN060DbContext _context;
    private readonly IAuditTrail _audit;

    public StayService(KLCN060DbContext context, IAuditTrail audit)
    {
        _context = context;
        _audit = audit;
    }

    public async Task<StayServiceUsageDto> AddServiceAsync(string maPhieuNhan, string maPhong, AddStayServiceRequest request)
    {
        if (request.SoLuong < 1 || request.SoLuong > SoLuongToiDa)
            throw new ApiException(StatusCodes.Status400BadRequest, "SO_LUONG_KHONG_HOP_LE", $"Số lượng phải từ 1 đến {SoLuongToiDa}.", "soLuong");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        await KhoaPhieuNhanAsync(maPhieuNhan);

        var luotO = await TimLuotLuuTruAsync(maPhieuNhan, maPhong);
        if (luotO.TrangThai != TrangThaiChiTietPhieuNhan.DANG_O)
            throw new ApiException(StatusCodes.Status409Conflict, "LUOT_LUU_TRU_DA_KET_THUC", "Lượt lưu trú này đã trả phòng, không thể thêm dịch vụ.");
        await KiemTraChuaLapHoaDonAsync(maPhieuNhan);

        var dichVu = await _context.DichVus.FirstOrDefaultAsync(x => x.MaDV == request.MaDV);
        if (dichVu is null)
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_DICH_VU", "Không tìm thấy dịch vụ.", "maDV");
        if (!dichVu.HoatDong)
            throw new ApiException(StatusCodes.Status409Conflict, "DICH_VU_NGUNG_PHUC_VU", $"Dịch vụ \"{dichVu.TenDV}\" đã ngừng phục vụ, không ghi thêm cho khách được.", "maDV");

        // Chốt giá tại thời điểm gọi: ThanhTien lưu cứng, không đổi khi DichVu.GiaDV thay đổi về sau.
        var chiTiet = new ChiTietSuDungDV
        {
            MaPhieuNhan = maPhieuNhan,
            MaDV = dichVu.MaDV,
            SoLuong = request.SoLuong,
            NgaySuDung = DateTime.Now,
            TenDichVu = dichVu.TenDV,
            DonViTinh = dichVu.DonViTinh,
            ThanhTien = dichVu.GiaDV * request.SoLuong,
            MaPhong = maPhong,
            NguoiGhi = _audit.TenDN
        };

        _context.ChiTietSuDungDVs.Add(chiTiet);
        _audit.Ghi("GHI_DICH_VU", maPhieuNhan, $"phong={maPhong}; dichVu={dichVu.MaDV} {dichVu.TenDV}; soLuong={request.SoLuong}; donGia={Math.Round(dichVu.GiaDV, 0)}; thanhTien={Math.Round(chiTiet.ThanhTien, 0)}");
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        var dtoGhi = ToDto(chiTiet, dichVu);
        await GanTenNguoiGhiAsync(new[] { dtoGhi });
        return dtoGhi;
    }

    /// <summary>
    /// Ghi một gói dịch vụ cho lượt lưu trú: mỗi dịch vụ trong gói thành một dòng sử dụng (số lượng = số lượng trong gói × số gói), tổng các dòng đúng bằng
    /// giá gói × số gói; giá gói được chia theo tỷ lệ giá lẻ, dòng cuối nhận phần làm tròn còn dư. Giá chốt tại thời điểm ghi.
    /// </summary>
    public async Task<List<StayServiceUsageDto>> AddServicePackageAsync(string maPhieuNhan, string maPhong, AddStayServicePackageRequest request)
    {
        if (request.SoLuong < 1 || request.SoLuong > SoLuongToiDa)
            throw new ApiException(StatusCodes.Status400BadRequest, "SO_LUONG_KHONG_HOP_LE", $"Số lượng gói phải từ 1 đến {SoLuongToiDa}.", "soLuong");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        await KhoaPhieuNhanAsync(maPhieuNhan);

        var luotO = await TimLuotLuuTruAsync(maPhieuNhan, maPhong);
        if (luotO.TrangThai != TrangThaiChiTietPhieuNhan.DANG_O)
            throw new ApiException(StatusCodes.Status409Conflict, "LUOT_LUU_TRU_DA_KET_THUC", "Lượt lưu trú này đã trả phòng, không thể thêm dịch vụ.");
        await KiemTraChuaLapHoaDonAsync(maPhieuNhan);

        var goi = await _context.GoiDichVus.Include(x => x.ChiTietGois).ThenInclude(c => c.DichVu).FirstOrDefaultAsync(x => x.MaGoi == request.MaGoi)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_GOI_DICH_VU", "Không tìm thấy gói dịch vụ.", "maGoi");
        if (!goi.HoatDong)
            throw new ApiException(StatusCodes.Status409Conflict, "GOI_DICH_VU_NGUNG_BAN", $"Gói \"{goi.TenGoi}\" đã ngừng bán.", "maGoi");
        var ngung = goi.ChiTietGois.Where(x => !x.DichVu.HoatDong).Select(x => x.DichVu.TenDV).ToList();
        if (ngung.Count > 0)
            throw new ApiException(StatusCodes.Status409Conflict, "DICH_VU_TRONG_GOI_NGUNG_PHUC_VU", $"Gói có dịch vụ đã ngừng phục vụ: {string.Join(", ", ngung)}.", "maGoi");

        var tongTienGoi = goi.GiaGoi * request.SoLuong;
        var dongs = goi.ChiTietGois.OrderBy(x => x.MaDV, StringComparer.Ordinal).ToList();
        var giaLe = dongs.Select(x => x.DichVu.GiaDV * x.SoLuong * request.SoLuong).ToList();
        var tongLe = giaLe.Sum();
        var bayGio = DateTime.Now;

        var chiTiets = new List<ChiTietSuDungDV>();
        decimal daChia = 0;
        for (var i = 0; i < dongs.Count; i++)
        {
            var thanhTien = i == dongs.Count - 1
                ? tongTienGoi - daChia
                : Math.Round(tongLe > 0 ? tongTienGoi * giaLe[i] / tongLe : tongTienGoi / dongs.Count, 0, MidpointRounding.AwayFromZero);
            daChia += thanhTien;
            chiTiets.Add(new ChiTietSuDungDV
            {
                MaPhieuNhan = maPhieuNhan,
                MaDV = dongs[i].MaDV,
                SoLuong = dongs[i].SoLuong * request.SoLuong,
                NgaySuDung = bayGio,
                TenDichVu = dongs[i].DichVu.TenDV,
                DonViTinh = dongs[i].DichVu.DonViTinh,
                ThanhTien = thanhTien,
                MaGoi = goi.MaGoi,
                MaPhong = maPhong,
                NguoiGhi = _audit.TenDN
            });
        }

        _context.ChiTietSuDungDVs.AddRange(chiTiets);
        _audit.Ghi("GHI_GOI_DICH_VU", maPhieuNhan, $"phong={maPhong}; goi={goi.MaGoi} {goi.TenGoi}; soGoi={request.SoLuong}; tongTien={Math.Round(tongTienGoi, 0)}; giaLe={Math.Round(tongLe, 0)}");
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        var dtos = chiTiets.Select((x, i) => ToDto(x, dongs[i].DichVu)).ToList();
        await GanTenNguoiGhiAsync(dtos);
        return dtos;
    }

    /// <summary>
    /// Hủy một dòng dịch vụ đã ghi nhầm (sai dịch vụ/số lượng): chỉ khi phiếu nhận CHƯA lập hóa đơn (sau đó phải qua giảm giá/điều chỉnh có Quản lý duyệt),
    /// bắt buộc ghi lý do, nhật ký lưu dịch vụ/số lượng/thành tiền bị hủy.
    /// </summary>
    public async Task<StayServiceUsageDto> VoidServiceAsync(string maPhieuNhan, string maPhong, int maChiTietDV, string? lyDo)
    {
        if (string.IsNullOrWhiteSpace(lyDo))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_LY_DO", "Cần ghi lý do khi hủy dòng dịch vụ (ảnh hưởng số tiền của khách, có ghi nhật ký).", "lyDo");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        await KhoaPhieuNhanAsync(maPhieuNhan);
        await TimLuotLuuTruAsync(maPhieuNhan, maPhong);

        if (await _context.HoaDons.AnyAsync(x => x.MaPhieuNhan == maPhieuNhan))
            throw new ApiException(StatusCodes.Status409Conflict, "DA_LAP_HOA_DON", "Phiếu nhận đã lập hóa đơn, không hủy được dòng dịch vụ: hãy gửi yêu cầu giảm giá/điều chỉnh để Quản lý duyệt.");

        var dong = await _context.ChiTietSuDungDVs.Include(x => x.DichVu).FirstOrDefaultAsync(x => x.MaChiTietDV == maChiTietDV && x.MaPhieuNhan == maPhieuNhan)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_DONG_DICH_VU", "Không tìm thấy dòng dịch vụ trong lượt lưu trú này.");
        if (dong.MaPhong is not null && dong.MaPhong != maPhong)
            throw new ApiException(StatusCodes.Status409Conflict, "DONG_DICH_VU_KHAC_PHONG",
                $"Dòng dịch vụ này được ghi cho phòng {dong.MaPhong}, không phải phòng {maPhong}: hãy chọn đúng phòng trước khi hủy.");
        var dto = ToDto(dong, dong.DichVu);
        await GanTenNguoiGhiAsync(new[] { dto });
        _context.ChiTietSuDungDVs.Remove(dong);

        // Dòng này do lễ tân tính phí từ minibar buồng phòng ghi nhận: quay lại hàng chờ để xử lý lại (tính phí lại hoặc bỏ qua có lý do).
        var tuMinibar = await _context.ChiTietKiemPhongs.Where(c => c.MaChiTietDV == maChiTietDV).ToListAsync();
        foreach (var c in tuMinibar)
        {
            c.TrangThaiTinhPhi = TrangThaiTinhPhi.CHO_TINH_PHI;
            c.MaChiTietDV = null;
            c.NguoiXuLy = null;
            c.ThoiGianXuLy = null;
        }
        _audit.Ghi("HUY_DICH_VU", maPhieuNhan,
            $"phong={maPhong}; dichVu={dong.MaDV} {dong.DichVu.TenDV}; soLuong={dong.SoLuong}; thanhTien={Math.Round(dong.ThanhTien, 0)}; goi={dong.MaGoi ?? "khong"}; nguoiGhi={dong.NguoiGhi ?? "khong ro"}; ngayGhi={dong.NgaySuDung:yyyy-MM-dd HH:mm}; lyDo={lyDo.Trim()}");
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return dto;
    }

    public async Task<List<StayServiceUsageDto>> GetServicesAsync(string maPhieuNhan, string maPhong)
    {
        await TimLuotLuuTruAsync(maPhieuNhan, maPhong);

        // ChiTietSuDungDV chỉ gắn với MaPhieuNhan (không có MaPhong) nên trả toàn bộ dịch vụ của phiếu nhận.
        var danhSach = await _context.ChiTietSuDungDVs
            .Include(x => x.DichVu)
            .Where(x => x.MaPhieuNhan == maPhieuNhan)
            .OrderBy(x => x.NgaySuDung)
            .ToListAsync();

        var maHoaDon = await _context.HoaDons.Where(x => x.MaPhieuNhan == maPhieuNhan).Select(x => x.MaHD).FirstOrDefaultAsync();
        var tenNguoi = await TenNguoiDung.HienThiAsync(_context, danhSach.Select(x => x.NguoiGhi));
        return danhSach.Select(x =>
        {
            var dto = ToDto(x, x.DichVu);
            dto.NguoiGhiHienThi = string.IsNullOrEmpty(x.NguoiGhi) || x.NguoiGhi == "he_thong" ? "Hệ thống (dữ liệu cũ)" : tenNguoi.GetValueOrDefault(x.NguoiGhi, x.NguoiGhi);
            dto.CoTheHuy = maHoaDon is null;
            dto.LyDoKhongHuy = maHoaDon is null ? null : $"Đã lập hóa đơn {maHoaDon}: không hủy được, hãy gửi yêu cầu giảm giá/điều chỉnh để Quản lý duyệt.";
            return dto;
        }).ToList();
    }

    /// <summary>Số lượng tối đa mỗi lần ghi (chặn gõ nhầm thừa số 0).</summary>
    public const int SoLuongToiDa = 999;

    private async Task KiemTraChuaLapHoaDonAsync(string maPhieuNhan)
    {
        var maHD = await _context.HoaDons.Where(x => x.MaPhieuNhan == maPhieuNhan).Select(x => x.MaHD).FirstOrDefaultAsync();
        if (maHD is not null)
            throw new ApiException(StatusCodes.Status409Conflict, "DA_LAP_HOA_DON", $"Phiếu nhận đã lập hóa đơn {maHD}, không ghi thêm dịch vụ được.");
    }

    /// <summary>Khóa phiếu nhận tới hết transaction để thêm dịch vụ, đổi phòng và trả phòng của cùng 1 phiếu không chạy chồng nhau (tránh dịch vụ lọt khỏi hóa đơn).</summary>
    private async Task KhoaPhieuNhanAsync(string maPhieuNhan)
    {
        var tonTai = await PhieuNhanLock.KhoaAsync(_context, maPhieuNhan);
        if (!tonTai)
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LUOT_LUU_TRU", "Không tìm thấy lượt lưu trú.");
    }

    public async Task KiemTraLuotTonTaiAsync(string maPhieuNhan, string maPhong)
    {
        if (!await _context.ChiTietPhieuNhans.AnyAsync(x => x.MaPhieuNhan == maPhieuNhan && x.MaPhong == maPhong))
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LUOT_LUU_TRU", "Không tìm thấy lượt lưu trú.");
    }

    private async Task<ChiTietPhieuNhan> TimLuotLuuTruAsync(string maPhieuNhan, string maPhong)
    {
        var luotO = await _context.ChiTietPhieuNhans.FirstOrDefaultAsync(x => x.MaPhieuNhan == maPhieuNhan && x.MaPhong == maPhong);
        if (luotO is null)
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LUOT_LUU_TRU", "Không tìm thấy lượt lưu trú.");
        return luotO;
    }

    /// <summary>Đổi mã đăng nhập người ghi thành "Họ tên (Mã NV)" như danh sách dịch vụ, để phản hồi lúc ghi/hủy hiện giống danh sách.</summary>
    private async Task GanTenNguoiGhiAsync(IEnumerable<StayServiceUsageDto> dtos)
    {
        var ds = dtos.ToList();
        var tenNguoi = await TenNguoiDung.HienThiAsync(_context, ds.Select(x => x.NguoiGhi));
        foreach (var d in ds)
            d.NguoiGhiHienThi = string.IsNullOrEmpty(d.NguoiGhi) || d.NguoiGhi == "he_thong" ? "Hệ thống (dữ liệu cũ)" : tenNguoi.GetValueOrDefault(d.NguoiGhi, d.NguoiGhi);
    }

    private static StayServiceUsageDto ToDto(ChiTietSuDungDV x, DichVu dichVu) => new()
    {
        MaChiTietDV = x.MaChiTietDV,
        MaPhieuNhan = x.MaPhieuNhan,
        MaDV = x.MaDV,
        TenDV = x.TenDichVu ?? dichVu.TenDV,
        DonViTinh = x.DonViTinh ?? dichVu.DonViTinh,
        SoLuong = x.SoLuong,
        NgaySuDung = x.NgaySuDung,
        MaGoi = x.MaGoi,
        ThanhTien = x.ThanhTien,
        MaPhong = x.MaPhong,
        DonGia = x.SoLuong == 0 ? 0 : Math.Round(x.ThanhTien / x.SoLuong, 0, MidpointRounding.AwayFromZero),
        NguoiGhi = x.NguoiGhi,
        NguoiGhiHienThi = x.NguoiGhi ?? ""
    };
}
