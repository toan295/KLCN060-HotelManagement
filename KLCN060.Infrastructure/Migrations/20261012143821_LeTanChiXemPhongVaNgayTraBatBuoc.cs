using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLCN060.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LeTanChiXemPhongVaNgayTraBatBuoc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Khách lưu trú bắt buộc có ngày trả dự kiến cụ thể: lượt đang ở (khách vãng lai cũ) chưa khai thì ngầm định ở 1 đêm kể từ ngày nhận
            //    (lễ tân có thể gia hạn / trả sớm ở màn hình Đổi phòng như bình thường).
            migrationBuilder.Sql(@"
UPDATE ChiTietPhieuNhans
SET NgayTraDuKien = DATEADD(DAY, 1, NgayNhan)
WHERE NgayTraDuKien IS NULL AND TrangThai = 'DANG_O';");

            // 2) Lễ tân chỉ XEM sơ đồ phòng và đồ bỏ quên, không dọn phòng / báo sự cố / kiểm minibar trong phòng / khóa mở phòng:
            //    gỡ các quyền DOI_TINH_TRANG_PHONG, KHOA_MO_PHONG, GHI_NHAN_BUONG_PHONG khỏi vai trò LE_TAN (quyền nghiệp vụ quầy giữ nguyên).
            migrationBuilder.Sql(@"
DELETE vq
FROM VaiTro_Quyen vq
JOIN VaiTros v ON v.MaVaiTro = vq.MaVaiTro
JOIN Quyens q ON q.MaQuyen = vq.MaQuyen
WHERE v.TenVaiTro = 'LE_TAN' AND q.TenQuyen IN ('DOI_TINH_TRANG_PHONG', 'KHOA_MO_PHONG', 'GHI_NHAN_BUONG_PHONG');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Khôi phục quyền mặc định cũ của lễ tân (không khôi phục ngày trả đã bổ sung vì không phân biệt được với dữ liệu thật).
            migrationBuilder.Sql(@"
INSERT INTO VaiTro_Quyen (MaVaiTro, MaQuyen)
SELECT v.MaVaiTro, q.MaQuyen
FROM VaiTros v CROSS JOIN Quyens q
WHERE v.TenVaiTro = 'LE_TAN' AND q.TenQuyen IN ('DOI_TINH_TRANG_PHONG', 'KHOA_MO_PHONG', 'GHI_NHAN_BUONG_PHONG')
  AND NOT EXISTS (SELECT 1 FROM VaiTro_Quyen x WHERE x.MaVaiTro = v.MaVaiTro AND x.MaQuyen = q.MaQuyen);");
        }
    }
}
