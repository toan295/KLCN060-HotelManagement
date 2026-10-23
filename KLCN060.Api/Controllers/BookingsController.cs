using KLCN060.Api.Authorization;
using KLCN060.Domain;
using KLCN060.Api.DTOs.Bookings;
using KLCN060.Api.DTOs.Common;
using KLCN060.Api.DTOs.FrontDesk;
using KLCN060.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

[ApiController]
[Route("api/v1/bookings")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;
    private readonly ICheckInService _checkInService;
    private readonly IAuditLogService _auditLogService;

    public BookingsController(IBookingService bookingService, ICheckInService checkInService, IAuditLogService auditLogService)
    {
        _bookingService = bookingService;
        _checkInService = checkInService;
        _auditLogService = auditLogService;
    }

    [HttpPost]
    [HasPermission(Permissions.ThaoTacLeTan, AllowRoles = "KHACH_HANG")]
    public async Task<IActionResult> Create([FromBody] CreateBookingRequest request)
    {
        var result = await _bookingService.CreateAsync(request, CurrentUser.From(User));
        return StatusCode(StatusCodes.Status201Created, ApiResponse<BookingDto>.Ok(result));
    }

    [HttpGet]
    [HasPermission(Permissions.XemDatPhong)]
    public async Task<IActionResult> Search([FromQuery] BookingListQuery query)
    {
        var (items, total) = await _bookingService.SearchAsync(query);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var meta = new { page, pageSize, totalItems = total, totalPages = (int)Math.Ceiling(total / (double)pageSize) };
        return Ok(ApiResponse<List<BookingSummaryDto>>.Ok(items, meta));
    }

    [HttpGet("me")]
    [Authorize(Roles = "KHACH_HANG")]
    public async Task<IActionResult> GetMine()
    {
        var result = await _bookingService.GetMineAsync(CurrentUser.From(User).MaKhach);
        return Ok(ApiResponse<List<BookingSummaryDto>>.Ok(result));
    }

    [HttpGet("{maPhieuDat}")]
    [HasPermission(Permissions.XemDatPhong, AllowRoles = "KHACH_HANG")]
    public async Task<IActionResult> GetById(string maPhieuDat)
    {
        var result = await _bookingService.GetByIdAsync(maPhieuDat, CurrentUser.From(User));
        return Ok(ApiResponse<BookingDto>.Ok(result));
    }

    /// <summary>Tổng quan một phiếu cho nhân viên: khách, lưu trú, phòng, thanh toán (đã thu / còn lại), ai tạo / ai sửa, chính sách hủy, thao tác được phép, lịch sử gần nhất.</summary>
    [HttpGet("{maPhieuDat}/overview")]
    [HasPermission(Permissions.XemDatPhong)]
    public async Task<IActionResult> Overview(string maPhieuDat)
        => Ok(ApiResponse<BookingOverviewDto>.Ok(await _bookingService.GetOverviewAsync(maPhieuDat, CurrentUser.From(User))));

    // Chỉ lễ tân (đã thu tiền cọc TIỀN MẶT tại quầy) mới xác nhận tay được. Khách hàng trực tuyến phải chuyển khoản qua mã QR:
    // xác nhận cọc khi khách chỉ "bấm đã chuyển" sẽ cho phép giữ phòng mà không trả tiền.
    [HttpPost("{maPhieuDat}/confirm-deposit")]
    [HasPermission(Permissions.ThaoTacLeTan)]
    public async Task<IActionResult> ConfirmDeposit(string maPhieuDat)
    {
        var result = await _bookingService.ConfirmDepositAsync(maPhieuDat, CurrentUser.From(User));
        return Ok(ApiResponse<BookingDto>.Ok(result));
    }

    /// <summary>Sửa phiếu chưa nhận phòng: đổi ngày đón/trả, đổi phòng, đổi loại phòng (lễ tân hoặc Quản lý) - bắt buộc lý do, tính lại giá, ghi nhật ký trước/sau.</summary>
    [HttpPut("{maPhieuDat}")]
    [HasPermission(Permissions.ThaoTacLeTan, Permissions.DuyetNghiepVu)]
    public async Task<IActionResult> Update(string maPhieuDat, [FromBody] UpdateBookingRequest request)
    {
        var result = await _bookingService.UpdateAsync(maPhieuDat, request, CurrentUser.From(User));
        return Ok(ApiResponse<BookingDto>.Ok(result));
    }

    /// <summary>
    /// Xem trước thay đổi (KHÔNG lưu gì): cùng kiểm tra phòng trống/giá/khuyến mãi như khi sửa thật, trả giá cũ → mới từng phòng, chênh lệch và tiền cọc
    /// để lễ tân đọc cho khách rồi mới xác nhận.
    /// </summary>
    [HttpPost("{maPhieuDat}/preview-update")]
    [HasPermission(Permissions.ThaoTacLeTan, Permissions.DuyetNghiepVu)]
    public async Task<IActionResult> PreviewUpdate(string maPhieuDat, [FromBody] UpdateBookingRequest request)
        => Ok(ApiResponse<BookingChangePreviewDto>.Ok(await _bookingService.PreviewUpdateAsync(maPhieuDat, request, CurrentUser.From(User))));

    /// <summary>Phòng có thể đổi sang cho một phòng đang giữ của phiếu: CÙNG loại phòng khách đã đặt, còn trống trong khoảng ngày (mặc định ngày của phiếu), không bảo trì.</summary>
    [HttpGet("{maPhieuDat}/swap-rooms")]
    [HasPermission(Permissions.ThaoTacLeTan, Permissions.DuyetNghiepVu)]
    public async Task<IActionResult> SwapRooms(string maPhieuDat, [FromQuery] string maPhongCu, [FromQuery] DateOnly? nhan, [FromQuery] DateOnly? tra)
        => Ok(ApiResponse<List<VacantRoomDto>>.Ok(await _bookingService.GetSwapRoomsAsync(maPhieuDat, maPhongCu, nhan, tra)));

    // Lễ tân hủy theo chính sách; Quản lý (DUYET_NGHIEP_VU) hủy trong trường hợp đặc biệt (bắt buộc ghi lý do); hoàn cọc luôn theo chính sách (hủy trước 30 ngày thì hoàn).
    [HttpPost("{maPhieuDat}/cancel")]
    [HasPermission(Permissions.ThaoTacLeTan, Permissions.DuyetNghiepVu, AllowRoles = "KHACH_HANG")]
    public async Task<IActionResult> Cancel(string maPhieuDat, [FromBody] CancelBookingRequest? request)
    {
        var result = await _bookingService.CancelAsync(maPhieuDat, request ?? new CancelBookingRequest(), CurrentUser.From(User));
        return Ok(ApiResponse<CancelBookingResultDto>.Ok(result));
    }

    /// <summary>Đánh dấu phiếu quá ngày đón mà khách không đến (mất cọc, trả lại phòng cho các đặt phòng khác).</summary>
    [HttpPost("{maPhieuDat}/no-show")]
    [HasPermission(Permissions.ThaoTacLeTan, Permissions.DuyetNghiepVu)]
    public async Task<IActionResult> NoShow(string maPhieuDat, [FromBody] NoShowRequest? request)
    {
        var result = await _bookingService.MarkNoShowAsync(maPhieuDat, request ?? new NoShowRequest(), CurrentUser.From(User));
        return Ok(ApiResponse<BookingDto>.Ok(result));
    }

    /// <summary>Báo giá trước khi tạo (KHÔNG lưu, không giữ phòng): phòng còn trống, tiền phòng, khuyến mãi áp dụng được hay không và tiền cọc.</summary>
    [HttpPost("quote")]
    [HasPermission(Permissions.ThaoTacLeTan)]
    public async Task<IActionResult> Quote([FromBody] CreateBookingRequest request)
        => Ok(ApiResponse<BookingQuoteDto>.Ok(await _bookingService.QuoteAsync(request)));

    /// <summary>Lịch sử thay đổi của phiếu đặt (tạo, xác nhận cọc, nhận phòng, hủy/không đến...).</summary>
    [HttpGet("{maPhieuDat}/history")]
    [HasPermission(Permissions.XemDatPhong)]
    public async Task<IActionResult> History(string maPhieuDat)
    {
        // Phiếu không tồn tại phải báo 404 (trước đây trả 200 kèm cả dòng nhật ký thao tác thất bại do chính mã gõ vào).
        await _bookingService.GetByIdAsync(maPhieuDat, CurrentUser.From(User));
        return Ok(ApiResponse<List<KLCN060.Api.DTOs.Audit.AuditLogDto>>.Ok(await _auditLogService.GetForObjectAsync(maPhieuDat)));
    }

    /// <summary>Người ở cùng đã khai báo trong phiếu (khách đại diện không nằm trong danh sách này).</summary>
    [HttpGet("{maPhieuDat}/guests")]
    [HasPermission(Permissions.XemDatPhong, AllowRoles = "KHACH_HANG")]
    public async Task<IActionResult> Companions(string maPhieuDat)
        => Ok(ApiResponse<List<CompanionDto>>.Ok(await _bookingService.GetCompanionsAsync(maPhieuDat, CurrentUser.From(User))));

    /// <summary>Thêm người ở cùng vào phiếu (phiếu còn hiệu lực hoặc đang ở).</summary>
    [HttpPost("{maPhieuDat}/guests")]
    [HasPermission(Permissions.ThaoTacLeTan)]
    public async Task<IActionResult> AddCompanion(string maPhieuDat, [FromBody] AddCompanionRequest request)
    {
        var result = await _bookingService.AddCompanionAsync(maPhieuDat, request, CurrentUser.From(User));
        return StatusCode(StatusCodes.Status201Created, ApiResponse<CompanionDto>.Ok(result));
    }

    [HttpDelete("{maPhieuDat}/guests/{maKhachDiCung:int}")]
    [HasPermission(Permissions.ThaoTacLeTan)]
    public async Task<IActionResult> RemoveCompanion(string maPhieuDat, int maKhachDiCung)
    {
        await _bookingService.RemoveCompanionAsync(maPhieuDat, maKhachDiCung, CurrentUser.From(User));
        return NoContent();
    }

    /// <summary>Thông tin tổng hợp để quyết định nhận phòng: khách, ngày, giá/cọc, từng phòng có nhận được không và vì sao.</summary>
    [HttpGet("{maPhieuDat}/check-in-info")]
    [HasPermission(Permissions.ThaoTacLeTan)]
    public async Task<IActionResult> CheckInInfo(string maPhieuDat)
        => Ok(ApiResponse<CheckInInfoDto>.Ok(await _checkInService.LayThongTinNhanPhongAsync(maPhieuDat)));

    [HttpPost("{maPhieuDat}/check-in")]
    [HasPermission(Permissions.ThaoTacLeTan)]
    public async Task<IActionResult> CheckIn(string maPhieuDat, [FromBody] BookingCheckInRequest request)
    {
        var result = await _checkInService.CheckInBookingAsync(maPhieuDat, request);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<CheckInResultDto>.Ok(result));
    }
}
