using KLCN060.Domain;
using KLCN060.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Infrastructure;

/// <summary>
/// Dữ liệu vận hành mở rộng: lịch sử lưu trú 45 ngày gần đây (đủ kiểu thanh toán, phụ thu, khuyến mãi, đổi phòng,
/// đặt đoàn, khách vãng lai), khách đang ở và các phiếu đặt tương lai/đã hủy/không đến. Toàn bộ mốc ngày tính TƯƠNG ĐỐI so với lúc seed chạy. Số tiền hóa đơn tính đúng theo
/// cùng công thức của FolioCalculator (tiền phòng + phụ thu vượt người + phụ thu trễ giờ + dịch vụ - khuyến mãi - cọc).
/// </summary>
public static partial class DataSeeder
{
    private sealed record DoanO(string MaPhong, int BatDau, int SoDem, bool DatTruoc = true);
    private sealed record DichVuDung(string MaDV, int SoLuong, int NgayThu);
    private sealed record TraTien(HinhThucThanhToan Hinh, decimal? SoTien = null, string? MaGiaoDich = null);

    private sealed record LuuTruMau(
        string MaKhach,
        DoanO[] Phong,
        int SoNguoi,
        LoaiDatPhong Loai = LoaiDatPhong.CA_NHAN,
        string? MaKM = null,
        DichVuDung[]? DichVu = null,
        bool TraTre = false,
        string MaNV = "NV02",
        TraTien[]? ThanhToan = null,
        bool DangO = false,
        bool VangLai = false,
        int DatTruocNgay = 7,
        string? GhiChuDoiPhong = null);

    private sealed record DatPhongMau(
        string MaKhach,
        string[] Phong,
        int BatDau,
        int SoDem,
        TrangThaiPhieuDat TrangThai,
        LoaiDatPhong Loai = LoaiDatPhong.CA_NHAN,
        string? MaKM = null,
        int DatTruocNgay = 5);

    /// <param name="boSung">false: bộ dữ liệu mở rộng gốc (khách KH08-KH15); true: bộ bổ sung (khách KH16-KH19) gồm khách trả phòng hôm nay, quá hạn trả phòng,
    /// phiếu đặt đến hôm nay/ngày mai... Mỗi bộ có dấu hiệu "đã seed" riêng nên CSDL cũ cũng được bổ sung bộ mới.</param>
    private static async Task SeedLuuTruMoRongAsync(KLCN060DbContext context, bool boSung = false)
    {
        // KH08 / KH16 chỉ xuất hiện trong từng bộ dữ liệu tương ứng -> dùng làm dấu hiệu "đã seed rồi" (idempotent).
        var khachDauHieu = boSung ? "KH16" : "KH08";
        if (await context.PhieuDatPhongs.AnyAsync(x => x.MaKhach == khachDauHieu)) return;
        if (boSung && !await context.Khachs.AnyAsync(x => x.MaKhach == "KH19")) return;

        var homNay = DateOnly.FromDateTime(DateTime.Now);
        var bayGio = DateTime.Now;

        var loaiPhongs = await context.LoaiPhongs.ToDictionaryAsync(x => x.MaLoai);
        var phongLoai = await context.Phongs.ToDictionaryAsync(x => x.MaPhong, x => x.MaLoai);
        var dichVus = await context.DichVus.ToDictionaryAsync(x => x.MaDV);
        var khuyenMais = await context.KhuyenMais.ToDictionaryAsync(x => x.MaKM);

        // Mã đã dùng bởi 6 kịch bản gốc (SeedVongDoiLuuTruMauAsync) - tránh trùng khóa chính khi cùng ngày.
        var maDaDung = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ma in await context.PhieuDatPhongs.Select(x => x.MaPhieuDat).ToListAsync()) maDaDung.Add(ma);
        foreach (var ma in await context.PhieuNhanPhongs.Select(x => x.MaPhieuNhan).ToListAsync()) maDaDung.Add(ma);
        foreach (var ma in await context.HoaDons.Select(x => x.MaHD).ToListAsync()) maDaDung.Add(ma);
        foreach (var ma in await context.PhieuDoiPhongs.Select(x => x.MaPhieuDoi).ToListAsync()) maDaDung.Add(ma);

        string Ma(string tienTo, DateOnly ngay)
        {
            var n = 1;
            string ma;
            do { ma = $"{tienTo}{ngay:yyyyMMdd}-{n:0000}"; n++; } while (!maDaDung.Add(ma));
            return ma;
        }

        DateTime Luc(DateOnly ngay, int gio, int phut = 0) => ngay.ToDateTime(new TimeOnly(gio, phut));

        decimal TinhKhuyenMai(string? maKM, decimal tongTienPhong)
        {
            if (maKM is null) return 0m;
            var km = khuyenMais[maKM];
            var giam = km.LoaiKM switch
            {
                LoaiKhuyenMai.PHAN_TRAM => tongTienPhong * (km.PhanTramKM ?? km.GiaTri ?? 0) / 100m,
                LoaiKhuyenMai.SO_TIEN_CO_DINH => km.GiaTri ?? 0m,
                _ => 0m
            };
            return Math.Round(Math.Min(giam, tongTienPhong), 0, MidpointRounding.AwayFromZero);
        }

        // ================== 1) LỊCH SỬ + ĐANG Ở ==================
        var luuTrus = boSung ? LuuTruBoSung() : new List<LuuTruMau>
        {
            // ---- đã trả phòng, đã thanh toán đủ ----
            new("KH02", [new("P101", -40, 2)], 2, ThanhToan: [new(HinhThucThanhToan.TIEN_MAT)], DatTruocNgay: 10),
            new("KH06", [new("P102", -38, 3)], 3, DichVu: [new("DV06", 3, 1)], MaNV: "NV05", ThanhToan: [new(HinhThucThanhToan.CHUYEN_KHOAN, MaGiaoDich: "FT26081601")]),
            new("KH05", [new("P202", -35, 2)], 2, MaKM: "KM03", ThanhToan: [new(HinhThucThanhToan.CHUYEN_KHOAN, MaGiaoDich: "FT26082201")], DatTruocNgay: 15),
            new("KH04", [new("P301", -30, 4)], 2, DichVu: [new("DV07", 1, 0), new("DV05", 4, 2)], TraTre: true, ThanhToan: [new(HinhThucThanhToan.CHUYEN_KHOAN, MaGiaoDich: "FT26082002")]),
            new("KH01", [new("P401", -28, 2)], 3, ThanhToan: [new(HinhThucThanhToan.TIEN_MAT)], MaNV: "NV05"),
            new("KH03", [new("P303", -25, 1)], 2, ThanhToan: [new(HinhThucThanhToan.CHUYEN_KHOAN, MaGiaoDich: "FT26090102")]),
            // ---- khách vãng lai (không đặt trước, không cọc) ----
            new("KH07", [new("P102", -20, 2)], 2, DichVu: [new("DV04", 2, 0)], VangLai: true, ThanhToan: [new(HinhThucThanhToan.TIEN_MAT)]),
            // ---- thanh toán một phần (còn nợ) ----
            new("KH02", [new("P203", -15, 3)], 2, DichVu: [new("DV01", 3, 1), new("DV04", 2, 2)], ThanhToan: [new(HinhThucThanhToan.TIEN_MAT, 600000m)]),
            new("KH06", [new("P302", -14, 2)], 2, MaNV: "NV05", ThanhToan: [new(HinhThucThanhToan.TIEN_MAT)]),
            // ---- chưa thanh toán đồng nào ----
            new("KH05", [new("P401", -10, 2)], 3, DichVu: [new("DV06", 3, 0)], ThanhToan: []),
            // ---- đổi phòng giữa chừng (Superior -> Deluxe, +200.000đ/đêm) ----
            new("KH09", [new("P202", -9, 1), new("P303", -8, 2, false)], 2, ThanhToan: [new(HinhThucThanhToan.CHUYEN_KHOAN, MaGiaoDich: "FT26091503")], GhiChuDoiPhong: "Khách xin nâng hạng lên Deluxe do phòng cũ ồn từ công trình bên cạnh."),
            // ---- đặt đoàn 3 phòng Triple + khuyến mãi khách đoàn, thanh toán 2 lần ----
            new("KH10", [new("P401", -7, 2), new("P402", -7, 2), new("P403", -7, 2)], 3, LoaiDatPhong.DOAN, MaKM: "KM02",
                DichVu: [new("DV06", 6, 0), new("DV07", 1, 0)], ThanhToan: [new(HinhThucThanhToan.TIEN_MAT, 1000000m), new(HinhThucThanhToan.CHUYEN_KHOAN, MaGiaoDich: "FT26091704")], DatTruocNgay: 14),
            new("KH11", [new("P301", -3, 2)], 2, DichVu: [new("DV04", 2, 0), new("DV05", 2, 1)], MaNV: "NV05", ThanhToan: [new(HinhThucThanhToan.TIEN_MAT)]),
            new("KH12", [new("P202", -4, 1)], 2, ThanhToan: [new(HinhThucThanhToan.CHUYEN_KHOAN, MaGiaoDich: "FT26092203")]),
            // ---- ĐANG Ở ----
            new("KH08", [new("P403", -1, 3)], 3, DichVu: [new("DV04", 3, 0)], DatTruocNgay: 5, DangO: true),
            new("KH07", [new("P302", -1, 2)], 2, DichVu: [new("DV05", 2, 0), new("DV01", 1, 0)], VangLai: true, DangO: true)
        };

        foreach (var s in luuTrus)
            ThemLuuTru(s);

        void ThemLuuTru(LuuTruMau s)
        {
            var doans = s.Phong;
            var ngayDon = homNay.AddDays(doans.Min(d => d.BatDau));
            var ngayTraCuoi = homNay.AddDays(doans.Max(d => d.BatDau + d.SoDem));
            var tongDem = ngayTraCuoi.DayNumber - ngayDon.DayNumber;

            // ---- phiếu đặt (nếu có đặt trước) ----
            PhieuDatPhong? phieuDat = null;
            decimal tienCoc = 0;
            if (!s.VangLai)
            {
                phieuDat = new PhieuDatPhong
                {
                    MaPhieuDat = Ma("BK", ngayDon),
                    MaKhach = s.MaKhach,
                    NgayDat = Luc(ngayDon.AddDays(-s.DatTruocNgay), 10, 30),
                    NgayDonDuKien = ngayDon,
                    NgayTraDuKien = ngayTraCuoi,
                    TrangThai = TrangThaiPhieuDat.DA_NHAN_PHONG,
                    LoaiDatPhong = s.Loai
                };
                foreach (var d in doans.Where(x => x.DatTruoc))
                {
                    var donGia = loaiPhongs[phongLoai[d.MaPhong]].DonGia;
                    phieuDat.ChiTietPhieuDats.Add(new ChiTietPhieuDat { MaPhieuDat = phieuDat.MaPhieuDat, MaPhong = d.MaPhong, SoLuong = 1, DonGiaApDung = donGia, ThanhTien = donGia * tongDem });
                }
                var tongDat = phieuDat.ChiTietPhieuDats.Sum(x => x.ThanhTien);
                tienCoc = Math.Round(tongDat * 0.5m, 0, MidpointRounding.AwayFromZero);
                phieuDat.TienCoc = tienCoc;

                if (s.MaKM is not null)
                {
                    phieuDat.ChiTietKhuyenMais.Add(new ChiTietKhuyenMai { MaPhieuDat = phieuDat.MaPhieuDat, MaKM = s.MaKM, TongKhuyenMai = TinhKhuyenMai(s.MaKM, tongDat) });
                    khuyenMais[s.MaKM].SoLuongDaSuDung += 1;
                }
                context.PhieuDatPhongs.Add(phieuDat);
            }

            // ---- phiếu nhận + từng dòng lưu trú ----
            var phieuNhan = new PhieuNhanPhong
            {
                MaPhieuNhan = Ma("CI", ngayDon),
                MaPhieuDat = phieuDat?.MaPhieuDat,
                // [Khối 8 - K8-5] Backfill MaKhach (cột thêm ở K7-1) cho dữ liệu mẫu — K7-1 chỉ sửa luồng nghiệp vụ (CheckInService), seed tạo phiếu nhận trực tiếp nên bị sót.
                // Lượt vãng lai (VangLai) cũng có khách xác định trong mẫu nên luôn gán được.
                MaKhach = s.MaKhach,
                NgayNhan = Luc(ngayDon, 14, 5)
            };

            var dongLuuTru = new List<ChiTietPhieuNhan>();
            for (var i = 0; i < doans.Length; i++)
            {
                var d = doans[i];
                var ngayNhan = homNay.AddDays(d.BatDau);
                var ngayTra = homNay.AddDays(d.BatDau + d.SoDem);
                var donGia = loaiPhongs[phongLoai[d.MaPhong]].DonGia;
                var laDongCuoiCung = ngayTra == ngayTraCuoi;

                var dong = new ChiTietPhieuNhan
                {
                    MaPhieuNhan = phieuNhan.MaPhieuNhan,
                    MaPhong = d.MaPhong,
                    NgayNhan = ngayNhan,
                    NgayTra = s.DangO ? null : ngayTra,
                    NgayTraDuKien = ngayTra,
                    SoNguoi = s.SoNguoi,
                    DonGia = donGia,
                    TienPhuThu = (s.TraTre && !s.DangO && laDongCuoiCung) ? donGia : 0m,
                    TrangThai = s.DangO ? TrangThaiChiTietPhieuNhan.DANG_O : TrangThaiChiTietPhieuNhan.DA_TRA_PHONG
                };
                dongLuuTru.Add(dong);
                phieuNhan.ChiTietPhieuNhans.Add(dong);

                // đổi phòng: dòng không "đặt trước" nối tiếp dòng ngay trước nó
                if (!d.DatTruoc && i > 0)
                {
                    var cu = doans[i - 1];
                    var donGiaCu = loaiPhongs[phongLoai[cu.MaPhong]].DonGia;
                    context.PhieuDoiPhongs.Add(new PhieuDoiPhong
                    {
                        MaPhieuDoi = Ma("RM", ngayNhan),
                        MaPhieuNhan = phieuNhan.MaPhieuNhan,
                        MaPhongCu = cu.MaPhong,
                        MaPhongMoi = d.MaPhong,
                        NgayDoi = Luc(ngayNhan, 9, 30),
                        ChenhLechGia = donGia - donGiaCu,
                        GhiChu = s.GhiChuDoiPhong
                    });
                }
            }

            // ---- dịch vụ đã dùng ----
            foreach (var dv in s.DichVu ?? [])
            {
                phieuNhan.ChiTietSuDungDVs.Add(new ChiTietSuDungDV
                {
                    MaPhieuNhan = phieuNhan.MaPhieuNhan,
                    MaDV = dv.MaDV,
                    SoLuong = dv.SoLuong,
                    ThanhTien = dichVus[dv.MaDV].GiaDV * dv.SoLuong,
                    NgaySuDung = Luc(ngayDon.AddDays(dv.NgayThu), 12, 20),
                    NguoiGhi = "letan"
                });
            }

            // ---- hóa đơn (chỉ khi đã trả hết phòng) ----
            if (!s.DangO)
                phieuNhan.HoaDons.Add(LapHoaDon(s, phieuNhan, dongLuuTru, tienCoc, ngayTraCuoi));

            context.PhieuNhanPhongs.Add(phieuNhan);
        }

        HoaDon LapHoaDon(LuuTruMau s, PhieuNhanPhong phieuNhan, List<ChiTietPhieuNhan> dongs, decimal tienCoc, DateOnly ngayTraCuoi)
        {
            var maHD = Ma("HD", ngayTraCuoi);
            var lines = new List<ChiTietHoaDon>();
            decimal tienPhong = 0, phuThu = 0, tienDV = 0;

            foreach (var o in dongs)
            {
                var loai = loaiPhongs[phongLoai[o.MaPhong]];
                var soDem = Math.Max(o.NgayTra!.Value.DayNumber - o.NgayNhan.DayNumber, 1);
                var tt = o.DonGia * soDem;
                tienPhong += tt;
                lines.Add(new ChiTietHoaDon { MaHD = maHD, LoaiKhoanMuc = LoaiKhoanMuc.TIEN_PHONG, MoTa = $"Tiền phòng {o.MaPhong} ({loai.TenLoai})", SoLuong = soDem, DonGia = o.DonGia, ThanhTien = tt });

                var nguoiThem = Math.Max(0, o.SoNguoi - loai.SoNguoiTieuChuan);
                if (nguoiThem > 0 && loai.PhuThu > 0)
                {
                    var pt = loai.PhuThu * nguoiThem * soDem;
                    phuThu += pt;
                    lines.Add(new ChiTietHoaDon { MaHD = maHD, LoaiKhoanMuc = LoaiKhoanMuc.PHU_THU, MoTa = $"Phụ thu {nguoiThem} người vượt tiêu chuẩn - phòng {o.MaPhong}", SoLuong = nguoiThem * soDem, DonGia = loai.PhuThu, ThanhTien = pt });
                }
                if (o.TienPhuThu > 0)
                {
                    phuThu += o.TienPhuThu;
                    lines.Add(new ChiTietHoaDon { MaHD = maHD, LoaiKhoanMuc = LoaiKhoanMuc.PHU_THU, MoTa = $"Phụ thu trả phòng trễ giờ - phòng {o.MaPhong}", SoLuong = 1, DonGia = o.TienPhuThu, ThanhTien = o.TienPhuThu });
                }
            }

            foreach (var dv in phieuNhan.ChiTietSuDungDVs.OrderBy(x => x.NgaySuDung))
            {
                tienDV += dv.ThanhTien;
                lines.Add(new ChiTietHoaDon { MaHD = maHD, LoaiKhoanMuc = LoaiKhoanMuc.DICH_VU, MoTa = dichVus[dv.MaDV].TenDV, SoLuong = dv.SoLuong, DonGia = dichVus[dv.MaDV].GiaDV, ThanhTien = dv.ThanhTien });
            }

            var tongKM = Math.Min(s.MaKM is null ? 0m : TinhKhuyenMai(s.MaKM, dongs.Sum(o => o.DonGia * Math.Max(o.NgayTra!.Value.DayNumber - o.NgayNhan.DayNumber, 1))), tienPhong);
            if (tongKM > 0)
                lines.Add(new ChiTietHoaDon { MaHD = maHD, LoaiKhoanMuc = LoaiKhoanMuc.KHUYEN_MAI, MoTa = "Khuyến mãi", SoLuong = 1, DonGia = -tongKM, ThanhTien = -tongKM });

            var conPhaiThu = Math.Max(0m, tienPhong + phuThu + tienDV - tongKM - tienCoc);
            var ngayLap = Luc(ngayTraCuoi, 11, 30);

            var hoaDon = new HoaDon
            {
                MaHD = maHD,
                MaPhieuNhan = phieuNhan.MaPhieuNhan,
                MaNV = s.MaNV,
                NgayLap = ngayLap,
                TienPhong = tienPhong,
                TienDV = tienDV,
                PhuThu = phuThu,
                TienDaCoc = tienCoc,
                TongTien = conPhaiThu,
                HinhThucThanhToan = HinhThucThanhToan.TIEN_MAT
            };
            foreach (var l in lines) hoaDon.ChiTietHoaDons.Add(l);

            // thanh toán: mặc định (null) = trả đủ 1 lần bằng tiền mặt; [] = chưa trả; danh sách rõ ràng = theo từng lần
            var ke = s.ThanhToan ?? [new TraTien(HinhThucThanhToan.TIEN_MAT)];
            decimal daTra = 0;
            for (var i = 0; i < ke.Length && conPhaiThu > 0; i++)
            {
                var soTien = ke[i].SoTien ?? (conPhaiThu - daTra);
                if (soTien <= 0) continue;
                hoaDon.ChiTietThanhToans.Add(new ChiTietThanhToan
                {
                    MaHD = maHD,
                    HinhThucThanhToan = ke[i].Hinh,
                    SoTien = soTien,
                    ThoiGianThanhToan = ngayLap.AddMinutes(5 + i * 20),
                    MaTaiKhoanThuNgan = s.MaNV,
                    MaGiaoDich = ke[i].MaGiaoDich
                });
                if (i == 0) hoaDon.HinhThucThanhToan = ke[i].Hinh;
                daTra += soTien;
            }
            hoaDon.TrangThaiThanhToan = conPhaiThu == 0 || daTra >= conPhaiThu
                ? TrangThaiThanhToan.DA_THANH_TOAN
                : daTra > 0 ? TrangThaiThanhToan.THANH_TOAN_MOT_PHAN : TrangThaiThanhToan.CHUA_THANH_TOAN;
            return hoaDon;
        }

        // ================== 2) PHIẾU ĐẶT KHÔNG PHÁT SINH LƯU TRÚ (tương lai / hủy / không đến) ==================
        var datPhongs = boSung ? DatPhongBoSung() : new List<DatPhongMau>
        {
            new("KH09", ["P202"], 3, 2, TrangThaiPhieuDat.DA_XAC_NHAN, DatTruocNgay: 4),
            new("KH10", ["P301"], 10, 3, TrangThaiPhieuDat.DA_XAC_NHAN, MaKM: "KM01", DatTruocNgay: 8),
            new("KH11", ["P401", "P402"], 20, 3, TrangThaiPhieuDat.DA_XAC_NHAN, LoaiDatPhong.DOAN, "KM02", 6),
            new("KH12", ["P202"], 30, 2, TrangThaiPhieuDat.CHO_XAC_NHAN, DatTruocNgay: 1),
            new("KH13", ["P301"], 2, 2, TrangThaiPhieuDat.DA_HUY, DatTruocNgay: 6),     // hủy < 30 ngày: không hoàn cọc
            new("KH14", ["P102"], 60, 2, TrangThaiPhieuDat.DA_HUY, DatTruocNgay: 12),   // hủy sớm >= 30 ngày: hoàn cọc
            new("KH15", ["P202"], -12, 2, TrangThaiPhieuDat.KHONG_DEN, DatTruocNgay: 9),
            new("KH15", ["P303"], 25, 2, TrangThaiPhieuDat.CHO_XAC_NHAN, DatTruocNgay: 0),
            new("KH02", ["P201"], 8, 2, TrangThaiPhieuDat.DA_XAC_NHAN, MaKM: "KM05", DatTruocNgay: 3),
            new("KH01", ["P102"], 12, 2, TrangThaiPhieuDat.CHO_XAC_NHAN, DatTruocNgay: 1),
            new("KH01", ["P303"], 40, 2, TrangThaiPhieuDat.DA_XAC_NHAN, MaKM: "KM01", DatTruocNgay: 2),
            new("KH03", ["P101"], 18, 4, TrangThaiPhieuDat.DA_XAC_NHAN, DatTruocNgay: 5),
            new("KH04", ["P401"], 35, 2, TrangThaiPhieuDat.DA_HUY, DatTruocNgay: 15)
        };

        foreach (var d in datPhongs)
        {
            var ngayDon = homNay.AddDays(d.BatDau);
            var ngayTra = ngayDon.AddDays(d.SoDem);
            var phieu = new PhieuDatPhong
            {
                MaPhieuDat = Ma("BK", ngayDon),
                MaKhach = d.MaKhach,
                NgayDat = Luc(ngayDon.AddDays(-d.DatTruocNgay), 15, 10),
                NgayDonDuKien = ngayDon,
                NgayTraDuKien = ngayTra,
                TrangThai = d.TrangThai,
                LoaiDatPhong = d.Loai
            };
            foreach (var maPhong in d.Phong)
            {
                var donGia = loaiPhongs[phongLoai[maPhong]].DonGia;
                phieu.ChiTietPhieuDats.Add(new ChiTietPhieuDat { MaPhieuDat = phieu.MaPhieuDat, MaPhong = maPhong, SoLuong = 1, DonGiaApDung = donGia, ThanhTien = donGia * d.SoDem });
            }
            var tong = phieu.ChiTietPhieuDats.Sum(x => x.ThanhTien);
            phieu.TienCoc = Math.Round(tong * 0.5m, 0, MidpointRounding.AwayFromZero);

            // phiếu đã hủy đã trả lại lượt khuyến mãi nên không ghi ChiTietKhuyenMai
            if (d.MaKM is not null && d.TrangThai != TrangThaiPhieuDat.DA_HUY)
            {
                phieu.ChiTietKhuyenMais.Add(new ChiTietKhuyenMai { MaPhieuDat = phieu.MaPhieuDat, MaKM = d.MaKM, TongKhuyenMai = TinhKhuyenMai(d.MaKM, tong) });
                khuyenMais[d.MaKM].SoLuongDaSuDung += 1;
            }
            context.PhieuDatPhongs.Add(phieu);
        }

        // Phòng đang có khách phải ở tình trạng "có khách" (OC) - trừ phòng đang ngưng sử dụng.
        if (boSung)
        {
            var phongDangO = luuTrus.Where(x => x.DangO).SelectMany(x => x.Phong).Select(x => x.MaPhong).ToHashSet();
            foreach (var p in await context.Phongs.Where(x => phongDangO.Contains(x.MaPhong) && x.TinhTrang != TinhTrangPhong.OOO).ToListAsync())
                p.TinhTrang = TinhTrangPhong.OC;
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Bộ bổ sung: đủ các tình huống quanh NGÀY TRẢ PHÒNG (check-out) - trả hôm nay, quá hạn chưa trả, ở dài ngày - và các lượt đã trả có ngày trả rõ ràng.
    /// Phòng chọn sao cho không trùng lịch với bộ dữ liệu gốc.
    /// </summary>
    private static List<LuuTruMau> LuuTruBoSung() => new()
    {
        // ---- đã trả phòng, thanh toán tiền mặt (ngày trả đã chốt) ----
        new("KH16", [new("P102", -12, 2)], 2, ThanhToan: [new(HinhThucThanhToan.TIEN_MAT)], DatTruocNgay: 8),
        new("KH17", [new("P303", -18, 3)], 2, DichVu: [new("DV01", 2, 1)], MaNV: "NV05", ThanhToan: [new(HinhThucThanhToan.TIEN_MAT)]),
        new("KH19", [new("P203", -4, 2)], 2, DichVu: [new("DV04", 2, 0)], ThanhToan: [new(HinhThucThanhToan.TIEN_MAT, 400000m)]),
        // ---- ĐANG Ở: ngày trả dự kiến là HÔM NAY (khách sẽ trả phòng trong ngày) ----
        new("KH16", [new("P202", -3, 3)], 2, DichVu: [new("DV04", 2, 0), new("DV05", 1, 1)], DatTruocNgay: 6, DangO: true),
        // ---- ĐANG Ở: QUÁ HẠN trả phòng (ngày trả dự kiến là hôm qua, chưa trả, 3 người -> có phụ thu) ----
        new("KH17", [new("P101", -4, 3)], 3, DichVu: [new("DV06", 2, 1)], DatTruocNgay: 5, DangO: true),
        // ---- ĐANG Ở dài ngày: trả sau 3 ngày nữa ----
        new("KH18", [new("P402", -2, 5)], 3, DichVu: [new("DV04", 3, 0)], DatTruocNgay: 9, DangO: true)
    };

    /// <summary>Phiếu đặt chưa phát sinh lưu trú của bộ bổ sung: đến hôm nay, đến ngày mai, đặt xa và đang chờ cọc.</summary>
    private static List<DatPhongMau> DatPhongBoSung() => new()
    {
        new("KH18", ["P102"], 0, 2, TrangThaiPhieuDat.DA_XAC_NHAN, DatTruocNgay: 4),
        new("KH19", ["P203"], 1, 2, TrangThaiPhieuDat.DA_XAC_NHAN, DatTruocNgay: 3),
        new("KH16", ["P303"], 5, 3, TrangThaiPhieuDat.DA_XAC_NHAN, DatTruocNgay: 2),
        new("KH19", ["P401"], 8, 2, TrangThaiPhieuDat.CHO_XAC_NHAN, DatTruocNgay: 1)
    };

    private static async Task SeedKhachBoSungAsync(KLCN060DbContext context)
    {
        var coSan = (await context.Khachs.Select(x => x.MaKhach).ToListAsync()).ToHashSet();
        var moi = new[]
        {
            new Khach { MaKhach = "KH16", HoTen = "Nguyễn Hoài Phương", SoDT = "0903445566", CCCD = "079198004411", Email = "phuong.nguyen@gmail.com", DiaChi = "27 Võ Thị Sáu, Quận 3, TP.HCM" },
            new Khach { MaKhach = "KH17", HoTen = "Châu Đức Thịnh", SoDT = "0916778899", CCCD = "079087002233", DiaChi = "15 Bà Triệu, TP. Đà Lạt" },
            new Khach { MaKhach = "KH18", HoTen = "Vũ Khánh Linh", SoDT = "0948110022", CCCD = "079200006677", Email = "linh.vu@gmail.com", DiaChi = "102 Trần Hưng Đạo, TP. Nha Trang" },
            new Khach { MaKhach = "KH19", HoTen = "Phan Quốc Bảo", SoDT = "0935667788", CCCD = "079092008899", Email = "bao.phan@outlook.com", DiaChi = "5 Hai Bà Trưng, TP. Huế" }
        };
        // Bỏ qua khách có số điện thoại/CCCD/email trùng với dữ liệu người dùng đã tự nhập.
        var soDT = (await context.Khachs.Select(x => x.SoDT).ToListAsync()).ToHashSet();
        var cccd = (await context.Khachs.Where(x => x.CCCD != null).Select(x => x.CCCD!).ToListAsync()).ToHashSet();
        var email = (await context.Khachs.Where(x => x.Email != null).Select(x => x.Email!.ToLower()).ToListAsync()).ToHashSet();
        var them = moi.Where(k => !coSan.Contains(k.MaKhach) && !soDT.Contains(k.SoDT) && !cccd.Contains(k.CCCD!)
            && (k.Email is null || !email.Contains(k.Email.ToLower()))).ToList();
        if (them.Count == 0) return;
        context.Khachs.AddRange(them);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Mọi lượt ở phải có ngày trả dự kiến (check-out). Dữ liệu mẫu cũ chưa có: lượt đã trả lấy đúng ngày trả thực tế; lượt đang ở lấy ngày trả dự kiến của phiếu đặt,
    /// khách vãng lai không có phiếu thì mặc định là ngày mai (hoặc một đêm kể từ ngày nhận nếu muộn hơn). IDEMPOTENT: chỉ đụng dòng còn trống.
    /// </summary>
    private static async Task BoSungNgayTraDuKienAsync(KLCN060DbContext context)
    {
        var thieu = await context.ChiTietPhieuNhans
            .Include(x => x.PhieuNhanPhong).ThenInclude(p => p.PhieuDatPhong)
            .Where(x => x.NgayTraDuKien == null)
            .ToListAsync();
        if (thieu.Count == 0) return;

        var ngayMai = DateOnly.FromDateTime(DateTime.Now).AddDays(1);
        foreach (var x in thieu)
        {
            if (x.NgayTra is { } daTra)
            {
                x.NgayTraDuKien = daTra;
                continue;
            }
            var toiThieu = x.NgayNhan.AddDays(1) > ngayMai ? x.NgayNhan.AddDays(1) : ngayMai;
            var duKien = x.PhieuNhanPhong.PhieuDatPhong?.NgayTraDuKien;
            x.NgayTraDuKien = duKien is { } d && d > x.NgayNhan ? d : toiThieu;
        }
        await context.SaveChangesAsync();
    }
}
