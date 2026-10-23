using KLCN060.Api.Authorization;
using KLCN060.Api.DTOs.Common;
using KLCN060.Api.DTOs.Housekeeping;
using KLCN060.Api.Services;
using KLCN060.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

/// <summary>Việc dọn phòng của buồng phòng: phòng bẩn xếp theo ưu tiên, nhận dọn / bỏ nhận, báo cáo dọn phòng cho quản lý.</summary>
[ApiController]
[Route("api/v1/housekeeping")]
[Authorize]
public class HousekeepingController : ControllerBase
{
    private readonly IHousekeepingService _housekeeping;

    public HousekeepingController(IHousekeepingService housekeeping)
    {
        _housekeeping = housekeeping;
    }

    /// <summary>Danh sách phòng cần dọn (VD/OD) theo mức ưu tiên, kèm ai đang dọn và số phòng đã dọn hôm nay.</summary>
    [HttpGet("tasks")]
    [HasPermission(Permissions.XacNhanDonPhong)]
    public async Task<IActionResult> Tasks()
        => Ok(ApiResponse<HousekeepingBoardDto>.Ok(await _housekeeping.GetBoardAsync(CurrentUser.From(User).TenDN)));

    /// <summary>Nhận dọn một phòng đang bẩn (mỗi phòng chỉ một người nhận).</summary>
    [HttpPost("rooms/{maPhong}/claim")]
    [HasPermission(Permissions.XacNhanDonPhong)]
    public async Task<IActionResult> Claim(string maPhong)
        => Ok(ApiResponse<HousekeepingTaskDto>.Ok(await _housekeeping.ClaimAsync(maPhong, CurrentUser.From(User).TenDN)));

    /// <summary>Bỏ nhận dọn (người đã nhận hoặc quản lý).</summary>
    [HttpDelete("rooms/{maPhong}/claim")]
    [HasPermission(Permissions.XacNhanDonPhong, Permissions.DuyetNghiepVu)]
    public async Task<IActionResult> Release(string maPhong)
    {
        await _housekeeping.ReleaseAsync(maPhong, CurrentUser.From(User).TenDN);
        return NoContent();
    }

    /// <summary>Bảng điều hành buồng phòng cho Quản lý: số phòng theo giai đoạn, phòng cần xử lý theo ưu tiên, phòng sắp có khách, phòng bảo trì, nhân viên đang làm gì.</summary>
    [HttpGet("overview")]
    [HasPermission(Permissions.DuyetNghiepVu)]
    public async Task<IActionResult> Overview()
        => Ok(ApiResponse<HousekeepingOverviewDto>.Ok(await _housekeeping.GetOverviewAsync(CurrentUser.From(User).TenDN)));

    /// <summary>Quản lý giao việc dọn một phòng bẩn cho nhân viên buồng phòng (nhân viên bấm "nhận dọn" để bắt đầu).</summary>
    [HttpPost("rooms/{maPhong}/assign")]
    [HasPermission(Permissions.DuyetNghiepVu)]
    public async Task<IActionResult> Assign(string maPhong, [FromBody] AssignCleaningRequest request)
        => Ok(ApiResponse<HousekeepingTaskDto>.Ok(await _housekeeping.AssignAsync(maPhong, request.NguoiDon, CurrentUser.From(User).TenDN)));

    /// <summary>Nhật ký buồng phòng: ai làm gì, phòng nào, lúc nào, tình trạng trước → sau (mặc định 7 ngày qua). Chỉ Quản lý xem (để trả lời khi có tranh chấp: ai đổi tình trạng phòng, ai bỏ khoản minibar, ai trả đồ, ai đóng sự cố).</summary>
    [HttpGet("activity")]
    [HasPermission(Permissions.DuyetNghiepVu)]
    public async Task<IActionResult> Activity([FromQuery] string? maPhong, [FromQuery] DateOnly? tuNgay, [FromQuery] DateOnly? denNgay, [FromQuery] int toiDa = 150)
        => Ok(ApiResponse<List<HousekeepingActivityDto>>.Ok(await _housekeeping.GetActivityAsync(maPhong, tuNgay, denNgay, toiDa)));

    /// <summary>Số phòng đã dọn theo nhân viên và thời gian dọn trung bình trong kỳ (mặc định 7 ngày qua).</summary>
    [HttpGet("cleaning-report")]
    [HasPermission(Permissions.DuyetNghiepVu)]
    public async Task<IActionResult> CleaningReport([FromQuery] DateOnly? tuNgay, [FromQuery] DateOnly? denNgay)
        => Ok(ApiResponse<CleaningReportDto>.Ok(await _housekeeping.GetCleaningReportAsync(tuNgay, denNgay)));
}
