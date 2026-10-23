using KLCN060.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

/// <summary>
/// Khóa đúng 1 dòng phiếu nhận tới hết transaction. Mọi thao tác ghi vào folio (dịch vụ, gói, hủy dòng, đổi phòng, gia hạn, tính phí minibar)
/// và trả phòng đều khóa cùng khóa này nên chạy tuần tự, dòng dịch vụ không lọt khỏi hóa đơn.
/// </summary>
/// <remarks>
/// Hai điểm dễ sai (đã đo bằng sys.dm_tran_locks):
/// 1) Cột MaPhieuNhan là varchar còn tham số của FromSql là nvarchar → SQL Server đổi kiểu cột, quét cả bảng và khóa cả bảng.
///    Ép tham số về varchar(15) để dò đúng khóa chính.
/// 2) Dùng EXISTS/AnyAsync khiến bộ tối ưu chọn chỉ mục phụ IX_PhieuNhanPhongs_MaKhach, trong khi trả phòng khóa khóa chính → hai bên không loại trừ nhau.
///    Chỉ định rõ chỉ mục khóa chính và đọc dòng ra thay vì EXISTS.
/// </remarks>
internal static class PhieuNhanLock
{
    private const int DoDaiMaToiDa = 15;

    /// <summary>Khóa dòng phiếu nhận; trả false nếu không có phiếu này.</summary>
    public static async Task<bool> KhoaAsync(KLCN060DbContext context, string maPhieuNhan)
    {
        if (string.IsNullOrEmpty(maPhieuNhan) || maPhieuNhan.Length > DoDaiMaToiDa)
            return false;
        var ma = await context.Database
            .SqlQuery<string>($"SELECT MaPhieuNhan AS Value FROM PhieuNhanPhongs WITH (UPDLOCK, HOLDLOCK, INDEX(PK_PhieuNhanPhongs)) WHERE MaPhieuNhan = CAST({maPhieuNhan} AS varchar(15))")
            .ToListAsync();
        return ma.Count > 0;
    }
}
