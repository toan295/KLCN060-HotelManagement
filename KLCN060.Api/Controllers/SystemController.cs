using KLCN060.Api.DTOs.Common;
using KLCN060.Api.DTOs.System;
using KLCN060.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

[ApiController]
[Route("api/v1/system/backups")]
[Authorize(Roles = "QUAN_LY")]
public class SystemController : ControllerBase
{
    private readonly IBackupService _backupService;

    public SystemController(IBackupService backupService)
    {
        _backupService = backupService;
    }

    [HttpPost]
    public async Task<IActionResult> Backup()
        => StatusCode(StatusCodes.Status201Created, ApiResponse<BackupHistoryDto>.Ok(await _backupService.BackupAsync(CurrentUser.From(User))));

    [HttpGet]
    public async Task<IActionResult> GetHistory()
        => Ok(ApiResponse<List<BackupHistoryDto>>.Ok(await _backupService.GetHistoryAsync()));

    /// <summary>Phục hồi ghi đè toàn bộ CSDL hiện tại bằng bản sao lưu đã chọn.</summary>
    [HttpPost("{maLichSu:int}/restore")]
    public async Task<IActionResult> Restore(int maLichSu)
        => Ok(ApiResponse<BackupHistoryDto>.Ok(await _backupService.RestoreAsync(maLichSu, CurrentUser.From(User))));
}
