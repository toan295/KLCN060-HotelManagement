using KLCN060.Api.Authorization;
using KLCN060.Api.DTOs.Common;
using KLCN060.Api.DTOs.Housekeeping;
using KLCN060.Api.Services;
using KLCN060.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

/// <summary>Sự cố trong phòng: nhân viên báo, quản lý tiếp nhận và xử lý.</summary>
[ApiController]
[Route("api/v1/incidents")]
[Authorize]
public class IncidentsController : ControllerBase
{
    private readonly IIncidentService _incidents;

    public IncidentsController(IIncidentService incidents)
    {
        _incidents = incidents;
    }

    /// <summary>Danh sách sự cố; lọc theo trạng thái (hoặc DANG_MO = chưa xử lý xong), phòng, mức độ, loại, người báo.</summary>
    [HttpGet]
    [HasPermission(Permissions.GhiNhanBuongPhong, Permissions.XuLySuCo)]
    public async Task<IActionResult> GetAll([FromQuery] string? trangThai, [FromQuery] string? maPhong, [FromQuery] string? mucDo, [FromQuery] string? loai, [FromQuery] string? nguoiBao)
        => Ok(ApiResponse<List<IncidentDto>>.Ok(await _incidents.GetAllAsync(trangThai, maPhong, mucDo, loai, nguoiBao)));

    [HttpGet("{maSuCo:int}")]
    [HasPermission(Permissions.GhiNhanBuongPhong, Permissions.XuLySuCo)]
    public async Task<IActionResult> Get(int maSuCo)
        => Ok(ApiResponse<IncidentDto>.Ok(await _incidents.GetAsync(maSuCo)));

    /// <summary>Báo sự cố trong phòng (VD: Phòng 305 - Điều hòa không lạnh - Mức độ Cao).</summary>
    [HttpPost]
    [HasPermission(Permissions.GhiNhanBuongPhong, Permissions.XuLySuCo)]
    public async Task<IActionResult> Create([FromBody] CreateIncidentRequest request)
    {
        var ketQua = await _incidents.CreateAsync(request, CurrentUser.From(User).TenDN);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<IncidentDto>.Ok(ketQua));
    }

    /// <summary>Tiếp nhận / đang xử lý / đã xử lý / hủy (người báo chỉ hủy được sự cố báo nhầm khi chưa ai tiếp nhận).</summary>
    [HttpPatch("{maSuCo:int}/status")]
    [HasPermission(Permissions.GhiNhanBuongPhong, Permissions.XuLySuCo)]
    public async Task<IActionResult> UpdateStatus(int maSuCo, [FromBody] UpdateIncidentStatusRequest request)
        => Ok(ApiResponse<IncidentDto>.Ok(await _incidents.UpdateStatusAsync(maSuCo, request, CurrentUser.From(User).TenDN)));
}
