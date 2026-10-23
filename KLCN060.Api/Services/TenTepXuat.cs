namespace KLCN060.Api.Services;

/// <summary>
/// Quy ước tên mọi tệp xuất ra từ hệ thống: <c>KLCN060Hotel_{Loại}_{dd.MM.yyyy}_{HHmm}.{đuôi}</c>, ví dụ <c>KLCN060Hotel_NhatKyHoatDong_11.10.2026_2105.xlsx</c>.
/// Mã đối tượng / kỳ báo cáo (nếu có) được nối vào phần Loại bằng dấu "-", ví dụ <c>KLCN060Hotel_BaoCaoQuanTri-01.10.2026-31.10.2026_11.10.2026_2105.xlsx</c>.
/// </summary>
public static class TenTepXuat
{
    public static string Tao(string loai, string duoi, string? maHoacKy = null, DateTime? luc = null)
        => $"KLCN060Hotel_{loai}{(string.IsNullOrWhiteSpace(maHoacKy) ? "" : "-" + maHoacKy)}_{(luc ?? DateTime.Now):dd.MM.yyyy_HHmm}.{duoi.TrimStart('.')}";
}
