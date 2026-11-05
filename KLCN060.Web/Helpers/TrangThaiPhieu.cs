namespace KLCN060.Web.Helpers;

/// <summary>Cách hiển thị trạng thái phiếu đặt phòng cho khách: tên, tên dài (giải thích) và lớp CSS màu (mỗi trạng thái một màu riêng, xem site.css).</summary>
public static class TrangThaiPhieu
{
    public static string Nhan(string trangThai) => trangThai switch
    {
        "CHO_XAC_NHAN" => "Chờ đặt cọc",
        "DA_XAC_NHAN" => "Đã xác nhận",
        "DA_NHAN_PHONG" => "Đã nhận phòng",
        "DA_TRA_PHONG" => "Đã trả phòng",
        "DA_HUY" => "Đã hủy",
        "KHONG_DEN" => "Không đến",
        _ => trangThai
    };

    public static string NhanDai(string trangThai) => trangThai switch
    {
        "CHO_XAC_NHAN" => "Chờ đặt cọc (chưa giữ phòng)",
        "DA_XAC_NHAN" => "Đã xác nhận, phòng đã được giữ",
        _ => Nhan(trangThai)
    };

    public static string Lop(string trangThai) => trangThai switch
    {
        "CHO_XAC_NHAN" => "st-cho",
        "DA_XAC_NHAN" => "st-xacnhan",
        "DA_NHAN_PHONG" => "st-dango",
        "DA_TRA_PHONG" => "st-xong",
        "DA_HUY" => "st-huy",
        "KHONG_DEN" => "st-khongden",
        _ => "st-xong"
    };

    /// <summary>Thứ tự để lọc/sắp xếp phía client.</summary>
    public static readonly (string Ma, string Ten)[] TatCa =
    {
        ("CHO_XAC_NHAN", "Chờ đặt cọc"), ("DA_XAC_NHAN", "Đã xác nhận"), ("DA_NHAN_PHONG", "Đã nhận phòng"),
        ("DA_TRA_PHONG", "Đã trả phòng"), ("DA_HUY", "Đã hủy"), ("KHONG_DEN", "Không đến")
    };
}
