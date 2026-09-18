using KLCN060.Api.DTOs.Facilities;
using KLCN060.Api.Middlewares;
using KLCN060.Domain;
using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

public class FacilityService : IFacilityService
{
    private readonly KLCN060DbContext _context;

    public FacilityService(KLCN060DbContext context)
    {
        _context = context;
    }

    public async Task<List<FacilityDto>> GetByRoomAsync(string maPhong)
    {
        if (!await _context.Phongs.AnyAsync(x => x.MaPhong == maPhong))
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_PHONG", "Không tìm thấy phòng.");

        return await _context.CoSoVatChats
            .Where(x => x.MaPhong == maPhong)
            .OrderBy(x => x.MaSo)
            .Select(x => ToDto(x))
            .ToListAsync();
    }

    public async Task<FacilityDto> CreateForRoomAsync(string maPhong, FacilityRequest request)
    {
        if (!await _context.Phongs.AnyAsync(x => x.MaPhong == maPhong))
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_PHONG", "Không tìm thấy phòng.");

        var maHienCo = await _context.CoSoVatChats.Select(x => x.MaSo).ToListAsync();
        var maSo = MaCodeGenerator.GenerateNext(maHienCo, "CSVC", 3);

        var coSoVatChat = new CoSoVatChat
        {
            MaSo = maSo,
            Ten = request.Ten,
            SoLuong = request.SoLuong,
            TinhTrang = request.TinhTrang,
            MaPhong = maPhong
        };

        _context.CoSoVatChats.Add(coSoVatChat);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "XUNG_DOT_DU_LIEU", "Có xung đột dữ liệu khi tạo cơ sở vật chất, vui lòng thử lại.");
        }

        return ToDto(coSoVatChat);
    }

    public async Task<FacilityDto> UpdateAsync(string maSo, FacilityRequest request)
    {
        var coSoVatChat = await TimCoSoVatChatAsync(maSo);

        coSoVatChat.Ten = request.Ten;
        coSoVatChat.SoLuong = request.SoLuong;
        coSoVatChat.TinhTrang = request.TinhTrang;

        await _context.SaveChangesAsync();
        return ToDto(coSoVatChat);
    }

    public async Task DeleteAsync(string maSo)
    {
        var coSoVatChat = await TimCoSoVatChatAsync(maSo);
        _context.CoSoVatChats.Remove(coSoVatChat);
        await _context.SaveChangesAsync();
    }

    private async Task<CoSoVatChat> TimCoSoVatChatAsync(string maSo)
    {
        var coSoVatChat = await _context.CoSoVatChats.FirstOrDefaultAsync(x => x.MaSo == maSo);
        if (coSoVatChat is null)
            throw new ApiException(StatusCodes.Status404NotFound, "KHONG_TIM_THAY_CO_SO_VAT_CHAT", "Không tìm thấy cơ sở vật chất.");
        return coSoVatChat;
    }

    private static FacilityDto ToDto(CoSoVatChat x) => new()
    {
        MaSo = x.MaSo,
        Ten = x.Ten,
        SoLuong = x.SoLuong,
        TinhTrang = x.TinhTrang,
        MaPhong = x.MaPhong
    };
}
