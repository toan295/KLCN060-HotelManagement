using KLCN060.Api.DTOs.Promotions;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Domain.Enums;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public class PromotionService : IPromotionService
{
    private readonly KLCN060DbContext _context;

    public PromotionService(KLCN060DbContext context)
    {
        _context = context;
    }

    public async Task<List<PromotionDto>> GetActiveAsync()
    {
        var homNay = DateOnly.FromDateTime(DateTime.Now);

        return await _context.KhuyenMais
            .Where(x => x.NgayBatDau <= homNay && x.NgayKetThuc >= homNay
                     && (x.SoLuongGioiHan == null || x.SoLuongDaSuDung < x.SoLuongGioiHan))
            .OrderBy(x => x.MaKM)
            .Select(x => ToDto(x))
            .ToListAsync();
    }

    public async Task<List<PromotionDto>> GetAllAsync()
    {
        return await _context.KhuyenMais
            .OrderBy(x => x.MaKM)
            .Select(x => ToDto(x))
            .ToListAsync();
    }

    public async Task<PromotionDto> CreateAsync(PromotionRequest request)
    {
        var loaiKM = ParseLoaiKM(request.LoaiKM);
        KiemTraGiaTri(loaiKM, request.GiaTri);
        KiemTraNgay(request.NgayBatDau, request.NgayKetThuc);

        var maHienCo = await _context.KhuyenMais.Select(x => x.MaKM).ToListAsync();
        var maKM = MaCodeGenerator.GenerateNext(maHienCo, "KM", 2);

        var khuyenMai = new KhuyenMai
        {
            MaKM = maKM,
            TenKM = request.TenKM,
            PhanTramKM = request.PhanTramKM,
            NgayBatDau = request.NgayBatDau,
            NgayKetThuc = request.NgayKetThuc,
            DieuKien = request.DieuKien,
            LoaiKM = loaiKM,
            GiaTri = request.GiaTri,
            SoLuongGioiHan = request.SoLuongGioiHan,
            SoLuongDaSuDung = 0
        };

        _context.KhuyenMais.Add(khuyenMai);
        await _context.SaveChangesAsync();

        return ToDto(khuyenMai);
    }

    public async Task<PromotionDto> UpdateAsync(string maKM, PromotionRequest request)
    {
        var khuyenMai = await _context.KhuyenMais.FirstOrDefaultAsync(x => x.MaKM == maKM);
        if (khuyenMai is null)
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_KHUYEN_MAI", "Không tìm thấy khuyến mãi.");

        var loaiKM = ParseLoaiKM(request.LoaiKM);
        KiemTraGiaTri(loaiKM, request.GiaTri);
        KiemTraNgay(request.NgayBatDau, request.NgayKetThuc);

        khuyenMai.TenKM = request.TenKM;
        khuyenMai.PhanTramKM = request.PhanTramKM;
        khuyenMai.NgayBatDau = request.NgayBatDau;
        khuyenMai.NgayKetThuc = request.NgayKetThuc;
        khuyenMai.DieuKien = request.DieuKien;
        khuyenMai.LoaiKM = loaiKM;
        khuyenMai.GiaTri = request.GiaTri;
        khuyenMai.SoLuongGioiHan = request.SoLuongGioiHan;

        await _context.SaveChangesAsync();
        return ToDto(khuyenMai);
    }

    private static void KiemTraNgay(DateOnly batDau, DateOnly ketThuc)
    {
        if (ketThuc < batDau)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_KHONG_HOP_LE", "Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.");
    }

    private static void KiemTraGiaTri(LoaiKhuyenMai loaiKM, decimal? giaTri)
    {
        if (loaiKM != LoaiKhuyenMai.QUA_TANG && giaTri is null)
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_GIA_TRI_KHUYEN_MAI", "GiaTri là bắt buộc khi LoaiKM khác QUA_TANG.", "giaTri");
    }

    private static LoaiKhuyenMai ParseLoaiKM(string loaiKM)
    {
        if (!Enum.TryParse<LoaiKhuyenMai>(loaiKM, ignoreCase: true, out var ketQua))
            throw new ApiException(StatusCodes.Status400BadRequest, "LOAI_KHUYEN_MAI_KHONG_HOP_LE", "LoaiKM không hợp lệ. Chỉ chấp nhận: PHAN_TRAM, SO_TIEN_CO_DINH, QUA_TANG.", "loaiKM");
        return ketQua;
    }

    private static PromotionDto ToDto(KhuyenMai x) => new()
    {
        MaKM = x.MaKM,
        TenKM = x.TenKM,
        PhanTramKM = x.PhanTramKM,
        NgayBatDau = x.NgayBatDau,
        NgayKetThuc = x.NgayKetThuc,
        DieuKien = x.DieuKien,
        LoaiKM = x.LoaiKM.ToString(),
        GiaTri = x.GiaTri,
        SoLuongGioiHan = x.SoLuongGioiHan,
        SoLuongDaSuDung = x.SoLuongDaSuDung
    };
}
