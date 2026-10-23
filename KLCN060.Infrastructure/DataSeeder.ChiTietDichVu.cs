using KLCN060.Domain;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Infrastructure;

// Dữ liệu mẫu cho bảng ChiTietDichVus: mỗi dịch vụ có vài mục thông tin (thời gian phục vụ, bao gồm, lưu ý, đặt trước...).
// Chạy mỗi lần khởi động, chỉ thêm khi bảng còn trống (CSDL cũ được nâng cấp bằng migration cũng nhận được dữ liệu này).
public static partial class DataSeeder
{
    private static async Task SeedChiTietDichVuAsync(KLCN060DbContext context)
    {
        if (await context.ChiTietDichVus.AnyAsync()) return;

        var maDichVuCo = await context.DichVus.Select(x => x.MaDV).ToListAsync();
        var thuTu = 0;
        var soThuTuMa = 0;
        var dsMoi = new List<ChiTietDichVu>();

        void Them(string maDV, params (string TieuDe, string NoiDung)[] cacMuc)
        {
            if (!maDichVuCo.Contains(maDV)) return;
            thuTu = 0;
            foreach (var (tieuDe, noiDung) in cacMuc)
            {
                dsMoi.Add(new ChiTietDichVu
                {
                    MaCTDV = $"CT{++soThuTuMa:000}",
                    MaDV = maDV,
                    TieuDe = tieuDe,
                    NoiDung = noiDung,
                    ThuTu = ++thuTu,
                    ConHienThi = true
                });
            }
        }

        Them("DV01",
            ("Thời gian phục vụ", "Giao đồ trước 09:00 sẽ được trả trong ngày; giao sau 09:00 sẽ được trả vào ngày hôm sau."),
            ("Bao gồm", "Giặt, sấy khô, ủi phẳng và gấp gọn quần áo thông thường, đóng túi riêng cho từng phòng."),
            ("Cách tính", "Tính theo kg, lễ tân cân đồ cùng khách khi tiếp nhận và ghi nhận vào hóa đơn."),
            ("Lưu ý", "Không nhận đồ len, đồ da và đồ giặt khô cao cấp. Khách sạn không chịu trách nhiệm với quần áo dễ phai màu."));

        Them("DV02",
            ("Thời gian phục vụ", "Nhận và trả xe từ 07:00 đến 20:00 hằng ngày tại quầy lễ tân."),
            ("Bao gồm", "Xe máy đời mới đã kiểm tra kỹ thuật, kèm 2 mũ bảo hiểm và bản đồ tham quan Đà Lạt."),
            ("Giấy tờ", "Xuất trình CCCD hoặc hộ chiếu và giấy phép lái xe còn hiệu lực khi nhận xe."),
            ("Áp dụng", "Giá ngày thường áp dụng cho các ngày không trùng dịp lễ, Tết."));

        Them("DV03",
            ("Thời gian phục vụ", "Nhận và trả xe từ 07:00 đến 20:00 hằng ngày tại quầy lễ tân."),
            ("Bao gồm", "Xe máy đời mới đã kiểm tra kỹ thuật, kèm 2 mũ bảo hiểm và bản đồ tham quan Đà Lạt."),
            ("Áp dụng", "Giá ngày lễ áp dụng cho các ngày nghỉ lễ, Tết theo quy định của nhà nước."),
            ("Đặt trước", "Dịp lễ lượng xe có hạn, quý khách vui lòng báo lễ tân đặt trước ít nhất 1 ngày."));

        Them("DV04",
            ("Thời gian phục vụ", "Phục vụ tận phòng theo yêu cầu, gọi lễ tân để được hỗ trợ."),
            ("Chủng loại", "Nhiều loại nước ngọt có ga phổ biến, chủng loại tùy theo tồn kho tại thời điểm gọi."),
            ("Cách tính", "Tính theo lon thực tế sử dụng, lễ tân ghi nhận và cộng vào hóa đơn khi trả phòng."));

        Them("DV05",
            ("Thời gian phục vụ", "Phục vụ tận phòng theo yêu cầu, gọi lễ tân để được hỗ trợ."),
            ("Chủng loại", "Một số nhãn bia phổ biến, chủng loại tùy theo tồn kho tại thời điểm gọi."),
            ("Cách tính", "Tính theo lon thực tế sử dụng, lễ tân ghi nhận và cộng vào hóa đơn khi trả phòng."),
            ("Lưu ý", "Không phục vụ khách dưới 18 tuổi. Vui lòng uống có trách nhiệm."));

        Them("DV06",
            ("Thời gian phục vụ", "06:30 đến 09:30 hằng ngày."),
            ("Địa điểm", "Khu ăn sáng tại tầng trệt của khách sạn."),
            ("Thực đơn", "Các món Việt và món Âu: phở, cháo, bánh mì, trứng chế biến theo yêu cầu, trái cây theo mùa, cà phê và trà."),
            ("Đăng ký", "Đăng ký với lễ tân để được cộng vào hóa đơn. Một số chương trình khuyến mãi có tặng kèm bữa sáng."));

        Them("DV07",
            ("Tuyến phục vụ", "Sân bay Liên Khương - khách sạn (quãng đường khoảng 30 km, mất chừng 45 phút), đón hoặc tiễn theo từng lượt."),
            ("Bao gồm", "Xe 4 chỗ có tài xế, phí cầu đường và hỗ trợ đưa hành lý lên xe."),
            ("Đặt trước", "Đăng ký trước ít nhất 1 ngày, cung cấp số hiệu chuyến bay và giờ đến để lễ tân sắp xếp xe."),
            ("Liên hệ", "Gọi hotline 1900 0001 hoặc báo trực tiếp với lễ tân."));

        context.ChiTietDichVus.AddRange(dsMoi);
        await context.SaveChangesAsync();
    }
}
