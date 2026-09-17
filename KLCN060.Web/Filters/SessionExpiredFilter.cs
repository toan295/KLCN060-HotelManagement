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
            context.Result = new RedirectToActionResult(
                "Login",
                "Account",
                new { returnUrl = context.HttpContext.Request.Path.Value });
            context.ExceptionHandled = true;
        }
    }
}
