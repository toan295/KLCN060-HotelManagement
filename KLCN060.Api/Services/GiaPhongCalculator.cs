using KLCN060.Domain;

namespace KLCN060.Api.Services;

/// <summary>Kết quả tính giá cho một khoảng đêm của một loại phòng.</summary>
public record KetQuaGia(decimal DonGiaBinhQuan, decimal TongTien, List<(DateOnly Dem, decimal Gia)> TheoDem);

/// <summary>
/// Tính giá phòng theo đêm: mỗi đêm bằng đơn giá của loại phòng (<see cref="LoaiPhong.DonGia"/>).
/// TỔNG TIỀN = tổng giá từng đêm. Giá từng đêm được chốt vào <see cref="GiaDemDatPhong"/> khi đặt phòng (xem <see cref="TaoGiaChot"/>)
/// nên hóa đơn không bao giờ lệch so với giá đã báo cho khách dù sau đó quản lý đổi đơn giá loại phòng.
/// </summary>
public static class GiaPhongCalculator
{
    /// <summary>Giá cho các đêm [nhan, tra); khoảng rỗng được tính là 1 đêm.</summary>
    public static KetQuaGia Tinh(LoaiPhong loai, DateOnly nhan, DateOnly tra)
    {
        var soDem = Math.Max(1, tra.DayNumber - nhan.DayNumber);
        var theoDem = new List<(DateOnly Dem, decimal Gia)>();
        for (var i = 0; i < soDem; i++)
            theoDem.Add((nhan.AddDays(i), loai.DonGia));
        return new KetQuaGia(loai.DonGia, loai.DonGia * soDem, theoDem);
    }

    /// <summary>Dựng các dòng giá chốt từng đêm của một phòng trong phiếu đặt từ kết quả tính giá.</summary>
    public static List<GiaDemDatPhong> TaoGiaChot(string maPhieuDat, string maPhong, LoaiPhong loai, KetQuaGia kq)
        => kq.TheoDem.Select(d => new GiaDemDatPhong
        {
            MaPhieuDat = maPhieuDat, MaPhong = maPhong, Dem = d.Dem, GiaCoBan = loai.DonGia, Gia = d.Gia
        }).ToList();
}
