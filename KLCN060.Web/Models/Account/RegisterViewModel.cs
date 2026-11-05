using System.ComponentModel.DataAnnotations;

namespace KLCN060.Web.Models.Account;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Họ tên từ 2 đến 100 ký tự.")]
    // Không dùng \p{L}: kiểm tra này chạy cả phía trình duyệt (jQuery Validate), mà JavaScript không có cờ Unicode thì hiểu \p{L} là chữ "p{L}"
    // nên từ chối MỌI họ tên. Thay bằng "có ít nhất một ký tự không phải số / khoảng trắng / dấu câu ASCII" (chữ có dấu tiếng Việt thỏa điều kiện này).
    [RegularExpression(@"^.*[^\d\s!-/:-@\[-`{-~].*$", ErrorMessage = "Họ tên phải có chữ cái.")]
    [Display(Name = "Họ tên")]
    public string HoTen { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [Display(Name = "Số điện thoại")]
    public string SoDT { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng nhập email (dùng để khôi phục mật khẩu khi bạn quên).")]
    [Display(Name = "Email")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [MinLength(8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự.")]
    [MaxLength(64, ErrorMessage = "Mật khẩu tối đa 64 ký tự.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string MatKhau { get; set; } = null!;
}
