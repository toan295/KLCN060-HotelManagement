using System.Diagnostics;
using KLCN060.Web.Models;
using KLCN060.Web.Models.Home;
using KLCN060.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Web.Controllers;

public class HomeController : Controller
{
    private readonly ApiClient _apiClient;
    private readonly ILogger<HomeController> _logger;

    public HomeController(ApiClient apiClient, ILogger<HomeController> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    // GET / — W1 Trang chủ
    // Gọi GET /room-types (công khai, không cần token) - thay cho dữ liệu HTML viết cứng trước đây (Mục 5).
    public async Task<IActionResult> Index()
    {
        var result = await _apiClient.GetAsync<List<RoomTypeViewModel>>("api/v1/room-types");

        if (!result.Success)
            _logger.LogWarning("Không lấy được danh sách loại phòng từ API: {Message}", result.ErrorMessage);

        return View(result.Data ?? new List<RoomTypeViewModel>());
    }

    // GET /Home/Search — W2 Kết quả tìm kiếm
    // Giai đoạn 2 CHƯA có /room-types/availability lọc theo tồn phòng thật (thuộc Giai đoạn 4)
    // -> tạm gọi GET /room-types (toàn bộ danh mục) và lọc PHÍA CLIENT bằng JavaScript (Mục 5).
    public async Task<IActionResult> Search(DateOnly? ngayNhan, DateOnly? ngayTra, int soKhach = 1)
    {
        var result = await _apiClient.GetAsync<List<RoomTypeViewModel>>("api/v1/room-types");

        if (!result.Success)
            _logger.LogWarning("Không lấy được danh sách loại phòng từ API: {Message}", result.ErrorMessage);

        var model = new SearchViewModel
        {
            NgayNhan = ngayNhan,
            NgayTra = ngayTra,
            SoKhach = soKhach,
            DanhSachLoaiPhong = result.Data ?? new List<RoomTypeViewModel>()
        };

        return View(model);
    }

    // GET /Home/Detail/{maLoai} — W3 Chi tiết phòng
    // Gọi GET /room-types/{maLoai} lấy đúng 1 loại phòng (Mục 5).
    public async Task<IActionResult> Detail(string maLoai)
    {
        var result = await _apiClient.GetAsync<RoomTypeViewModel>($"api/v1/room-types/{maLoai}");

        if (!result.Success || result.Data is null)
            return NotFound();

        return View(result.Data);
    }

    // GET /Home/Services — W13 Giới thiệu & Dịch vụ
    // Gọi GET /services (endpoint công khai) - thay cho bảng dữ liệu viết cứng (Mục 5).
    public async Task<IActionResult> Services()
    {
        var result = await _apiClient.GetAsync<List<ServiceViewModel>>("api/v1/services");

        if (!result.Success)
            _logger.LogWarning("Không lấy được danh sách dịch vụ từ API: {Message}", result.ErrorMessage);

        return View(result.Data ?? new List<ServiceViewModel>());
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
