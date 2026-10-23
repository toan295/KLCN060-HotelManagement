using KLCN060.Api.DTOs.Bookings;
using KLCN060.Api.Middlewares;
using KLCN060.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public partial class BookingService
{
    /// <summary>
    /// Báo giá đặt phòng cho lễ tân đọc cho khách trước khi tạo: dùng đúng bảng giá, cách đếm phòng trống và quy tắc khuyến mãi như <see cref="CreateAsync"/>
    /// nhưng không khóa, không ghi, không tăng lượt khuyến mãi. Thiếu phòng hay khuyến mãi không đủ điều kiện được báo trong kết quả (không ném lỗi).
    /// </summary>
    public async Task<BookingQuoteDto> QuoteAsync(CreateBookingRequest request)
    {
        if (!EnumParser.TryParse<LoaiDatPhong>(request.LoaiDatPhong, out var loaiDatPhong))
            throw new ApiException(StatusCodes.Status400BadRequest, "LOAI_DAT_PHONG_KHONG_HOP_LE", "LoaiDatPhong không hợp lệ. Chỉ chấp nhận: CA_NHAN, DOAN.", "loaiDatPhong");
        var homNay = DateOnly.FromDateTime(DateTime.Now);
        if (request.NgayDonDuKien < homNay)
            throw new ApiException(StatusCodes.Status400BadRequest, "NGAY_DON_KHONG_HOP_LE", "Ngày đón dự kiến không được ở quá khứ.", "ngayDonDuKien");
        if (request.NgayTraDuKien <= request.NgayDonDuKien)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_KHONG_HOP_LE", "Ngày trả phải sau ngày đón.", "ngayTraDuKien");
        KiemTraSoDem(request.NgayDonDuKien, request.NgayTraDuKien, "ngayTraDuKien");
        if (request.PhongCanDat is null || request.PhongCanDat.Count == 0)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_PHONG_CAN_DAT", "Cần chọn ít nhất 1 loại phòng.", "phongCanDat");
        for (var i = 0; i < request.PhongCanDat.Count; i++)
        {
            var d = request.PhongCanDat[i];
            if (string.IsNullOrWhiteSpace(d.MaLoai))
                throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_MA_LOAI", "Thiếu mã loại phòng.", $"phongCanDat[{i}].maLoai");
            if (d.SoLuong < 1)
                throw new ApiException(StatusCodes.Status400BadRequest, "SO_LUONG_KHONG_HOP_LE", "Số lượng phòng phải lớn hơn 0.", $"phongCanDat[{i}].soLuong");
        }

        var gop = request.PhongCanDat.GroupBy(x => x.MaLoai.Trim())
            .Select(g => (MaLoai: g.Key, SoLuong: (int)Math.Min(g.Sum(x => (long)x.SoLuong), int.MaxValue)))
            .OrderBy(x => x.MaLoai, StringComparer.Ordinal).ToList();
        if (loaiDatPhong == LoaiDatPhong.DOAN && gop.Sum(x => (long)x.SoLuong) < 2)
            throw new ApiException(StatusCodes.Status400BadRequest, "DOAN_CAN_TU_2_PHONG", "Đặt phòng theo đoàn cần từ 2 phòng trở lên. Đặt 1 phòng vui lòng chọn loại cá nhân.", "loaiDatPhong");

        var soDem = request.NgayTraDuKien.DayNumber - request.NgayDonDuKien.DayNumber;
        var quyTacGia = await GiaPhongCalculator.LayQuyTacDangBatAsync(_context);
        var daChiem = await RoomAvailability.GetOccupiedRoomIdsAsync(_context, request.NgayDonDuKien, request.NgayTraDuKien);

        var ketQua = new BookingQuoteDto { SoDem = soDem, DuPhong = true };
        var dongTheoLoai = new List<(string MaLoai, decimal ThanhTien)>();
        for (var i = 0; i < gop.Count; i++)
        {
            var (maLoai, soLuong) = gop[i];
            var loai = await _context.LoaiPhongs.AsNoTracking().Include(x => x.Phongs).FirstOrDefaultAsync(x => x.MaLoai == maLoai)
                ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_LOAI_PHONG", $"Không tìm thấy loại phòng {maLoai}.", $"phongCanDat[{i}].maLoai");
            var trong = loai.Phongs.Count(p => p.TinhTrang != TinhTrangPhong.OOO && !daChiem.Contains(p.MaPhong));
            var gia = GiaPhongCalculator.Tinh(loai, request.NgayDonDuKien, request.NgayTraDuKien, loaiDatPhong, quyTacGia);
            var du = trong >= soLuong;
            ketQua.DuPhong &= du;
            ketQua.Dong.Add(new BookingQuoteLineDto
            {
                MaLoai = maLoai, TenLoai = loai.TenLoai, SoLuong = soLuong, SoPhongTrong = trong, DuPhong = du,
                DonGiaBinhQuan = gia.DonGiaBinhQuan, ThanhTienMotPhong = gia.TongTien, ThanhTien = gia.TongTien * soLuong
            });
            for (var k = 0; k < soLuong && k < 1000; k++) dongTheoLoai.Add((maLoai, gia.TongTien));
        }

        ketQua.TongTien = ketQua.Dong.Sum(x => x.ThanhTien);
        ketQua.TienCoc = Math.Round(ketQua.TongTien * 0.5m, 0, MidpointRounding.AwayFromZero);

        var conLai = ketQua.TongTien;
        foreach (var maKm in DanhSachMaKhuyenMai(request))
        {
            var km = await _context.KhuyenMais.AsNoTracking().FirstOrDefaultAsync(x => x.MaKM == maKm)
                ?? throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_KHUYEN_MAI", $"Không tìm thấy khuyến mãi {maKm}.", "maKhuyenMais");
            var tenLoaiKm = km.MaLoaiApDung is null ? null : await _context.LoaiPhongs.AsNoTracking().Where(x => x.MaLoai == km.MaLoaiApDung).Select(x => x.TenLoai).FirstOrDefaultAsync();
            var thieu = PromotionRules.DanhGia(km, homNay, soDem, dongTheoLoai, tinhLuot: true, tenLoaiKm).Where(c => !c.Dat).ToList();
            var tien = thieu.Count == 0 ? Math.Min(PromotionRules.TinhTien(km, dongTheoLoai), conLai) : 0m;
            conLai -= tien;
            ketQua.KhuyenMais.Add(new BookingQuotePromotionDto
            {
                MaKM = km.MaKM, TenKM = km.TenKM, DuDieuKien = thieu.Count == 0, TienGiam = tien,
                LyDoKhongDat = thieu.Count == 0 ? null : $"{thieu[0].Ten}: {thieu[0].ChiTiet}"
            });
            ketQua.TongKhuyenMai += tien;
        }
        ketQua.KhuyenMai = ketQua.KhuyenMais.FirstOrDefault();
        ketQua.TongSauKhuyenMai = ketQua.TongTien - ketQua.TongKhuyenMai;
        return ketQua;
    }
}
