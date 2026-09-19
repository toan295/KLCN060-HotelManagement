using KLCN060.Api.DTOs.System;

namespace KLCN060.Api.Services;

public interface IBackupService
{
    Task<BackupHistoryDto> BackupAsync(CurrentUser user);
    Task<List<BackupHistoryDto>> GetHistoryAsync();
    Task<BackupHistoryDto> RestoreAsync(int maLichSu, CurrentUser user);
}
