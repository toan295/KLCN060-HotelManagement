using KLCN060.Web.Models.Account;
using KLCN060.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Web.Controllers;

public class AccountController : Controller
{
    private readonly ApiClient _apiClient;

    public AccountController(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    // GET /Account/Register — W4
    [HttpGet]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    // POST /Account/Register — W4
    // Gọi POST /auth/register với đúng body { hoTen, soDT, email, matKhau } (Mục 5).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var body = new
        {
            hoTen = model.HoTen,
            soDT = model.SoDT,
            email = model.Email,
            matKhau = model.MatKhau
        };

        var result = await _apiClient.PostAsync<object?>("api/v1/auth/register", body);

        if (!result.Success)
        {
            // Lỗi 409 (SĐT đã tồn tại): hiển thị đúng nguyên văn error.message từ API (Mục 5).
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Đăng ký thất bại.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Đăng ký thành công, vui lòng đăng nhập";
        return RedirectToAction(nameof(Login));
    }

    // GET /Account/Login — W5
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    // POST /Account/Login — W5
    // Thành công -> lưu accessToken/refreshToken vào Session, chuyển hướng về W1 (Mục 5).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var body = new
        {
            tenDangNhap = model.TenDangNhap,
            matKhau = model.MatKhau
        };

        var result = await _apiClient.PostAsync<LoginResultModel>("api/v1/auth/login", body);

        if (!result.Success)
        {
            // Lỗi 423 (tài khoản bị khóa): hiển thị màu cảnh báo khác biệt với lỗi sai mật khẩu 401 (Mục 5).
            ViewBag.IsLocked = result.StatusCode == 423;
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Đăng nhập thất bại.");
            return View(model);
        }

        _apiClient.SaveTokens(result.Data!.AccessToken, result.Data.RefreshToken);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        _apiClient.ClearSession();
        return RedirectToAction("Index", "Home");
    }

    // GET /Account/Profile — W11 Hồ sơ cá nhân
    // Gọi GET /users/me (JWT trong Session, không cần tham số) - Mục 5B.
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        if (!_apiClient.IsLoggedIn)
            return RedirectToAction(nameof(Login), new { returnUrl = Url.Action(nameof(Profile)) });

        var result = await _apiClient.GetAsync<ProfileApiModel>("api/v1/users/me", requireAuth: true);
        if (!result.Success || result.Data is null)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "Không tải được hồ sơ.";
            return RedirectToAction("Index", "Home");
        }

        var model = new ProfileViewModel
        {
            HoTen = result.Data.HoTen,
            SoDT = result.Data.SoDT,
            Email = result.Data.Email,
            DiaChi = result.Data.DiaChi,
            CCCD = result.Data.CCCD
        };
        return View(model);
    }

    // POST /Account/Profile — W11
    // Gọi PUT /users/me với { hoTen, soDT, email, diaChi }. KHÔNG gửi CCCD (Mục 5B).
    // Thành công -> hiển thị thông báo NGAY TRÊN TRANG, không chuyển hướng.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        if (!_apiClient.IsLoggedIn)
            return RedirectToAction(nameof(Login));

        if (!ModelState.IsValid)
            return View(model);

        var body = new
        {
            hoTen = model.HoTen,
            soDT = model.SoDT,
            email = model.Email,
            diaChi = model.DiaChi
        };

        var result = await _apiClient.PutAsync<ProfileApiModel>("api/v1/users/me", body, requireAuth: true);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Cập nhật hồ sơ thất bại.");
            return View(model);
        }

        model.CCCD = result.Data?.CCCD;
        ViewBag.SuccessMessage = "Cập nhật hồ sơ thành công";
        return View(model);
    }

    private class LoginResultModel
    {
        public string AccessToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
        public int ExpiresInMinutes { get; set; }
    }
}
