using KLCN060.Domain;
using KLCN060.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Infrastructure;

public static partial class DataSeeder
{
    /// <summary>
    /// Danh mục đồ dùng cần kiểm trong phòng (khăn, ga, gối, nước, minibar, amenities...). Chỉ seed MỘT LẦN, đúng lúc quyền GHI_NHAN_BUONG_PHONG được thêm vào danh mục
    /// (CSDL mới hoặc lúc nâng cấp CSDL cũ) để quản lý xóa/sửa danh mục về sau không bị seed ghi đè. Minibar gắn với dịch vụ có sẵn (nước ngọt, bia) để tính phí cho khách.
    /// </summary>
    private static async Task SeedVatDungAsync(KLCN060DbContext context, bool laLanDauCoBuongPhong)
    {
        if (!laLanDauCoBuongPhong || await context.VatDungs.AnyAsync()) return;

        var dichVus = (await context.DichVus.Select(x => x.MaDV).ToListAsync()).ToHashSet();
        string? Dv(string ma) => dichVus.Contains(ma) ? ma : null;

        context.VatDungs.AddRange(
            // Định mức tính theo đơn vị của "cách tính": khăn/amenities/dép theo người tiêu chuẩn, ga/gối theo giường (phòng 2 giường cần gấp đôi), còn lại mỗi phòng.
            new VatDung { MaVatDung = "VT01", TenVatDung = "Khăn tắm", Nhom = NhomVatDung.KHAN_GA_GOI, DonViTinh = "cái", SoLuongChuan = 1, CachTinh = CachTinhDinhMuc.MOI_NGUOI },
            new VatDung { MaVatDung = "VT02", TenVatDung = "Khăn mặt", Nhom = NhomVatDung.KHAN_GA_GOI, DonViTinh = "cái", SoLuongChuan = 1, CachTinh = CachTinhDinhMuc.MOI_NGUOI },
            new VatDung { MaVatDung = "VT03", TenVatDung = "Ga giường", Nhom = NhomVatDung.KHAN_GA_GOI, DonViTinh = "bộ", SoLuongChuan = 1, CachTinh = CachTinhDinhMuc.MOI_GIUONG },
            new VatDung { MaVatDung = "VT04", TenVatDung = "Gối", Nhom = NhomVatDung.KHAN_GA_GOI, DonViTinh = "cái", SoLuongChuan = 2, CachTinh = CachTinhDinhMuc.MOI_GIUONG },
            new VatDung { MaVatDung = "VT05", TenVatDung = "Nước suối (miễn phí)", Nhom = NhomVatDung.NUOC_UONG, DonViTinh = "chai", SoLuongChuan = 1, CachTinh = CachTinhDinhMuc.MOI_NGUOI },
            new VatDung { MaVatDung = "VT06", TenVatDung = "Nước ngọt (Coca-Cola)", Nhom = NhomVatDung.MINIBAR, DonViTinh = "lon", SoLuongChuan = 2, MaDV = Dv("DV04") },
            new VatDung { MaVatDung = "VT07", TenVatDung = "Bia", Nhom = NhomVatDung.MINIBAR, DonViTinh = "lon", SoLuongChuan = 2, MaDV = Dv("DV05") },
            new VatDung { MaVatDung = "VT08", TenVatDung = "Bộ amenities (xà phòng, dầu gội, bàn chải)", Nhom = NhomVatDung.AMENITIES, DonViTinh = "bộ", SoLuongChuan = 1, CachTinh = CachTinhDinhMuc.MOI_NGUOI },
            new VatDung { MaVatDung = "VT09", TenVatDung = "Dép đi trong phòng", Nhom = NhomVatDung.AMENITIES, DonViTinh = "đôi", SoLuongChuan = 1, CachTinh = CachTinhDinhMuc.MOI_NGUOI },
            new VatDung { MaVatDung = "VT10", TenVatDung = "Ấm đun nước", Nhom = NhomVatDung.KHAC, DonViTinh = "cái", SoLuongChuan = 1 },
            new VatDung { MaVatDung = "VT11", TenVatDung = "Điều khiển TV / điều hòa", Nhom = NhomVatDung.KHAC, DonViTinh = "cái", SoLuongChuan = 2 });
        await context.SaveChangesAsync();
    }
}
