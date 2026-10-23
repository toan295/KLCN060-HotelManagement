namespace KLCN060.Api.Services.Finance;

/// <summary>Cấu hình tài chính (mục "Finance" trong appsettings; bỏ trống thì dùng mặc định).</summary>
public class FinanceOptions
{
    public const string SectionName = "Finance";

    /// <summary>Hóa đơn còn nợ mà khách đã trả phòng từ số ngày này trở lên thì bị coi là quá hạn thanh toán.</summary>
    public int HoaDonQuaHanNgay { get; set; } = 1;

    /// <summary>Thông báo tiền về bất thường chưa xử lý quá số ngày này thì bị coi là quá hạn xử lý (đối soát chuyển khoản).</summary>
    public int ThongBaoQuaHanNgay { get; set; } = 2;
}
