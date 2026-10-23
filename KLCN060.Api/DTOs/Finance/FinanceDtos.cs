using System.ComponentModel.DataAnnotations;

namespace KLCN060.Api.DTOs.Finance;

public class ResolveBankNoticeRequest
{
    /// <summary>DA_XU_LY_NGOAI (đã xử lý ngoài hệ thống) hoặc BO_QUA.</summary>
    [Required(ErrorMessage = "Thiếu huong.")]
    public string Huong { get; set; } = null!;

    [Required(ErrorMessage = "Cần nhập ghi chú xử lý.")]
    [StringLength(500, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string GhiChu { get; set; } = null!;
}
