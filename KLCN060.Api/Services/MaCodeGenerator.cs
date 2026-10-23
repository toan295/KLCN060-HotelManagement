using Microsoft.EntityFrameworkCore;

namespace KLCN060.Api.Services;

/// <summary>Sinh mã tuần tự dạng {Prefix}{Số} (ví dụ LP01, DV01, KM01, CSVC012) dựa trên các mã đã tồn tại.</summary>
public static class MaCodeGenerator
{
    public static string GenerateNext(IEnumerable<string> existingCodes, string prefix, int padWidth)
    {
        var soLon = existingCodes
            .Where(ma => ma.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(ma => int.TryParse(ma[prefix.Length..], out var so) ? so : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{(soLon + 1).ToString().PadLeft(padWidth, '0')}";
    }

    /// <summary>
    /// [Khối 8 - P1-4] Đọc mã hiện có + sinh mã kế tiếp AN TOÀN khi nhiều request chạy đồng thời. Trước đây mỗi service
    /// đọc "mã lớn nhất" rồi +1 mà không khóa gì: 2 request cùng lúc đọc cùng 1 giá trị → sinh trùng mã → request sau vỡ
    /// khóa chính → 409 XUNG_DOT_DU_LIEU dù dữ liệu hợp lệ (Khối 8 đo được 7/8 walk-in hợp lệ bị từ chối).
    /// Phải gọi TRONG transaction và SAU các khóa dòng nghiệp vụ của luồng (thứ tự khóa nhất quán → tránh deadlock).
    /// </summary>
    public static async Task<string> GenerateNextAsync(DbContext context, IQueryable<string> maHienCo, string prefix, int padWidth)
    {
        await KhoaTienToAsync(context, prefix);
        return GenerateNext(await maHienCo.ToListAsync(), prefix, padWidth);
    }

    /// <summary>
    /// Giữ application lock của SQL Server theo tiền tố mã (VD "CI20260925-", "KH") tới hết transaction hiện tại: chỉ các
    /// request sinh CÙNG loại mã mới phải chờ nhau, các luồng khác không bị ảnh hưởng. Dùng app lock thay vì
    /// UPDLOCK/HOLDLOCK trên bảng vì lúc sinh mã chưa có dòng nào để khóa (khóa khoảng khóa chính phụ thuộc chỉ mục).
    /// </summary>
    public static async Task KhoaTienToAsync(DbContext context, string prefix)
    {
        // EF Core InMemory (Component test Khối 6) không có khóa — bỏ qua, giữ nguyên hành vi cũ.
        if (!context.Database.IsRelational())
            return;
        if (context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Sinh mã tuần tự an toàn phải chạy trong transaction (khóa được giữ tới khi commit/rollback).");

        var resource = $"KLCN060:MaCode:{prefix}";
        await context.Database.ExecuteSqlInterpolatedAsync($@"
DECLARE @ketQua int;
EXEC @ketQua = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
IF @ketQua < 0 THROW 51000, N'Hết thời gian chờ khóa sinh mã, vui lòng thử lại.', 1;");
    }
}
