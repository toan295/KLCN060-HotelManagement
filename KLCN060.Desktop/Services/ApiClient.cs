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
    public ApiClient(string baseUrl) => _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl, UriKind.Absolute) };
    public void SetAccessToken(string accessToken) => _accessToken = accessToken;
    public Task<KetQuaDangNhap> LoginAsync(string user, string pass) => PostAsync<YeuCauDangNhap, KetQuaDangNhap>("api/v1/auth/login", new(user, pass));
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
    public Task DeleteServiceAsync(string id) => DeleteAsync($"api/v1/services/{id}");
    public Task<List<CoSoVatChatDto>> GetFacilitiesAsync(string roomId) => GetAsync<List<CoSoVatChatDto>>($"api/v1/rooms/{roomId}/facilities");

    private async Task<T> GetAsync<T>(string url) { using var r = Create(HttpMethod.Get, url); return await SendAsync<T>(r); }
    private async Task<TResponse> PostAsync<TRequest, TResponse>(string url, TRequest body) { using var r = Create(HttpMethod.Post, url, body); return await SendAsync<TResponse>(r); }
    private async Task<TResponse> PutAsync<TRequest, TResponse>(string url, TRequest body) { using var r = Create(HttpMethod.Put, url, body); return await SendAsync<TResponse>(r); }
    private async Task DeleteAsync(string url) { using var r = Create(HttpMethod.Delete, url); using var response = await _httpClient.SendAsync(r); if (!response.IsSuccessStatusCode) throw await ErrorAsync(response); }
    private HttpRequestMessage Create(HttpMethod method, string url, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrWhiteSpace(_accessToken)) request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
        if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
        return request;
    }
    private async Task<T> SendAsync<T>(HttpRequestMessage request)
    {
        using var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) throw await ErrorAsync(response);
        var data = await response.Content.ReadFromJsonAsync<PhanHoiApi<T>>(JsonOptions);
        return data?.Success == true && data.Data is not null ? data.Data : throw new LoiYeuCauApi(response.StatusCode, "API khong tra du lieu.");
    }
    private static async Task<LoiYeuCauApi> ErrorAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<PhanHoiLoiApi>(JsonOptions);
        return new LoiYeuCauApi(response.StatusCode, body?.Error?.Message ?? "Khong the ket noi toi may chu.");
    }
}

public sealed record YeuCauDangNhap(string TenDangNhap, string MatKhau);
public sealed record YeuCauLoaiPhong(string TenLoai, int SoNguoiTieuChuan, decimal DonGia, decimal PhuThu);
public sealed record YeuCauPhong(string TenPhong, int Tang, string MaLoai, string? TinhTrang);
public sealed record YeuCauKhuyenMai(string TenKM, decimal? PhanTramKM, DateOnly NgayBatDau, DateOnly NgayKetThuc, string? DieuKien, string LoaiKM, decimal? GiaTri, int? SoLuongGioiHan);
public sealed record YeuCauDichVu(string TenDV, decimal GiaDV, string DonViTinh);
public sealed class KetQuaDangNhap { public string AccessToken { get; init; } = ""; public string RefreshToken { get; init; } = ""; }
public sealed class LoaiPhongDto { public string MaLoai { get; init; } = ""; public string TenLoai { get; init; } = ""; public int SoNguoiTieuChuan { get; init; } public decimal DonGia { get; init; } public decimal PhuThu { get; init; } }
public sealed class PhongDto { public string MaPhong { get; init; } = ""; public string TenPhong { get; init; } = ""; public int Tang { get; init; } public string TinhTrang { get; init; } = ""; public string MaLoai { get; init; } = ""; public string TenLoaiPhong { get; init; } = ""; }
public sealed class KhuyenMaiDto { public string MaKM { get; init; } = ""; public string TenKM { get; init; } = ""; public decimal? PhanTramKM { get; init; } public DateOnly NgayBatDau { get; init; } public DateOnly NgayKetThuc { get; init; } public string? DieuKien { get; init; } public string LoaiKM { get; init; } = ""; public decimal? GiaTri { get; init; } }
public sealed class DichVuDto { public string MaDV { get; init; } = ""; public string TenDV { get; init; } = ""; public decimal GiaDV { get; init; } public string DonViTinh { get; init; } = ""; }
public sealed class CoSoVatChatDto { public string MaSo { get; init; } = ""; public string Ten { get; init; } = ""; public int SoLuong { get; init; } public string TinhTrang { get; init; } = ""; public string? MaPhong { get; init; } }
public sealed class DongCoSoVatChat { public string MaSo { get; init; } = ""; public string Ten { get; init; } = ""; public string Phong { get; init; } = ""; public int SoLuong { get; init; } public string TinhTrang { get; init; } = ""; }
public sealed class PhanHoiApi<T> { public bool Success { get; init; } public T? Data { get; init; } }
public sealed class PhanHoiLoiApi { public LoiApi? Error { get; init; } }
public sealed class LoiApi { public string Message { get; init; } = ""; }
public sealed class LoiYeuCauApi : Exception { public HttpStatusCode StatusCode { get; } public LoiYeuCauApi(HttpStatusCode statusCode, string message) : base(message) => StatusCode = statusCode; }
