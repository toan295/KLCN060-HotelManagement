using Microsoft.AspNetCore.Mvc.Filters;

namespace KLCN060.Api.Middlewares;

/// <summary>
/// Kiểm tra tham số khoảng ngày (from, to) của các báo cáo trước khi tính: bắt buộc phải có (thiếu thì DateOnly mặc định = năm 0001
/// sẽ cho dữ liệu giả hoặc lỗi 500) và nằm trong khoảng năm hợp lý (năm 9999/0001 làm tràn phép cộng ngày).
/// </summary>
public class KhoangNgayBaoCaoFilter : IActionFilter
{
    public const int NamNhoNhat = 2000, NamLonNhat = 2100;

    public void OnActionExecuting(ActionExecutingContext context)
    {
        // Tham số không có trong query thì MVC có thể không đưa vào ActionArguments: dựa vào khai báo của action thay vì giá trị đã bind.
        var tenThamSo = context.ActionDescriptor.Parameters.Select(x => x.Name).ToList();
        if (!tenThamSo.Contains("from") || !tenThamSo.Contains("to")) return;

        var query = context.HttpContext.Request.Query;
        if (string.IsNullOrWhiteSpace(query["from"]) || string.IsNullOrWhiteSpace(query["to"]))
            throw new ApiException(StatusCodes.Status400BadRequest, "THIEU_KHOANG_NGAY", "Cần chọn đủ khoảng ngày (from và to).", string.IsNullOrWhiteSpace(query["from"]) ? "from" : "to");
        var from = context.ActionArguments.TryGetValue("from", out var f) && f is DateOnly df ? df : default;
        var to = context.ActionArguments.TryGetValue("to", out var t) && t is DateOnly dt ? dt : default;
        if (from.Year < NamNhoNhat || from.Year > NamLonNhat)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_KHONG_HOP_LE", $"Ngày bắt đầu phải trong khoảng năm {NamNhoNhat}-{NamLonNhat}.", "from");
        if (to.Year < NamNhoNhat || to.Year > NamLonNhat)
            throw new ApiException(StatusCodes.Status400BadRequest, "KHOANG_NGAY_KHONG_HOP_LE", $"Ngày kết thúc phải trong khoảng năm {NamNhoNhat}-{NamLonNhat}.", "to");
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
