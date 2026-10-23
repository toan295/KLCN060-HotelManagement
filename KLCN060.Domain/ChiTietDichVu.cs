namespace KLCN060.Domain;

/// <summary>
/// Một mục thông tin chi tiết của dịch vụ (thời gian phục vụ, bao gồm những gì, lưu ý, cách đặt trước...).
/// Thiết kế dạng "tiêu đề - nội dung" để thêm loại thông tin mới chỉ là thêm dòng dữ liệu, không phải sửa bảng.
/// Không dính tới tính tiền: giá vẫn nằm ở DichVu.GiaDV, lượt dùng ở ChiTietSuDungDV.
/// </summary>
public class ChiTietDichVu
{
    public string MaCTDV { get; set; } = null!;
    public string MaDV { get; set; } = null!;
    public string TieuDe { get; set; } = null!;
    public string NoiDung { get; set; } = null!;
    public int ThuTu { get; set; }
    public bool ConHienThi { get; set; } = true;

    public DichVu DichVu { get; set; } = null!;
}
