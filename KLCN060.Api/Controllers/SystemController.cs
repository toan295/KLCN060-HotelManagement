using KLCN060.Api.Authorization;
using KLCN060.Domain;
using KLCN060.Api.DTOs.Common;
using KLCN060.Api.DTOs.System;
using KLCN060.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

[ApiController]
[Route("api/v1/system/backups")]
[HasPermission(Permissions.QuanTriHeThong)]
public class SystemController : ControllerBase
{
    private readonly IBackupService _backupService;

    public SystemController(IBackupService backupService)
    {
        _backupService = backupService;
    }

    /// <summary>Sao lưu CSDL. Body { "thuMuc": "D:\..." } là tùy chọn; bỏ trống = thư mục mặc định.</summary>
    [HttpPost]
    public async Task<IActionResult> Backup([FromBody] BackupRequest? request = null)
        => StatusCode(StatusCodes.Status201Created, ApiResponse<BackupHistoryDto>.Ok(await _backupService.BackupAsync(request, CurrentUser.From(User))));

    [HttpGet]
    public async Task<IActionResult> GetHistory()
        => Ok(ApiResponse<List<BackupHistoryDto>>.Ok(await _backupService.GetHistoryAsync()));

    /// <summary>Tình trạng sao lưu: lần cuối, số bản, kết quả kiểm tra gần nhất, cảnh báo quá lâu chưa sao lưu.</summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
        => Ok(ApiResponse<BackupStatusDto>.Ok(await _backupService.GetStatusAsync()));

    /// <summary>Kiểm tra toàn vẹn một bản sao lưu trong lịch sử (RESTORE VERIFYONLY) mà không phục hồi.</summary>
    [HttpPost("{maLichSu:int}/verify")]
    public async Task<IActionResult> Verify(int maLichSu)
        => Ok(ApiResponse<BackupVerifyResultDto>.Ok(await _backupService.VerifyAsync(maLichSu, CurrentUser.From(User))));

    /// <summary>Kiểm tra toàn vẹn một file .bak bất kỳ trên máy chạy API.</summary>
    [HttpPost("verify-file")]
    public async Task<IActionResult> VerifyFile([FromBody] RestoreFileRequest request)
        => Ok(ApiResponse<BackupVerifyResultDto>.Ok(await _backupService.VerifyFileAsync(request, CurrentUser.From(User))));

    /// <summary>Cài đặt sao lưu tự động hiện tại (bật / tắt, chu kỳ).</summary>
    [HttpGet("schedule")]
    public async Task<IActionResult> GetSchedule()
        => Ok(ApiResponse<BackupScheduleDto>.Ok(await _backupService.GetScheduleAsync()));

    /// <summary>Bật / tắt sao lưu tự động và đặt chu kỳ (giờ) - có hiệu lực ngay, được ghi vào nhật ký.</summary>
    [HttpPut("schedule")]
    public async Task<IActionResult> SetSchedule([FromBody] BackupScheduleRequest request)
        => Ok(ApiResponse<BackupScheduleDto>.Ok(await _backupService.SetScheduleAsync(request, CurrentUser.From(User))));

    /// <summary>Thư mục sao lưu mặc định hiện tại (dùng để gợi ý trên giao diện).</summary>
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
        => Ok(ApiResponse<BackupSettingsDto>.Ok(await _backupService.GetSettingsAsync()));

    /// <summary>Phục hồi ghi đè toàn bộ CSDL hiện tại bằng bản sao lưu đã chọn (có kiểm tra toàn vẹn trước và chụp bản sao lưu an toàn của CSDL hiện tại).</summary>
    [HttpPost("{maLichSu:int}/restore")]
    public async Task<IActionResult> Restore(int maLichSu, [FromBody] RestoreRequest? request = null)
        => Ok(ApiResponse<BackupHistoryDto>.Ok(await _backupService.RestoreAsync(maLichSu, request ?? new RestoreRequest(), CurrentUser.From(User))));

    /// <summary>Xem trước phục hồi từ một bản trong lịch sử: kiểm tra file, đúng CSDL, dữ liệu sẽ mất những gì. Không thay đổi gì.</summary>
    [HttpGet("{maLichSu:int}/restore-preview")]
    public async Task<IActionResult> RestorePreview(int maLichSu)
        => Ok(ApiResponse<RestorePreviewDto>.Ok(await _backupService.PreviewRestoreAsync(maLichSu, null, CurrentUser.From(User))));

    [HttpPost("restore-file-preview")]
    public async Task<IActionResult> RestoreFilePreview([FromBody] RestoreFileRequest request)
        => Ok(ApiResponse<RestorePreviewDto>.Ok(await _backupService.PreviewRestoreAsync(null, request, CurrentUser.From(User))));

    /// <summary>Phục hồi từ một file .bak bất kỳ trên máy chạy API (không cần nằm trong lịch sử sao lưu).</summary>
    [HttpPost("restore-file")]
    public async Task<IActionResult> RestoreFromFile([FromBody] RestoreFileRequest request)
        => Ok(ApiResponse<BackupHistoryDto>.Ok(await _backupService.RestoreFromFileAsync(request, CurrentUser.From(User))));
}
