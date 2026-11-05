namespace KLCN060.Api.Services.Finance;

/// <summary>
/// Quy tắc duy nhất cho "công nợ quá hạn", dùng chung cho tab Hóa đơn, cảnh báo Điều hành và các báo cáo của Quản lý:
/// hóa đơn còn nợ, khách đã rời đi (không còn ở) và đã qua <see cref="FinanceOptions.HoaDonQuaHanNgay"/> ngày kể từ ngày trả phòng cuối cùng.
/// Khách còn đang ở thì chưa đến hạn thanh toán nên không bao giờ bị coi là quá hạn.
/// </summary>
public static class CongNoQuaHan
{
    /// <summary>Quá hạn từ số ngày này trở lên thì mức độ cảnh báo là nghiêm trọng.</summary>
    public const int NghiemTrongSauNgay = 30;

    public static (bool QuaHan, int SoNgay) Tinh(decimal tongTien, decimal daThu, bool dangO, DateOnly? ngayTraCuoi, DateOnly homNay, int soNgayHan)
    {
        if (tongTien <= daThu || dangO || ngayTraCuoi is not { } tra) return (false, 0);
        var soNgay = homNay.DayNumber - tra.DayNumber;
        return soNgay >= Math.Max(soNgayHan, 0) ? (true, soNgay) : (false, 0);
    }
}
