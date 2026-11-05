using System.Net.Http;

namespace KLCN060.Desktop.Services;

// Màn Báo cáo (D17): từ điển chỉ số, đối chiếu/kiểm tra nhất quán, vấn đề cần chú ý, truy vết số liệu.
public sealed partial class ApiClient
{
}

public sealed partial class ApiClient
{
    /// <summary>Tải một tệp (báo cáo xuất) từ API: tự thử làm mới token một lần khi hết hạn.</summary>
    public async Task<(byte[] NoiDung, string TenFile)> TaiTepAsync(string duongDan, string tenMacDinh)
    {
        using var request = Create(HttpMethod.Get, duongDan);
        using var response = await _httpClient.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && await ThuLamMoiTokenAsync())
        {
            using var requestMoi = Create(HttpMethod.Get, duongDan);
            using var responseMoi = await _httpClient.SendAsync(requestMoi);
            if (!responseMoi.IsSuccessStatusCode) throw await ErrorAsync(responseMoi);
            return (await responseMoi.Content.ReadAsByteArrayAsync(), responseMoi.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? tenMacDinh);
        }
        if (!response.IsSuccessStatusCode) throw await ErrorAsync(response);
        return (await response.Content.ReadAsByteArrayAsync(), response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? tenMacDinh);
    }

    /// <summary>Xuất báo cáo quản trị của kỳ ra Excel (xlsx) hoặc PDF.</summary>
    public Task<(byte[] NoiDung, string TenFile)> XuatBaoCaoQuanTriAsync(DateOnly tu, DateOnly den, string dinhDang)
        => TaiTepAsync($"api/v1/reports/export?from={tu:yyyy-MM-dd}&to={den:yyyy-MM-dd}&dinhDang={Uri.EscapeDataString(dinhDang)}", $"KLCN060Hotel_BaoCaoQuanTri-{tu:dd.MM.yyyy}-{den:dd.MM.yyyy}_{DateTime.Now:dd.MM.yyyy_HHmm}.{dinhDang}");

    public Task<KetQuaTaiKhoanNhanVienDto> XoaNhanVienAsync(string maNV, string lyDo)
        => PostAsync<YeuCauLyDoTaiKhoan, KetQuaTaiKhoanNhanVienDto>($"api/v1/employees/{Uri.EscapeDataString(maNV)}/delete", new YeuCauLyDoTaiKhoan(lyDo));

}

