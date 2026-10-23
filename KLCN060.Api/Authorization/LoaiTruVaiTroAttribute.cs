using KLCN060.Api.Middlewares;
using Microsoft.AspNetCore.Mvc.Filters;

namespace KLCN060.Api.Authorization;

/// <summary>
/// Chặn các vai trò nêu tên (VD "QUAN_LY") dù họ có đủ quyền theo [HasPermission]: dùng cho chức năng chỉ dành cho vai trò khác
/// (VD bảng Ca trực của lễ tân - Quản lý theo dõi qua Bảng điều hành / Báo cáo, không dùng Ca trực).
/// Chạy sau bước xác thực/phân quyền nên người chưa đăng nhập vẫn nhận 401 như bình thường.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class LoaiTruVaiTroAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string[] _vaiTro;
    public string MaLoi { get; set; } = "VAI_TRO_KHONG_DUOC_DUNG";
    public string ThongBao { get; set; } = "Chức năng này không dành cho vai trò của bạn.";

    public LoaiTruVaiTroAttribute(params string[] vaiTro) => _vaiTro = vaiTro;

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated == true && _vaiTro.Any(user.IsInRole))
            throw new ApiException(StatusCodes.Status403Forbidden, MaLoi, ThongBao);
        return Task.CompletedTask;
    }
}
