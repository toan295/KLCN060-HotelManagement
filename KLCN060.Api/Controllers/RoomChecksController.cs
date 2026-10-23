using KLCN060.Api.Authorization;
using KLCN060.Api.DTOs.Common;
using KLCN060.Api.DTOs.Housekeeping;
using KLCN060.Api.Services;
using KLCN060.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

/// <summary>Kiểm đồ dùng / minibar của phòng và khoản tiêu thụ gửi lễ tân tính phí.</summary>
[ApiController]
[Route("api/v1/room-checks")]
[Authorize]
public class RoomChecksController : ControllerBase
{
    private readonly IRoomCheckService _checks;

    public RoomChecksController(IRoomCheckService checks)
    {
        _checks = checks;
    }

    /// <summary>Mẫu kiểm của một phòng: các đồ dùng đang kiểm, số lượng "trước" gợi ý (định mức hoặc kết quả kiểm trước trong cùng lượt ở), phòng có khách hay không.</summary>
    [HttpGet("template/{maPhong}")]
    [HasPermission(Permissions.GhiNhanBuongPhong)]
    public async Task<IActionResult> Template(string maPhong)
        => Ok(ApiResponse<RoomCheckTemplateDto>.Ok(await _checks.GetTemplateAsync(maPhong)));

    [HttpPost]
    [HasPermission(Permissions.GhiNhanBuongPhong)]
    public async Task<IActionResult> Create([FromBody] CreateRoomCheckRequest request)
        => StatusCode(StatusCodes.Status201Created, ApiResponse<RoomCheckDto>.Ok(await _checks.CreateAsync(request, CurrentUser.From(User).TenDN)));

    [HttpGet]
    [HasPermission(Permissions.GhiNhanBuongPhong)]
    public async Task<IActionResult> GetAll([FromQuery] string? maPhong, [FromQuery] DateOnly? tuNgay, [FromQuery] DateOnly? denNgay, [FromQuery] string? nguoiKiem)
        => Ok(ApiResponse<List<RoomCheckDto>>.Ok(await _checks.GetAllAsync(maPhong, tuNgay, denNgay, nguoiKiem)));

    [HttpGet("{maPhieuKiem:int}")]
    [HasPermission(Permissions.GhiNhanBuongPhong)]
    public async Task<IActionResult> Get(int maPhieuKiem)
        => Ok(ApiResponse<RoomCheckDto>.Ok(await _checks.GetAsync(maPhieuKiem)));

    /// <summary>Hủy phiếu kiểm nhập nhầm (người nhập trong 60 phút hoặc quản lý; bắt buộc lý do; chưa có khoản đã tính phí hoặc đã bỏ qua).</summary>
    [HttpDelete("{maPhieuKiem:int}")]
    [HasPermission(Permissions.GhiNhanBuongPhong)]
    public async Task<IActionResult> Delete(int maPhieuKiem, [FromQuery] string? lyDo)
    {
        await _checks.DeleteAsync(maPhieuKiem, lyDo ?? "", CurrentUser.From(User).TenDN);
        return NoContent();
    }

    /// <summary>Khoản tiêu thụ minibar chờ lễ tân tính phí (mặc định CHO_TINH_PHI; xem lại DA_TINH_PHI / BO_QUA).</summary>
    [HttpGet("minibar-charges")]
    [HasPermission(Permissions.ThaoTacLeTan, Permissions.DuyetNghiepVu)]
    public async Task<IActionResult> Charges([FromQuery] string? trangThai, [FromQuery] string? maPhong)
        => Ok(ApiResponse<List<MinibarChargeDto>>.Ok(await _checks.GetChargesAsync(trangThai, maPhong)));

    /// <summary>Tính phí khoản minibar cho khách đang ở (ghi thành dòng dịch vụ của lượt lưu trú).</summary>
    [HttpPost("minibar-charges/{maChiTiet:int}/charge")]
    [HasPermission(Permissions.ThaoTacLeTan)]
    public async Task<IActionResult> Charge(int maChiTiet)
        => Ok(ApiResponse<MinibarChargeDto>.Ok(await _checks.ChargeAsync(maChiTiet, CurrentUser.From(User).TenDN)));

    /// <summary>Bỏ qua khoản minibar (không tính phí khách), bắt buộc lý do.</summary>
    [HttpPost("minibar-charges/{maChiTiet:int}/waive")]
    [HasPermission(Permissions.ThaoTacLeTan, Permissions.DuyetNghiepVu)]
    public async Task<IActionResult> Waive(int maChiTiet, [FromBody] WaiveMinibarChargeRequest request)
        => Ok(ApiResponse<MinibarChargeDto>.Ok(await _checks.WaiveAsync(maChiTiet, request.LyDo, CurrentUser.From(User).TenDN)));

    /// <summary>Tổng hợp hao hụt theo đồ dùng (tiêu thụ, mất/thiếu, hư hỏng) trong kỳ (mặc định 30 ngày qua).</summary>
    [HttpGet("loss-report")]
    [HasPermission(Permissions.DuyetNghiepVu)]
    public async Task<IActionResult> LossReport([FromQuery] DateOnly? tuNgay, [FromQuery] DateOnly? denNgay)
        => Ok(ApiResponse<List<SupplyLossRowDto>>.Ok(await _checks.GetLossReportAsync(tuNgay, denNgay)));
}
