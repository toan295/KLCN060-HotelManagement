using KLCN060.Api.Authorization;
using KLCN060.Api.DTOs.Common;
using KLCN060.Api.DTOs.Rates;
using KLCN060.Api.Services;
using KLCN060.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

/// <summary>Bảng giá phòng: quy tắc giá cuối tuần / mùa cao điểm / lễ tết / giá đoàn (quyền QUAN_LY_GIA) và báo giá theo khoảng đêm.</summary>
[ApiController]
[Route("api/v1/rates")]
[Authorize]
public class RatesController : ControllerBase
{
    private readonly IRateService _rateService;

    public RatesController(IRateService rateService)
    {
        _rateService = rateService;
    }

    [HttpGet]
    [HasPermission(Permissions.QuanLyGia)]
    public async Task<IActionResult> GetAll()
        => Ok(ApiResponse<List<RateRuleDto>>.Ok(await _rateService.GetAllAsync()));

    /// <summary>Lịch sử thay đổi bảng giá (ai · lúc nào · trước → sau): toàn bộ hoặc theo một quy tắc (<c>?maQuyTac=</c>, kể cả quy tắc đã xóa).</summary>
    [HttpGet("history")]
    [HasPermission(Permissions.QuanLyGia)]
    public async Task<IActionResult> History([FromQuery] int? maQuyTac)
        => Ok(ApiResponse<List<RateHistoryEntryDto>>.Ok(await _rateService.GetHistoryAsync(maQuyTac)));

    /// <summary>Giá đã chốt từng đêm của một phiếu đặt: giá cơ bản, quy tắc đã áp dụng, giá cuối và so với bảng giá hiện hành.</summary>
    [HttpGet("bookings/{maPhieuDat}")]
    [HasPermission(Permissions.QuanLyGia, Permissions.XemDatPhong)]
    public async Task<IActionResult> BookingPrice(string maPhieuDat)
        => Ok(ApiResponse<BookingPriceBreakdownDto>.Ok(await _rateService.GetBookingPriceAsync(maPhieuDat)));

    [HttpPost]
    [HasPermission(Permissions.QuanLyGia)]
    public async Task<IActionResult> Create([FromBody] RateRuleRequest request)
        => StatusCode(StatusCodes.Status201Created, ApiResponse<RateRuleDto>.Ok(await _rateService.CreateAsync(request)));

    [HttpPut("{maQuyTac:int}")]
    [HasPermission(Permissions.QuanLyGia)]
    public async Task<IActionResult> Update(int maQuyTac, [FromBody] RateRuleRequest request)
        => Ok(ApiResponse<RateRuleDto>.Ok(await _rateService.UpdateAsync(maQuyTac, request)));

    [HttpDelete("{maQuyTac:int}")]
    [HasPermission(Permissions.QuanLyGia)]
    public async Task<IActionResult> Delete(int maQuyTac)
    {
        await _rateService.DeleteAsync(maQuyTac);
        return NoContent();
    }

    /// <summary>Báo giá từng đêm + đơn giá bình quân cho một loại phòng trong khoảng [checkin, checkout).</summary>
    [HttpGet("quote")]
    [HasPermission(Permissions.QuanLyGia, Permissions.XemDatPhong, Permissions.ThaoTacLeTan, AllowRoles = "KHACH_HANG")]
    public async Task<IActionResult> Quote([FromQuery] string maLoai, [FromQuery] DateOnly checkin, [FromQuery] DateOnly checkout, [FromQuery] string? loaiDatPhong)
    {
        var bao = await _rateService.QuoteAsync(maLoai, checkin, checkout, loaiDatPhong);
        // Khách hàng chỉ thấy giá + tên/mức quy tắc ĐÃ ÁP DỤNG của đêm đó (QuyTac/DieuChinh); danh sách các quy tắc khớp
        // (có thể là chính sách nội bộ) và độ ưu tiên bị ẩn.
        if (User.IsInRole("KHACH_HANG"))
        {
            foreach (var dem in bao.TheoDem)
            {
                dem.CacQuyTacKhop = new();
                dem.UuTien = null;
            }
        }
        return Ok(ApiResponse<RateQuoteDto>.Ok(bao));
    }
}
