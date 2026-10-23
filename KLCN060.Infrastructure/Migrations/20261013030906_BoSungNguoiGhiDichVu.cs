using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BoSungNguoiGhiDichVu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Mọi dòng dịch vụ phải truy được "ai ghi". Dòng cũ chưa có người ghi được bổ sung theo thứ tự tin cậy:
            // 1) Dòng minibar do lễ tân bấm "Tính phí": lấy người xử lý của khoản kiểm phòng tương ứng (và phòng của phiếu kiểm).
            migrationBuilder.Sql(@"
UPDATE c SET c.NguoiGhi = k.NguoiXuLy, c.MaPhong = COALESCE(c.MaPhong, pk.MaPhong)
FROM ChiTietSuDungDVs c
JOIN ChiTietKiemPhongs k ON k.MaChiTietDV = c.MaChiTietDV
JOIN PhieuKiemPhongs pk ON pk.MaPhieuKiem = k.MaPhieuKiem
WHERE c.NguoiGhi IS NULL AND k.NguoiXuLy IS NOT NULL;");

            // 2) Dòng ghi bằng chức năng Dịch vụ: lấy tài khoản trong nhật ký thao tác (GHI_DICH_VU / GHI_GOI_DICH_VU) cùng phiếu nhận, cách giờ ghi không quá 10 giây.
            migrationBuilder.Sql(@"
UPDATE c SET c.NguoiGhi = (
    SELECT TOP 1 n.MaTaiKhoan FROM NhatKyThaoTacs n
    WHERE n.HanhDong IN ('GHI_DICH_VU', 'GHI_GOI_DICH_VU') AND n.KetQua = 'THANH_CONG' AND n.DoiTuongTacDong = c.MaPhieuNhan
      AND ABS(DATEDIFF(SECOND, n.ThoiGian, c.NgaySuDung)) <= 10
    ORDER BY ABS(DATEDIFF(SECOND, n.ThoiGian, c.NgaySuDung)))
FROM ChiTietSuDungDVs c
WHERE c.NguoiGhi IS NULL;");

            // 3) Còn lại (dữ liệu nhập trước khi hệ thống lưu người ghi, không có nhật ký): ghi rõ là dữ liệu cũ của hệ thống thay vì để trống hay đoán tên người.
            migrationBuilder.Sql(@"UPDATE ChiTietSuDungDVs SET NguoiGhi = 'he_thong' WHERE NguoiGhi IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE ChiTietSuDungDVs SET NguoiGhi = NULL WHERE NguoiGhi = 'he_thong';");
        }
    }
}
