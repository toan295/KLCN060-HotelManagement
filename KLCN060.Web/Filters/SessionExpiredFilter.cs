using KLCN060.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace KLCN060.Web.Filters;

public class SessionExpiredFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is ApiSessionExpiredException)
        {
            // Chỉ trang xem (GET) mới quay lại được sau đăng nhập; giữ cả query để về đúng phiếu/giao dịch đang xem.
            var request = context.HttpContext.Request;
            var returnUrl = HttpMethods.IsGet(request.Method) ? request.Path.Value + request.QueryString.Value : null;
            context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
            context.ExceptionHandled = true;
        }
    }
}
