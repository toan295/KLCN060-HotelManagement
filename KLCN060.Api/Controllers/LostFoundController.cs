using KLCN060.Api.Authorization;
using KLCN060.Api.DTOs.Common;
using KLCN060.Api.DTOs.Housekeeping;
using KLCN060.Api.Services;
using KLCN060.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

/// <summary>Đồ khách bỏ quên (Lost &amp; Found): buồng phòng ghi nhận và lưu kho, lễ tân trả khách, quản lý thanh lý.</summary>
[ApiController]
[Route("api/v1/lost-found")]
[Authorize]
public class LostFoundController : ControllerBase
{
    private readonly ILostFoundService _lostFound;

    public LostFoundController(ILostFoundService lostFound)
    {
        _lostFound = lostFound;
    }

    /// <summary>Danh sách đồ bỏ quên; lọc theo trạng thái (hoặc DANG_CON = chưa trả/thanh lý), phòng, từ khóa (mô tả, vị trí, người nhặt), khoảng ngày phát hiện.</summary>
    [HttpGet]
    [HasPermission(Permissions.GhiNhanBuongPhong, Permissions.ThaoTacLeTan)]
    public async Task<IActionResult> GetAll([FromQuery] string? trangThai, [FromQuery] string? maPhong, [FromQuery] string? tuKhoa, [FromQuery] DateOnly? tuNgay, [FromQuery] DateOnly? denNgay)
        => Ok(ApiResponse<List<LostFoundDto>>.Ok(await _lostFound.GetAllAsync(trangThai, maPhong, tuKhoa, tuNgay, denNgay, CurrentUser.From(User).TenDN)));

    [HttpGet("{maDo:int}")]
    [HasPermission(Permissions.GhiNhanBuongPhong, Permissions.ThaoTacLeTan)]
    public async Task<IActionResult> Get(int maDo)
        => Ok(ApiResponse<LostFoundDto>.Ok(await _lostFound.GetAsync(maDo, CurrentUser.From(User).TenDN)));

    [HttpPost]
    [HasPermission(Permissions.GhiNhanBuongPhong)]
    public async Task<IActionResult> Create([FromBody] CreateLostFoundRequest request)
        => StatusCode(StatusCodes.Status201Created, ApiResponse<LostFoundDto>.Ok(await _lostFound.CreateAsync(request, CurrentUser.From(User).TenDN)));

    [HttpPut("{maDo:int}")]
    [HasPermission(Permissions.GhiNhanBuongPhong)]
    public async Task<IActionResult> Update(int maDo, [FromBody] UpdateLostFoundRequest request)
        => Ok(ApiResponse<LostFoundDto>.Ok(await _lostFound.UpdateAsync(maDo, request, CurrentUser.From(User).TenDN)));

    /// <summary>Đưa vào kho / đổi vị trí lưu giữ → trạng thái Đang lưu giữ.</summary>
    [HttpPost("{maDo:int}/store")]
    [HasPermission(Permissions.GhiNhanBuongPhong)]
    public async Task<IActionResult> Store(int maDo, [FromBody] StoreLostFoundRequest request)
        => Ok(ApiResponse<LostFoundDto>.Ok(await _lostFound.StoreAsync(maDo, request, CurrentUser.From(User).TenDN)));

    /// <summary>Trả đồ cho khách (quản lý; lễ tân chỉ xem thông tin): bắt buộc người nhận và giấy tờ đối chiếu.</summary>
    [HttpPost("{maDo:int}/return")]
    [HasPermission(Permissions.DuyetNghiepVu)]
    public async Task<IActionResult> Return(int maDo, [FromBody] ReturnLostFoundRequest request)
        => Ok(ApiResponse<LostFoundDto>.Ok(await _lostFound.ReturnAsync(maDo, request, CurrentUser.From(User).TenDN)));

    /// <summary>Thanh lý đồ không ai nhận (quản lý; sau tối thiểu 30 ngày lưu giữ, có lý do).</summary>
    [HttpPost("{maDo:int}/dispose")]
    [HasPermission(Permissions.DuyetNghiepVu)]
    public async Task<IActionResult> Dispose(int maDo, [FromBody] DisposeLostFoundRequest request)
        => Ok(ApiResponse<LostFoundDto>.Ok(await _lostFound.DisposeAsync(maDo, request, CurrentUser.From(User).TenDN)));

    /// <summary>Thêm ảnh (multipart/form-data, field "file"; JPG/PNG/WEBP tối đa 5 MB; tối đa 3 ảnh mỗi món).</summary>
    [HttpPost("{maDo:int}/images")]
    [HasPermission(Permissions.GhiNhanBuongPhong)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> AddImage(int maDo, IFormFile? file)
        => StatusCode(StatusCodes.Status201Created, ApiResponse<LostFoundDto>.Ok(await _lostFound.AddImageAsync(maDo, file, CurrentUser.From(User).TenDN)));

    /// <summary>Tải nội dung ảnh (cần đăng nhập - ảnh đồ cá nhân của khách không công khai).</summary>
    [HttpGet("{maDo:int}/images/{maAnh:int}")]
    [HasPermission(Permissions.GhiNhanBuongPhong, Permissions.ThaoTacLeTan)]
    public async Task<IActionResult> GetImage(int maDo, int maAnh)
    {
        var (duongDan, contentType) = await _lostFound.GetImageAsync(maDo, maAnh);
        return PhysicalFile(duongDan, contentType);
    }

    [HttpDelete("{maDo:int}/images/{maAnh:int}")]
    [HasPermission(Permissions.GhiNhanBuongPhong)]
    public async Task<IActionResult> DeleteImage(int maDo, int maAnh)
        => Ok(ApiResponse<LostFoundDto>.Ok(await _lostFound.DeleteImageAsync(maDo, maAnh, CurrentUser.From(User).TenDN)));
}
