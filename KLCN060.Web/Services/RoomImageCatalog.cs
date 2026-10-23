namespace KLCN060.Web.Services;

/// <summary>Ảnh phòng do quản lý tải lên qua Desktop (lưu ở KLCN060.Api). Website chỉ cần biết vị trí nào đã có ảnh để hiển thị.</summary>
public interface IRoomImageCatalog
{
    /// <summary>Trả đường dẫn ảnh (dạng /media/rooms/LP01.jpg?v=...) của vị trí; null nếu chưa có ảnh trên API.</summary>
    Task<string?> GetUrlAsync(string? slot);
}

public class RoomImageCatalog : IRoomImageCatalog
{
    // Danh sách ảnh đổi rất ít nên cache ngắn dùng chung mọi request: không gọi API cho từng khung ảnh (mỗi trang có nhiều khung),
    // và ảnh mới tải lên hiện ra trên Website sau tối đa vài giây.
    private static readonly TimeSpan ThoiGianCache = TimeSpan.FromSeconds(10);
    private static readonly SemaphoreSlim Khoa = new(1, 1);
    private static Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static DateTime _hetHanLuc = DateTime.MinValue;

    private readonly ApiClient _apiClient;

    public RoomImageCatalog(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<string?> GetUrlAsync(string? slot)
    {
        if (string.IsNullOrWhiteSpace(slot)) return null;
        await LamMoiNeuHetHanAsync();
        return _cache.TryGetValue(slot, out var url) ? url : null;
    }

    private async Task LamMoiNeuHetHanAsync()
    {
        if (DateTime.UtcNow < _hetHanLuc) return;

        await Khoa.WaitAsync();
        try
        {
            if (DateTime.UtcNow < _hetHanLuc) return;

            var moi = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var result = await _apiClient.GetAsync<List<RoomImageModel>>("api/v1/media/rooms");
                if (result.Success && result.Data is not null)
                {
                    foreach (var anh in result.Data)
                        moi[anh.Slot] = $"/media/rooms/{anh.TenFile}?v={anh.PhienBan}";
                }
            }
            catch (HttpRequestException)
            {
                // API tạm thời không truy cập được: coi như chưa có ảnh (Website vẫn chạy, hiện khung gradient / ảnh copy tay).
            }

            _cache = moi;
            _hetHanLuc = DateTime.UtcNow + ThoiGianCache;
        }
        finally
        {
            Khoa.Release();
        }
    }

    private class RoomImageModel
    {
        public string Slot { get; set; } = null!;
        public string TenFile { get; set; } = null!;
        public long PhienBan { get; set; }
    }
}
