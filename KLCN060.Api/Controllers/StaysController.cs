using KLCN060.Api.DTOs.Common;
using KLCN060.Api.DTOs.Stays;
using KLCN060.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

/// <summary>Lượt lưu trú = ChiTietPhieuNhan, khóa kép (maPhieuNhan, maPhong) nên route dùng cả hai.</summary>
[ApiController]
[Route("api/v1/stays/{maPhieuNhan}/{maPhong}")]
[Authorize]
public class StaysController : ControllerBase
{
    private readonly IStayService _stayService;

    public StaysController(IStayService stayService)
    {
        _stayService = stayService;
    }

    [HttpPost("services")]
    [Authorize(Roles = "LE_TAN")]
    public async Task<IActionResult> AddService(string maPhieuNhan, string maPhong, [FromBody] AddStayServiceRequest request)
    {
        var result = await _stayService.AddServiceAsync(maPhieuNhan, maPhong, request);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<StayServiceUsageDto>.Ok(result));
    }

    [HttpGet("services")]
    [Authorize(Roles = "LE_TAN,KE_TOAN")]
    public async Task<IActionResult> GetServices(string maPhieuNhan, string maPhong)
    {
        var result = await _stayService.GetServicesAsync(maPhieuNhan, maPhong);
        return Ok(ApiResponse<List<StayServiceUsageDto>>.Ok(result));
    }

    [HttpPost("change-room")]
    [Authorize(Roles = "LE_TAN")]
    public async Task<IActionResult> ChangeRoom(string maPhieuNhan, string maPhong, [FromBody] ChangeRoomRequest request)
    {
        var result = await _stayService.ChangeRoomAsync(maPhieuNhan, maPhong, request);
        return Ok(ApiResponse<ChangeRoomResultDto>.Ok(result));
    }

    [HttpPost("check-out")]
    [Authorize(Roles = "LE_TAN")]
    public async Task<IActionResult> CheckOut(string maPhieuNhan, string maPhong, [FromBody] CheckOutRequest? request)
    {
        var result = await _stayService.CheckOutAsync(maPhieuNhan, maPhong, request ?? new CheckOutRequest(), CurrentUser.From(User));
        return Ok(ApiResponse<CheckOutResultDto>.Ok(result));
    }

    /// <summary>Xem trước folio của cả phiếu nhận (mọi phòng + dịch vụ), không ghi gì xuống DB.</summary>
    [HttpGet("folio")]
    [Authorize(Roles = "LE_TAN,KE_TOAN,KHACH_HANG")]
    public async Task<IActionResult> GetFolio(string maPhieuNhan, string maPhong)
    {
        var result = await _stayService.GetFolioAsync(maPhieuNhan, maPhong, CurrentUser.From(User));
        return Ok(ApiResponse<FolioDto>.Ok(result));
    }
}

/// <summary>Danh sách lượt lưu trú (mặc định đang ở) để màn hình lễ tân tìm MaPhieuNhan theo phòng.</summary>
[ApiController]
[Route("api/v1/stays")]
[Authorize(Roles = "LE_TAN,KE_TOAN,QUAN_LY")]
public class StaysQueryController : ControllerBase
{
    private readonly IStayService _stayService;

    public StaysQueryController(IStayService stayService)
    {
        _stayService = stayService;
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? maPhong, [FromQuery] string? trangThai)
    {
        var result = await _stayService.SearchAsync(maPhong, trangThai);
        return Ok(ApiResponse<List<StaySummaryDto>>.Ok(result));
    }
}
