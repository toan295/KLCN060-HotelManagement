using System.Diagnostics;
using KLCN060.Web.Helpers;
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
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var result = await _apiClient.GetAsync<List<RoomTypeViewModel>>("api/v1/room-types");

        if (!result.Success)
            _logger.LogWarning("Không lấy được danh sách loại phòng từ API: {Message}", result.ErrorMessage);

        return View(result.Data ?? new List<RoomTypeViewModel>());
    }

    // GET /Home/About — trang Giới thiệu (nội dung tĩnh + số hạng phòng thật từ API để hiển thị ảnh/liên kết)
    [HttpGet]
    public async Task<IActionResult> About()
    {
        var result = await _apiClient.GetAsync<List<RoomTypeViewModel>>("api/v1/room-types");
        return View(result.Data ?? new List<RoomTypeViewModel>());
    }

    // GET /Home/Search — W2 Kết quả tìm kiếm
    // Có đủ ngày nhận/trả -> gọi GET /room-types/availability (tồn phòng thật, đã trừ phòng đã đặt/đang ở, Giai đoạn 4).
    // Chưa chọn ngày (vào thẳng từ menu) -> hiển thị toàn bộ danh mục loại phòng như trước, không có badge tồn kho.
    [HttpGet]
    public async Task<IActionResult> Search(DateOnly? ngayNhan, DateOnly? ngayTra, int soKhach = 1)
    {
        // Chuẩn hóa 1 lần, dùng chung cho cả phần hiển thị "Số khách: N" lẫn tham số gửi API - tránh lệch
        // giữa số hiển thị (có thể âm/0 nếu nhập bậy) và số khách thực tế dùng để lọc sức chứa phòng.
        soKhach = Math.Max(soKhach, 1);
        var model = new SearchViewModel { NgayNhan = ngayNhan, NgayTra = ngayTra, SoKhach = soKhach };
        // Gửi form tìm kiếm (có tham số) mà thiếu ngày nhận/trả: nhắc chọn ngày thay vì lặng lẽ liệt kê phòng như thể đã kiểm tra phòng trống.
        var daGuiForm = Request.Query.ContainsKey("ngayNhan") || Request.Query.ContainsKey("ngayTra") || Request.Query.ContainsKey("soKhach");
        model.ThieuNgay = daGuiForm && (ngayNhan is null || ngayTra is null);

        if (ngayNhan is { } checkin && ngayTra is { } checkout && checkout > checkin)
        {
            model.DaLocTheoNgay = true;
            var result = await _apiClient.GetAsync<List<RoomTypeAvailabilityViewModel>>(
                $"api/v1/room-types/availability?checkin={checkin:yyyy-MM-dd}&checkout={checkout:yyyy-MM-dd}&guests={soKhach}");

            if (!result.Success)
            {
                _logger.LogWarning("Không lấy được tồn phòng từ API: {Message}", result.ErrorMessage);
                model.ThongBaoLoi = result.ErrorMessage;
            }

            model.DanhSachLoaiPhong = result.Data ?? new List<RoomTypeAvailabilityViewModel>();
        }
        else
        {
            var result = await _apiClient.GetAsync<List<RoomTypeViewModel>>("api/v1/room-types");

            if (!result.Success)
                _logger.LogWarning("Không lấy được danh sách loại phòng từ API: {Message}", result.ErrorMessage);

            // SoPhongTrong = -1: chưa biết tồn kho thật (chưa chọn ngày) -> view không hiển thị badge còn/hết phòng.
            model.DanhSachLoaiPhong = (result.Data ?? new List<RoomTypeViewModel>())
                .Select(x => new RoomTypeAvailabilityViewModel
                {
                    MaLoai = x.MaLoai,
                    TenLoai = x.TenLoai,
                    SoNguoiTieuChuan = x.SoNguoiTieuChuan,
                    DonGia = x.DonGia,
                    PhuThu = x.PhuThu,
                    SoPhongTrong = -1
                })
                .ToList();
        }

        return View(model);
    }

    // GET /Home/Detail/{maLoai} — W3 Chi tiết phòng
    // Gọi GET /room-types/{maLoai} lấy đúng 1 loại phòng (Mục 5).
    [HttpGet]
    public async Task<IActionResult> Detail(string maLoai)
    {
        var maUrl = MaThamChieu.DoanDuongDan(maLoai);
        if (maUrl is null)
            return NotFound();

        var result = await _apiClient.GetAsync<RoomTypeViewModel>($"api/v1/room-types/{maUrl}");

        if (!result.Success || result.Data is null)
            return result.KhongKetNoi ? StatusCode(503) : NotFound();

        return View(result.Data);
    }

    // GET /Home/Services — Dịch vụ (Giới thiệu đã tách sang /Home/About)
    // Gọi GET /services (endpoint công khai) - thay cho bảng dữ liệu viết cứng (Mục 5).
    [HttpGet]
    public async Task<IActionResult> Services()
    {
        var result = await _apiClient.GetAsync<List<ServiceViewModel>>("api/v1/services");

        if (!result.Success)
            _logger.LogWarning("Không lấy được danh sách dịch vụ từ API: {Message}", result.ErrorMessage);

        return View(result.Data ?? new List<ServiceViewModel>());
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? code)
    {
        // Gọi từ UseExceptionHandler (không có code → lỗi 500) hoặc UseStatusCodePagesWithReExecute (code = mã HTTP gốc).
        var ma = code is >= 400 and <= 599 ? code.Value : 500;
        return View(ErrorViewModel.Tu(ma, Activity.Current?.Id ?? HttpContext.TraceIdentifier));
    }
}
