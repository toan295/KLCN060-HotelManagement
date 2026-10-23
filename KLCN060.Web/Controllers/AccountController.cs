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

        var result = await _apiClient.PostAsync<RegisterResultModel>("api/v1/auth/register", body);

        if (!result.Success)
        {
            // Lỗi 409 (SĐT đã tồn tại): hiển thị đúng nguyên văn error.message từ API (Mục 5).
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Đăng ký thất bại.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Đăng ký thành công, vui lòng đăng nhập.";
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

        // Website chỉ dành cho khách hàng. Tài khoản nhân viên (Quản lý, Lễ tân, Kế toán, Buồng phòng) làm việc trên ứng dụng Desktop:
        // để nhân viên đăng nhập ở đây sẽ cho họ một phiên khách hàng "nửa vời" (đặt phòng không có khách, thấy menu của khách...).
        if (DocVaiTro(result.Data.AccessToken) != "KHACH_HANG")
        {
            try { await _apiClient.PostAsync<object>("api/v1/auth/logout", null, requireAuth: true); }
            catch (ApiSessionExpiredException) { }
            _apiClient.ClearSession();
            ModelState.AddModelError(string.Empty, "Tài khoản nhân viên không đăng nhập được trên Website. Vui lòng dùng ứng dụng quản lý (Desktop).");
            return View(model);
        }

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);

        return RedirectToAction("Index", "Home");
    }

    /// <summary>Đọc claim vaiTro từ payload của access token (chỉ để định tuyến giao diện; API vẫn tự kiểm tra quyền ở mọi endpoint).</summary>
    private static string? DocVaiTro(string accessToken)
    {
        try
        {
            var payload = accessToken.Split('.')[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var doc = System.Text.Json.JsonDocument.Parse(Convert.FromBase64String(payload));
            return doc.RootElement.TryGetProperty("vaiTro", out var vaiTro) ? vaiTro.GetString() : null;
        }
        catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException or IndexOutOfRangeException)
        {
            return null;
        }
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

    // ------------------------------------------------------------------ Quên mật khẩu: email -> mã xác nhận -> mật khẩu mới

    // GET /Account/ForgotPassword — bước 1: nhập EMAIL đã đăng ký
    [HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    // POST /Account/ForgotPassword — API kiểm tra email có tồn tại không rồi gửi mã xác nhận 6 số tới email đó.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await _apiClient.PostAsync<ForgotPasswordResultModel>("api/v1/auth/forgot-password", new { email = model.Email });
        if (!result.Success || result.Data is null)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không gửi được mã xác nhận, vui lòng thử lại.");
            return View(model);
        }

        // Bước 2: hiện ô nhập mã (giữ nguyên địa chỉ URL, email nằm trong ô ẩn của biểu mẫu).
        ViewBag.ThongBao = result.Data.ThongBao;
        return View("VerifyCode", new VerifyCodeViewModel { Email = model.Email.Trim() });
    }

    // POST /Account/ResendCode — "Gửi lại mã" ở bước 2.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendCode(VerifyCodeViewModel model)
    {
        ModelState.Clear();
        if (string.IsNullOrWhiteSpace(model.Email))
            return RedirectToAction(nameof(ForgotPassword));

        var result = await _apiClient.PostAsync<ForgotPasswordResultModel>("api/v1/auth/forgot-password", new { email = model.Email });
        if (result.Success && result.Data is not null)
            ViewBag.ThongBao = "Đã gửi lại mã xác nhận mới tới email của bạn. Mã cũ không còn dùng được.";
        else
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không gửi lại được mã, vui lòng thử lại.");

        model.Ma = string.Empty;
        return View("VerifyCode", model);
    }

    // POST /Account/VerifyCode — bước 2: đúng mã thì hiện giao diện đặt mật khẩu mới, sai mã thì báo lỗi.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyCode(VerifyCodeViewModel model)
    {
        KhongLuuVaKhongLoReferer();
        if (string.IsNullOrWhiteSpace(model.Email))
            return RedirectToAction(nameof(ForgotPassword));

        if (!ModelState.IsValid)
            return View(model);

        var result = await _apiClient.PostAsync<VerifyResetCodeResultModel>("api/v1/auth/forgot-password/verify", new { email = model.Email, ma = model.Ma.Trim() });
        if (!result.Success || result.Data is null)
        {
            ModelState.Remove(nameof(model.Ma));
            model.Ma = string.Empty;
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không xác nhận được mã, vui lòng thử lại.");
            return View(model);
        }

        return View("ResetPassword", new ResetPasswordViewModel { Token = result.Data.Token });
    }

    // POST /Account/ResetPassword — bước 3: đặt mật khẩu mới.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        KhongLuuVaKhongLoReferer();

        if (!ModelState.IsValid)
            return View(model);

        var result = await _apiClient.PostAsync<object?>("api/v1/auth/reset-password", new { token = model.Token, matKhauMoi = model.MatKhauMoi });
        if (!result.Success)
        {
            if (result.ErrorCode == "PHIEN_DAT_LAI_KHONG_HOP_LE")
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
                return RedirectToAction(nameof(ForgotPassword));
            }
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không đặt lại được mật khẩu.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.";
        return RedirectToAction(nameof(Login));
    }

    // ------------------------------------------------------------------ Đổi mật khẩu (đã đăng nhập)

    // GET /Account/ChangePassword — cần đăng nhập
    [HttpGet]
    public IActionResult ChangePassword()
    {
        if (!_apiClient.IsLoggedIn)
            return RedirectToAction(nameof(Login), new { returnUrl = Url.Action(nameof(ChangePassword)) });

        return View(new ChangePasswordViewModel());
    }

    // POST /Account/ChangePassword — thành công thì mọi phiên cũ (kể cả phiên này) bị thu hồi -> đăng nhập lại bằng mật khẩu mới.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!_apiClient.IsLoggedIn)
            return RedirectToAction(nameof(Login));

        if (!ModelState.IsValid)
            return View(model);

        var result = await _apiClient.PostAsync<object?>("api/v1/auth/change-password",
            new { matKhauCu = model.MatKhauCu, matKhauMoi = model.MatKhauMoi }, requireAuth: true);

        if (!result.Success)
        {
            if (result.StatusCode == 423)
            {
                // Sai mật khẩu hiện tại quá số lần -> tài khoản bị khóa: kết thúc phiên và báo rõ ở trang đăng nhập.
                _apiClient.ClearSession();
                TempData["ErrorMessage"] = result.ErrorMessage;
                return RedirectToAction(nameof(Login));
            }

            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không đổi được mật khẩu.");
            return View(model);
        }

        _apiClient.ClearSession();
        TempData["SuccessMessage"] = "Đổi mật khẩu thành công. Vui lòng đăng nhập lại bằng mật khẩu mới.";
        return RedirectToAction(nameof(Login));
    }

    // Các bước nhập mã / đặt mật khẩu chứa mã bí mật: không cho trình duyệt lưu cache và không gửi đi qua Referer.
    private void KhongLuuVaKhongLoReferer()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
    }

    private class LoginResultModel
    {
        public string AccessToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
        public int ExpiresInMinutes { get; set; }
    }
}
