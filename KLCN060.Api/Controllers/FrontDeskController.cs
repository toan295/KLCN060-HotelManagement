using KLCN060.Api.Authorization;
using KLCN060.Api.DTOs.Common;
using KLCN060.Api.DTOs.FrontDesk;
using KLCN060.Api.Middlewares;
using KLCN060.Api.Services;
using KLCN060.Domain;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

/// <summary>Bảng theo dõi ca trực của lễ tân: tình hình phòng, khách đến/đi hôm nay, khách đang ở, phòng trống, khách không đến (chỉ đọc).</summary>
[ApiController]
[Route("api/v1/frontdesk")]
[LoaiTruVaiTro("QUAN_LY", MaLoi = "CA_TRUC_KHONG_DANH_CHO_QUAN_LY", ThongBao = "Màn hình Ca trực chỉ dành cho lễ tân/nhân viên trực ca; Quản lý theo dõi tình hình qua Bảng điều hành và Báo cáo.")]
public class FrontDeskController : ControllerBase
{
    private readonly IFrontDeskService _frontDesk;

    public FrontDeskController(IFrontDeskService frontDesk)
    {
        _frontDesk = frontDesk;
    }

    [HttpGet("overview")]
    [HasPermission(Permissions.XemDatPhong, Permissions.XemLuuTru)]
    public async Task<IActionResult> Overview()
        => Ok(ApiResponse<FrontDeskOverviewDto>.Ok(await _frontDesk.GetOverviewAsync()));

    /// <summary>Booking đến trong ngày (mặc định hôm nay): trạng thái cọc, từng phòng đã sẵn sàng chưa, có thể nhận ngay hay không.</summary>
    [HttpGet("arrivals")]
    [HasPermission(Permissions.XemDatPhong)]
    public async Task<IActionResult> Arrivals([FromQuery] DateOnly? ngay)
        => Ok(ApiResponse<List<ArrivalDto>>.Ok(await _frontDesk.GetArrivalsAsync(ngay)));

    /// <summary>Phòng trả trong ngày (mặc định hôm nay): khách còn ở có ngày trả dự kiến đúng ngày (và quá hạn nếu là hôm nay) + phòng đã trả trong ngày.</summary>
    [HttpGet("departures")]
    [HasPermission(Permissions.XemLuuTru)]
    public async Task<IActionResult> Departures([FromQuery] DateOnly? ngay)
        => Ok(ApiResponse<List<DepartureDto>>.Ok(await _frontDesk.GetDeparturesAsync(ngay)));

    /// <summary>Khách đang lưu trú.</summary>
    [HttpGet("in-house")]
    [HasPermission(Permissions.XemLuuTru)]
    public async Task<IActionResult> InHouse()
        => Ok(ApiResponse<List<DepartureDto>>.Ok(await _frontDesk.GetInHouseAsync()));

    /// <summary>Phòng trống trong khoảng [nhan, tra) (mặc định tối nay), có thể lọc theo loại phòng; không gồm phòng bảo trì và phòng đã có khách/đã được đặt.</summary>
    [HttpGet("vacant-rooms")]
    [HasPermission(Permissions.XemPhong)]
    public async Task<IActionResult> VacantRooms([FromQuery] DateOnly? nhan, [FromQuery] DateOnly? tra, [FromQuery] string? maLoai)
        => Ok(ApiResponse<List<VacantRoomDto>>.Ok(await _frontDesk.GetVacantRoomsAsync(nhan, tra, maLoai)));

    /// <summary>Khách không đến: phiếu đã đánh dấu trong kỳ (mặc định 7 ngày qua) và phiếu quá ngày đón đang chờ xử lý.</summary>
    [HttpGet("no-shows")]
    [HasPermission(Permissions.XemDatPhong)]
    public async Task<IActionResult> NoShows([FromQuery] DateOnly? tuNgay, [FromQuery] DateOnly? denNgay)
        => Ok(ApiResponse<NoShowReportDto>.Ok(await _frontDesk.GetNoShowsAsync(tuNgay, denNgay)));
}
