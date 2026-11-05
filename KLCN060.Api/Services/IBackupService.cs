using KLCN060.Api.DTOs.System;

namespace KLCN060.Api.Services;

public interface IBackupService
{
    Task<BackupHistoryDto> BackupAsync(BackupRequest? request, CurrentUser user);
    Task<List<BackupHistoryDto>> GetHistoryAsync();
    Task<BackupHistoryDto> RestoreAsync(int maLichSu, RestoreRequest request, CurrentUser user);
    /// <summary>Xem trước phục hồi (không thay đổi gì): kiểm tra file và cho biết dữ liệu sẽ mất.</summary>
    Task<RestorePreviewDto> PreviewRestoreAsync(int? maLichSu, RestoreFileRequest? file, CurrentUser user);
    Task<BackupHistoryDto> RestoreFromFileAsync(RestoreFileRequest request, CurrentUser user);
    Task<BackupSettingsDto> GetSettingsAsync();

    /// <summary>Kiểm tra toàn vẹn một bản sao lưu trong lịch sử (không phục hồi).</summary>
    Task<BackupVerifyResultDto> VerifyAsync(int maLichSu, CurrentUser user);

    /// <summary>Kiểm tra toàn vẹn một file .bak bất kỳ trên máy chạy API (không phục hồi).</summary>
    Task<BackupVerifyResultDto> VerifyFileAsync(RestoreFileRequest request, CurrentUser user);
}
