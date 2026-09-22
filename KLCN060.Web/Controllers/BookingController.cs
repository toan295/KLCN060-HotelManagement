using KLCN060.Web.Models.Booking;
using KLCN060.Web.Models.Home;
using KLCN060.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Web.Controllers;

// W6-W10: luồng đặt phòng, xác nhận cọc, lịch sử, hủy đặt phòng (Giai đoạn 4-5, Mục 5B).
// Toàn bộ action yêu cầu đã đăng nhập (JWT trong Session) vì các API /bookings/* đều [Authorize].
public class BookingController : Controller
{
    private readonly ApiClient _apiClient;
    private readonly ILogger<BookingController> _logger;

    public BookingController(ApiClient apiClient, ILogger<BookingController> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    // GET /Booking/Book?maLoai=... — W6 Đặt phòng cá nhân
    // maLoai đã biết sẵn từ link ở W3, chỉ cần điền ngày nhận/trả + số khách.
    [HttpGet]
    public async Task<IActionResult> Book(string maLoai)
    {
        if (!_apiClient.IsLoggedIn)
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Book), new { maLoai }) });

        var roomTypeResult = await _apiClient.GetAsync<RoomTypeViewModel>($"api/v1/room-types/{maLoai}");
        if (!roomTypeResult.Success || roomTypeResult.Data is null)
            return NotFound();

        var model = new BookViewModel
        {
            MaLoai = roomTypeResult.Data.MaLoai,
            TenLoai = roomTypeResult.Data.TenLoai,
            DonGia = roomTypeResult.Data.DonGia,
            SoKhach = 1
        };
        return View(model);
    }

    // POST /Booking/Book — W6
    // Gọi POST /bookings với loaiDatPhong="CA_NHAN", phongCanDat=[{ maLoai, soLuong: 1 }]. maKhach KHÔNG gửi (lấy từ JWT).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(BookViewModel model)
    {
        if (!_apiClient.IsLoggedIn)
            return RedirectToAction("Login", "Account");

        // Validate ngày trả > ngày nhận phía server (đã có HTML5 min attribute phía client).
        if (model.NgayNhan.HasValue && model.NgayTra.HasValue && model.NgayTra <= model.NgayNhan)
            ModelState.AddModelError(nameof(model.NgayTra), "Ngày trả phải sau ngày nhận.");

        if (!ModelState.IsValid)
        {
            await NapLaiThongTinLoaiPhong(model);
            return View(model);
        }

        var body = new
        {
            loaiDatPhong = "CA_NHAN",
            ngayDonDuKien = model.NgayNhan!.Value.ToString("yyyy-MM-dd"),
            ngayTraDuKien = model.NgayTra!.Value.ToString("yyyy-MM-dd"),
            phongCanDat = new[] { new { maLoai = model.MaLoai, soLuong = 1 } }
        };

        var result = await _apiClient.PostAsync<BookingViewModel>("api/v1/bookings", body, requireAuth: true);

        if (!result.Success)
        {
            // Lỗi 409 (hết phòng): hiển thị đúng nguyên văn error.message, giữ lại dữ liệu đã nhập (Mục 5B).
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Đặt phòng thất bại.");
            await NapLaiThongTinLoaiPhong(model);
            return View(model);
        }

        return RedirectToAction(nameof(Confirmation), new { maPhieuDat = result.Data!.MaPhieuDat });
    }

    private async Task NapLaiThongTinLoaiPhong(BookViewModel model)
    {
        var roomTypeResult = await _apiClient.GetAsync<RoomTypeViewModel>($"api/v1/room-types/{model.MaLoai}");
        if (roomTypeResult.Success && roomTypeResult.Data is not null)
        {
            model.TenLoai = roomTypeResult.Data.TenLoai;
            model.DonGia = roomTypeResult.Data.DonGia;
        }
    }

    // GET /Booking/BookGroup — W7 Đặt phòng theo đoàn
    [HttpGet]
    public async Task<IActionResult> BookGroup()
    {
        if (!_apiClient.IsLoggedIn)
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(BookGroup)) });

        var model = new BookGroupViewModel();
        await NapDanhSachLoaiPhong(model);
        model.PhongCanDat.Add(new BookGroupRoomLineViewModel());
        return View(model);
    }

    // POST /Booking/BookGroup — W7
    // Gọi POST /bookings với loaiDatPhong="DOAN", nhiều dòng phongCanDat.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BookGroup(BookGroupViewModel model)
    {
        if (!_apiClient.IsLoggedIn)
            return RedirectToAction("Login", "Account");

        if (model.NgayNhan.HasValue && model.NgayTra.HasValue && model.NgayTra <= model.NgayNhan)
            ModelState.AddModelError(nameof(model.NgayTra), "Ngày trả phải sau ngày nhận.");

        var dongHopLe = model.PhongCanDat
            .Where(dong => !string.IsNullOrWhiteSpace(dong.MaLoai) && dong.SoLuong > 0)
            .ToList();

        if (dongHopLe.Count == 0)
            ModelState.AddModelError(string.Empty, "Vui lòng chọn ít nhất 1 loại phòng.");

        if (!ModelState.IsValid)
        {
            await NapDanhSachLoaiPhong(model);
            return View(model);
        }

        var body = new
        {
            loaiDatPhong = "DOAN",
            ngayDonDuKien = model.NgayNhan!.Value.ToString("yyyy-MM-dd"),
            ngayTraDuKien = model.NgayTra!.Value.ToString("yyyy-MM-dd"),
            phongCanDat = dongHopLe.Select(dong => new { maLoai = dong.MaLoai, soLuong = dong.SoLuong })
        };

        var result = await _apiClient.PostAsync<BookingViewModel>("api/v1/bookings", body, requireAuth: true);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Đặt phòng thất bại.");
            await NapDanhSachLoaiPhong(model);
            return View(model);
        }

        return RedirectToAction(nameof(Confirmation), new { maPhieuDat = result.Data!.MaPhieuDat });
    }

    private async Task NapDanhSachLoaiPhong(BookGroupViewModel model)
    {
        var result = await _apiClient.GetAsync<List<RoomTypeViewModel>>("api/v1/room-types");
        model.DanhSachLoaiPhong = result.Data ?? new List<RoomTypeViewModel>();
    }

    // GET /Booking/Confirmation?maPhieuDat=... — W8 Xác nhận đặt phòng / hóa đơn cọc
    [HttpGet]
    public async Task<IActionResult> Confirmation(string maPhieuDat)
    {
        if (!_apiClient.IsLoggedIn)
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Confirmation), new { maPhieuDat }) });

        var result = await _apiClient.GetAsync<BookingViewModel>($"api/v1/bookings/{maPhieuDat}", requireAuth: true);
        if (!result.Success || result.Data is null)
            return NotFound();

        return View(result.Data);
    }

    // POST /Booking/ConfirmDeposit — W8 nút "Tôi đã chuyển khoản"
    // MÔ PHỎNG: không có cổng thanh toán thật ở giữa, gọi thẳng API xác nhận (Mục 1).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmDeposit(string maPhieuDat)
    {
        if (!_apiClient.IsLoggedIn)
            return RedirectToAction("Login", "Account");

        var result = await _apiClient.PostAsync<BookingViewModel>($"api/v1/bookings/{maPhieuDat}/confirm-deposit", null, requireAuth: true);

        if (!result.Success)
            TempData["ErrorMessage"] = result.ErrorMessage ?? "Xác nhận đặt cọc thất bại.";

        return RedirectToAction(nameof(Confirmation), new { maPhieuDat });
    }

    // GET /Booking/MyBookings — W9 Lịch sử đặt phòng của tôi
    [HttpGet]
    public async Task<IActionResult> MyBookings()
    {
        if (!_apiClient.IsLoggedIn)
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(MyBookings)) });

        var result = await _apiClient.GetAsync<List<BookingSummaryViewModel>>("api/v1/bookings/me", requireAuth: true);

        if (!result.Success)
            _logger.LogWarning("Không lấy được lịch sử đặt phòng: {Message}", result.ErrorMessage);

        return View(result.Data ?? new List<BookingSummaryViewModel>());
    }

    // GET /Booking/Detail?maPhieuDat=... — W10 Chi tiết đơn / Hủy đặt phòng (dùng cho MỌI trạng thái)
    [HttpGet]
    public async Task<IActionResult> Detail(string maPhieuDat)
    {
        if (!_apiClient.IsLoggedIn)
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Detail), new { maPhieuDat }) });

        var result = await _apiClient.GetAsync<BookingViewModel>($"api/v1/bookings/{maPhieuDat}", requireAuth: true);
        if (!result.Success || result.Data is null)
            return NotFound();

        return View(result.Data);
    }

    // POST /Booking/Cancel — W10 Hủy đặt phòng
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(string maPhieuDat)
    {
        if (!_apiClient.IsLoggedIn)
            return RedirectToAction("Login", "Account");

        // KH không gửi lyDo — để trống (lyDo chỉ bắt buộc khi LT ghi đè chính sách) - Mục 5B.
        var result = await _apiClient.PostAsync<CancelResultViewModel>($"api/v1/bookings/{maPhieuDat}/cancel", new { }, requireAuth: true);

        if (!result.Success)
        {
            // 409 KHONG_THE_HUY_DA_NHAN_PHONG: thông báo rõ ràng thay vì lỗi chung chung (Mục 5B).
            TempData["ErrorMessage"] = result.ErrorCode == "KHONG_THE_HUY_DA_NHAN_PHONG"
                ? "Đơn đã được nhận phòng, vui lòng liên hệ trực tiếp khách sạn để được hỗ trợ."
                : result.ErrorMessage ?? "Hủy đặt phòng thất bại.";
        }

        return RedirectToAction(nameof(Detail), new { maPhieuDat });
    }
}
