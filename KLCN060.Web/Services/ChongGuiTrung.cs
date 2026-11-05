using System.Collections.Concurrent;
using KLCN060.Web.Models.Common;

namespace KLCN060.Web.Services;

/// <summary>
/// Chống gửi trùng một yêu cầu (bấm đúp, gửi lại form): cùng một phiên + cùng nội dung đang được xử lý thì request đến sau
/// dùng chung kết quả của lần gửi đầu thay vì chạy thêm lần nữa (tạo thêm phiếu giữ thêm phòng).
/// Đây là lớp 1 (nhanh, nằm ở Web). Khóa chỉ được xóa khi tác vụ gốc chạy xong (thành công hoặc lỗi), KHÔNG xóa theo thời gian nên API chậm bao lâu
/// cũng không có request thứ hai chạy song song. Lớp 2 nằm ở API (BookingService.CreateAsync trả lại phiếu giống hệt vừa tạo trong vài giây),
/// chặn cả trường hợp Web chạy nhiều instance, khởi động lại hoặc request đến sau khi lần đầu đã xong.
/// </summary>
public static class ChongGuiTrung
{
    // Chỉ để dọn rác nếu một tác vụ treo bất thường; không phải cơ chế chặn chính.
    private static readonly TimeSpan ThoiGianTreoToiDa = TimeSpan.FromMinutes(2);

    private sealed class Muc<TRes>
    {
        public DateTime Luc { get; init; }
        public Lazy<Task<ApiResult<TRes>>> Viec { get; set; } = null!;
    }

    private static class Kho<TRes>
    {
        public static readonly ConcurrentDictionary<string, Muc<TRes>> Dang = new();
    }

    public static async Task<ApiResult<TRes>> ChayAsync<TRes>(string khoa, Func<Task<ApiResult<TRes>>> thucHien)
    {
        var kho = Kho<TRes>.Dang;
        var now = DateTime.UtcNow;
        foreach (var treo in kho.Where(x => now - x.Value.Luc > ThoiGianTreoToiDa).Select(x => x.Key).ToList())
            kho.TryRemove(treo, out _);

        // Lazy (ExecutionAndPublication) bảo đảm thucHien chỉ chạy đúng một lần dù nhiều request cùng tới.
        var moi = new Muc<TRes> { Luc = now };
        moi.Viec = new Lazy<Task<ApiResult<TRes>>>(() => ChayRoiDonAsync(kho, khoa, moi, thucHien));
        var muc = kho.GetOrAdd(khoa, moi);
        return await muc.Viec.Value;
    }

    // Xóa khóa đúng lúc tác vụ gốc kết thúc (kể cả lỗi) và chỉ xóa đúng mục của mình, không xóa nhầm mục của lần gửi sau.
    private static async Task<ApiResult<TRes>> ChayRoiDonAsync<TRes>(ConcurrentDictionary<string, Muc<TRes>> kho, string khoa, Muc<TRes> muc, Func<Task<ApiResult<TRes>>> thucHien)
    {
        try
        {
            return await thucHien();
        }
        finally
        {
            kho.TryRemove(new KeyValuePair<string, Muc<TRes>>(khoa, muc));
        }
    }
}
