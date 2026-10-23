namespace KLCN060.Desktop.Services;

// Các lời gọi API xử lý thông báo tiền về bất thường của Kế toán (đối soát chuyển khoản, D18).
public sealed partial class ApiClient
{
    public Task<List<ThongBaoNganHangDto>> GetThongBaoNganHangAsync(string? trangThaiXuLy)
        => GetAsync<List<ThongBaoNganHangDto>>($"api/v1/payments/bank-notifications?pageSize=100{(string.IsNullOrEmpty(trangThaiXuLy) ? "" : "&trangThaiXuLy=" + trangThaiXuLy)}");
    public Task<ThongBaoNganHangDto> XuLyThongBaoNganHangAsync(int maThongBao, string huong, string ghiChu)
        => PostAsync<object, ThongBaoNganHangDto>($"api/v1/reconciliation/bank-notifications/{maThongBao}/resolve", new { huong, ghiChu });
}
