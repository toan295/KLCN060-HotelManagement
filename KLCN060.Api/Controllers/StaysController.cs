using KLCN060.Api.Authorization;
using KLCN060.Domain;
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
    private readonly IAuditLogService _auditLogService;

    public StaysController(IStayService stayService, IAuditLogService auditLogService)
    {
        _stayService = stayService;
        _auditLogService = auditLogService;
    }

    [HttpPost("services")]
    [HasPermission(Permissions.ThaoTacLeTan)]
    public async Task<IActionResult> AddService(string maPhieuNhan, string maPhong, [FromBody] AddStayServiceRequest request)
    {
        var result = await _stayService.AddServiceAsync(maPhieuNhan, maPhong, request);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<StayServiceUsageDto>.Ok(result));
    }

    /// <summary>Ghi một gói dịch vụ cho khách đang ở (mỗi dịch vụ trong gói thành một dòng sử dụng, tổng đúng bằng giá gói).</summary>
    [HttpPost("service-packages")]
    [HasPermission(Permissions.ThaoTacLeTan)]
    public async Task<IActionResult> AddServicePackage(string maPhieuNhan, string maPhong, [FromBody] AddStayServicePackageRequest request)
    {
        var result = await _stayService.AddServicePackageAsync(maPhieuNhan, maPhong, request);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<List<StayServiceUsageDto>>.Ok(result));
    }

    /// <summary>Hủy dòng dịch vụ ghi nhầm (chỉ khi chưa lập hóa đơn; bắt buộc lý do).</summary>
    [HttpDelete("services/{maChiTietDV:int}")]
    [HasPermission(Permissions.ThaoTacLeTan)]
    public async Task<IActionResult> VoidService(string maPhieuNhan, string maPhong, int maChiTietDV, [FromQuery] string? lyDo)
    {
        var result = await _stayService.VoidServiceAsync(maPhieuNhan, maPhong, maChiTietDV, lyDo);
        return Ok(ApiResponse<StayServiceUsageDto>.Ok(result));
    }

    [HttpGet("services")]
    [HasPermission(Permissions.XemLuuTru)]
    public async Task<IActionResult> GetServices(string maPhieuNhan, string maPhong)
    {
        var result = await _stayService.GetServicesAsync(maPhieuNhan, maPhong);
        return Ok(ApiResponse<List<StayServiceUsageDto>>.Ok(result));
    }

    /// <summary>Xem trước đổi phòng (không ghi gì): hạng, giá, chênh lệch, tác động tiền, cần duyệt hay không, vì sao chưa đổi được.</summary>
    [HttpGet("change-room-preview")]
    [HasPermission(Permissions.ThaoTacLeTan)]
    public async Task<IActionResult> ChangeRoomPreview(string maPhieuNhan, string maPhong, [FromQuery] string? maPhongMoi)
        => Ok(ApiResponse<ChangeRoomPreviewDto>.Ok(await _stayService.XemTruocDoiPhongAsync(maPhieuNhan, maPhong, maPhongMoi)));

    /// <summary>Xem trước gia hạn: số đêm thêm, tiền thêm, ngày tối đa gia hạn được.</summary>
    [HttpGet("extend-preview")]
    [HasPermission(Permissions.ThaoTacLeTan)]
    public async Task<IActionResult> ExtendPreview(string maPhieuNhan, string maPhong, [FromQuery] DateOnly ngayTraMoi)
        => Ok(ApiResponse<ExtendPreviewDto>.Ok(await _stayService.XemTruocGiaHanAsync(maPhieuNhan, maPhong, ngayTraMoi)));

    /// <summary>Lịch sử thao tác của lượt lưu trú (đổi phòng, gia hạn, ghi/hủy dịch vụ...): ai, lúc nào, trước → sau, lý do.</summary>
    [HttpGet("history")]
    [HasPermission(Permissions.XemLuuTru)]
    public async Task<IActionResult> History(string maPhieuNhan, string maPhong)
    {
        await _stayService.KiemTraLuotTonTaiAsync(maPhieuNhan, maPhong);
        return Ok(ApiResponse<List<KLCN060.Api.DTOs.Audit.AuditLogDto>>.Ok(await _auditLogService.GetForObjectAsync(maPhieuNhan)));
    }

    [HttpPost("change-room")]
    [HasPermission(Permissions.ThaoTacLeTan)]
    public async Task<IActionResult> ChangeRoom(string maPhieuNhan, string maPhong, [FromBody] ChangeRoomRequest request)
    {
        var result = await _stayService.ChangeRoomAsync(maPhieuNhan, maPhong, request);
        return Ok(ApiResponse<ChangeRoomResultDto>.Ok(result));
    }

    /// <summary>Gia hạn (extend stay): dời ngày trả dự kiến của lượt ở; từ chối nếu phòng đã có đặt phòng khác trong những đêm thêm.</summary>
    [HttpPost("extend")]
    [HasPermission(Permissions.ThaoTacLeTan)]
    public async Task<IActionResult> Extend(string maPhieuNhan, string maPhong, [FromBody] ExtendStayRequest request)
    {
        var result = await _stayService.ExtendAsync(maPhieuNhan, maPhong, request);
        return Ok(ApiResponse<ExtendStayResultDto>.Ok(result));
    }

    /// <summary>Xem trước trả phòng (không ghi gì): khách/phòng/phiếu/ngày ở, folio (kể cả phụ thu trễ giờ nếu chọn), điều kiện trả phòng và giải thích số còn phải thu.</summary>
    [HttpGet("check-out-info")]
    [HasPermission(Permissions.ThaoTacLeTan)]
    public async Task<IActionResult> CheckOutInfo(string maPhieuNhan, string maPhong, [FromQuery] bool apDungPhuThuTreGio = false)
        => Ok(ApiResponse<CheckOutInfoDto>.Ok(await _stayService.XemTruocTraPhongAsync(maPhieuNhan, maPhong, apDungPhuThuTreGio)));

    [HttpPost("check-out")]
    [HasPermission(Permissions.ThaoTacLeTan)]
    public async Task<IActionResult> CheckOut(string maPhieuNhan, string maPhong, [FromBody] CheckOutRequest? request)
    {
        var result = await _stayService.CheckOutAsync(maPhieuNhan, maPhong, request ?? new CheckOutRequest(), CurrentUser.From(User));
        return Ok(ApiResponse<CheckOutResultDto>.Ok(result));
    }

    /// <summary>Xem trước folio của cả phiếu nhận (mọi phòng + dịch vụ), không ghi gì xuống DB.</summary>
    [HttpGet("folio")]
    [HasPermission(Permissions.XemLuuTru, AllowRoles = "KHACH_HANG")]
    public async Task<IActionResult> GetFolio(string maPhieuNhan, string maPhong)
    {
        var result = await _stayService.GetFolioAsync(maPhieuNhan, maPhong, CurrentUser.From(User));
        return Ok(ApiResponse<FolioDto>.Ok(result));
    }
}

/// <summary>Danh sách lượt lưu trú (mặc định đang ở) để màn hình lễ tân tìm MaPhieuNhan theo phòng.</summary>
[ApiController]
[Route("api/v1/stays")]
[HasPermission(Permissions.XemLuuTru)]
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
