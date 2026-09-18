using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace KLCN060.Desktop.Services;

public sealed class ApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _httpClient;
    private string? _accessToken;

    /// <summary>
    /// Access token đã hết hạn VÀ làm mới bằng refresh token cũng thất bại - phiên đăng nhập đã kết thúc,
    /// ViewModel nên đưa người dùng quay lại D1 (tương đương ApiSessionExpiredException bên KLCN060.Web).
    /// </summary>
    public event Action? SessionExpired;

    public ApiClient(string baseUrl) => _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl, UriKind.Absolute) };
    public void SetAccessToken(string accessToken) => _accessToken = accessToken;
    public void ClearAccessToken() => _accessToken = null;
    public Task<KetQuaDangNhap> LoginAsync(string user, string pass) => PostAsync<YeuCauDangNhap, KetQuaDangNhap>("api/v1/auth/login", new(user, pass));
    public Task LogoutAsync() => SendKhongCoNoiDungAsync(HttpMethod.Post, "api/v1/auth/logout");
    public Task<List<LoaiPhongDto>> GetRoomTypesAsync() => GetAsync<List<LoaiPhongDto>>("api/v1/room-types");
    public Task<LoaiPhongDto> CreateRoomTypeAsync(YeuCauLoaiPhong x) => PostAsync<YeuCauLoaiPhong, LoaiPhongDto>("api/v1/room-types", x);
    public Task<LoaiPhongDto> UpdateRoomTypeAsync(string id, YeuCauLoaiPhong x) => PutAsync<YeuCauLoaiPhong, LoaiPhongDto>($"api/v1/room-types/{id}", x);
    public Task<List<PhongDto>> GetRoomsAsync() => GetAsync<List<PhongDto>>("api/v1/rooms");
    public Task<PhongDto> CreateRoomAsync(YeuCauPhong x) => PostAsync<YeuCauPhong, PhongDto>("api/v1/rooms", x);
    public Task<PhongDto> UpdateRoomAsync(string id, YeuCauPhong x) => PutAsync<YeuCauPhong, PhongDto>($"api/v1/rooms/{id}", x);
    public Task<List<KhuyenMaiDto>> GetPromotionsAsync() => GetAsync<List<KhuyenMaiDto>>("api/v1/promotions");
    public Task<KhuyenMaiDto> CreatePromotionAsync(YeuCauKhuyenMai x) => PostAsync<YeuCauKhuyenMai, KhuyenMaiDto>("api/v1/promotions", x);
    public Task<KhuyenMaiDto> UpdatePromotionAsync(string id, YeuCauKhuyenMai x) => PutAsync<YeuCauKhuyenMai, KhuyenMaiDto>($"api/v1/promotions/{id}", x);
    public Task<List<DichVuDto>> GetServicesAsync() => GetAsync<List<DichVuDto>>("api/v1/services");
    public Task<DichVuDto> CreateServiceAsync(YeuCauDichVu x) => PostAsync<YeuCauDichVu, DichVuDto>("api/v1/services", x);
    public Task<DichVuDto> UpdateServiceAsync(string id, YeuCauDichVu x) => PutAsync<YeuCauDichVu, DichVuDto>($"api/v1/services/{id}", x);
    public Task DeleteServiceAsync(string id) => SendKhongCoNoiDungAsync(HttpMethod.Delete, $"api/v1/services/{id}");
    public Task<List<CoSoVatChatDto>> GetFacilitiesAsync(string roomId) => GetAsync<List<CoSoVatChatDto>>($"api/v1/rooms/{roomId}/facilities");

    private Task<T> GetAsync<T>(string url) => GuiCoLamMoiAsync<T>(HttpMethod.Get, url, null);
    private Task<TResponse> PostAsync<TRequest, TResponse>(string url, TRequest body) => GuiCoLamMoiAsync<TResponse>(HttpMethod.Post, url, body);
    private Task<TResponse> PutAsync<TRequest, TResponse>(string url, TRequest body) => GuiCoLamMoiAsync<TResponse>(HttpMethod.Put, url, body);

    /// <summary>
    /// Gửi request; nếu bị 401 (access token hết hạn) thì tự gọi /auth/refresh 1 lần bằng RefreshToken trong
    /// AppSession rồi thử lại chính request đó - tương đương cơ chế đã có ở KLCN060.Web (ApiClient.SendAsync).
    /// Nếu làm mới cũng thất bại, phát SessionExpired để ViewModel quay về màn hình đăng nhập.
    /// </summary>
    private async Task<T> GuiCoLamMoiAsync<T>(HttpMethod method, string url, object? body)
    {
        using var request = Create(method, url, body);
        using var response = await _httpClient.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.Unauthorized && await ThuLamMoiTokenAsync())
        {
            using var requestMoi = Create(method, url, body);
            using var responseMoi = await _httpClient.SendAsync(requestMoi);
            return await DocKetQuaAsync<T>(responseMoi);
        }

        return await DocKetQuaAsync<T>(response);
    }

    private async Task SendKhongCoNoiDungAsync(HttpMethod method, string url)
    {
        using var request = Create(method, url);
        using var response = await _httpClient.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.Unauthorized && await ThuLamMoiTokenAsync())
        {
            using var requestMoi = Create(method, url);
            using var responseMoi = await _httpClient.SendAsync(requestMoi);
            if (!responseMoi.IsSuccessStatusCode) throw await ErrorAsync(responseMoi);
            return;
        }

        if (!response.IsSuccessStatusCode) throw await ErrorAsync(response);
    }

    private async Task<bool> ThuLamMoiTokenAsync()
    {
        var refreshToken = AppSession.Current.RefreshToken;
        if (string.IsNullOrEmpty(refreshToken))
        {
            SessionExpired?.Invoke();
            return false;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/auth/refresh")
            {
                Content = JsonContent.Create(new { refreshToken }, options: JsonOptions)
            };
            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                SessionExpired?.Invoke();
                return false;
            }

            var data = await response.Content.ReadFromJsonAsync<PhanHoiApi<KetQuaLamMoiToken>>(JsonOptions);
            if (data?.Success != true || data.Data is null)
            {
                SessionExpired?.Invoke();
                return false;
            }

            _accessToken = data.Data.AccessToken;
            AppSession.Current.CapNhatAccessToken(data.Data.AccessToken);
            return true;
        }
        catch (Exception)
        {
            SessionExpired?.Invoke();
            return false;
        }
    }

    private HttpRequestMessage Create(HttpMethod method, string url, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrWhiteSpace(_accessToken)) request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
        if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
        return request;
    }

    private async Task<T> DocKetQuaAsync<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode) throw await ErrorAsync(response);
        var data = await response.Content.ReadFromJsonAsync<PhanHoiApi<T>>(JsonOptions);
        return data?.Success == true && data.Data is not null ? data.Data : throw new LoiYeuCauApi(response.StatusCode, "API khong tra du lieu.");
    }

    private static async Task<LoiYeuCauApi> ErrorAsync(HttpResponseMessage response)
    {
        // Mot so loi (vi du 401/403 do [Authorize] tu choi truoc khi vao Controller) co the tra ve body rong
        // hoac khong dung JSON - khong duoc de ReadFromJsonAsync nem loi khac lam mat thong bao goc cho nguoi dung.
        try
        {
            var raw = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(raw))
                return new LoiYeuCauApi(response.StatusCode, ThongBaoMacDinh(response.StatusCode));

            var body = JsonSerializer.Deserialize<PhanHoiLoiApi>(raw, JsonOptions);
            var message = body?.Error?.Message;
            return new LoiYeuCauApi(response.StatusCode, string.IsNullOrWhiteSpace(message) ? ThongBaoMacDinh(response.StatusCode) : message);
        }
        catch (JsonException)
        {
            return new LoiYeuCauApi(response.StatusCode, ThongBaoMacDinh(response.StatusCode));
        }
    }

    private static string ThongBaoMacDinh(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => "Bạn cần đăng nhập lại để thực hiện thao tác này.",
        HttpStatusCode.Forbidden => "Bạn không có quyền thực hiện thao tác này.",
        _ => "Không thể kết nối tới máy chủ."
    };
}

public sealed record YeuCauDangNhap(string TenDangNhap, string MatKhau);
public sealed record YeuCauLoaiPhong(string TenLoai, int SoNguoiTieuChuan, decimal DonGia, decimal PhuThu);
public sealed record YeuCauPhong(string TenPhong, int Tang, string MaLoai, string? TinhTrang);
public sealed record YeuCauKhuyenMai(string TenKM, decimal? PhanTramKM, DateOnly NgayBatDau, DateOnly NgayKetThuc, string? DieuKien, string LoaiKM, decimal? GiaTri, int? SoLuongGioiHan);
public sealed record YeuCauDichVu(string TenDV, decimal GiaDV, string DonViTinh);
public sealed class KetQuaDangNhap { public string AccessToken { get; init; } = ""; public string RefreshToken { get; init; } = ""; }
public sealed class KetQuaLamMoiToken { public string AccessToken { get; init; } = ""; }
public sealed class LoaiPhongDto { public string MaLoai { get; init; } = ""; public string TenLoai { get; init; } = ""; public int SoNguoiTieuChuan { get; init; } public decimal DonGia { get; init; } public decimal PhuThu { get; init; } }
public sealed class PhongDto { public string MaPhong { get; init; } = ""; public string TenPhong { get; init; } = ""; public int Tang { get; init; } public string TinhTrang { get; init; } = ""; public string MaLoai { get; init; } = ""; public string TenLoaiPhong { get; init; } = ""; }
public sealed class KhuyenMaiDto
{
    public string MaKM { get; init; } = "";
    public string TenKM { get; init; } = "";
    public decimal? PhanTramKM { get; init; }
    public DateOnly NgayBatDau { get; init; }
    public DateOnly NgayKetThuc { get; init; }
    public string? DieuKien { get; init; }
    public string LoaiKM { get; init; } = "";
    public decimal? GiaTri { get; init; }

    // Tinh phia client tu 2 cot ngay, dung cho cot "Trang thai" o D12 (Muc 5, CLAUDE.md Trong).
    public string TrangThai => NgayBatDau <= DateOnly.FromDateTime(DateTime.Today) && NgayKetThuc >= DateOnly.FromDateTime(DateTime.Today)
        ? "Đang áp dụng"
        : "Đã kết thúc";
}
public sealed class DichVuDto { public string MaDV { get; init; } = ""; public string TenDV { get; init; } = ""; public decimal GiaDV { get; init; } public string DonViTinh { get; init; } = ""; }
public sealed class CoSoVatChatDto { public string MaSo { get; init; } = ""; public string Ten { get; init; } = ""; public int SoLuong { get; init; } public string TinhTrang { get; init; } = ""; public string? MaPhong { get; init; } }
public sealed class DongCoSoVatChat { public string MaSo { get; init; } = ""; public string Ten { get; init; } = ""; public string Phong { get; init; } = ""; public int SoLuong { get; init; } public string TinhTrang { get; init; } = ""; }
public sealed class PhanHoiApi<T> { public bool Success { get; init; } public T? Data { get; init; } }
public sealed class PhanHoiLoiApi { public LoiApi? Error { get; init; } }
public sealed class LoiApi { public string Message { get; init; } = ""; }
public sealed class LoiYeuCauApi : Exception { public HttpStatusCode StatusCode { get; } public LoiYeuCauApi(HttpStatusCode statusCode, string message) : base(message) => StatusCode = statusCode; }
