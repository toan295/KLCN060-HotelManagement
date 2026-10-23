using System.Text.RegularExpressions;
using KLCN060.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Web.Controllers;

/// <summary>
/// Chuyển tiếp ảnh phòng từ KLCN060.Api (/media/rooms/...) để trình duyệt chỉ cần nói chuyện với Website
/// (không phải mở địa chỉ/chứng chỉ của API, không vướng CORS).
/// </summary>
[Route("media/rooms")]
public class MediaController : Controller
{
    // Chỉ nhận đúng tên ảnh phòng (LP01, LP01-2, hero) - không cho chuyển tiếp đường dẫn tùy ý sang API.
    private static readonly Regex TenFileHopLe = new(@"^(?:hero|LP\d{2}(?:-[1-3])?)\.(?:jpg|jpeg|png|webp)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ApiClient _apiClient;

    public MediaController(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet("{file}")]
    public async Task<IActionResult> Room(string file)
    {
        if (!TenFileHopLe.IsMatch(file))
            return NotFound();

        var anh = await _apiClient.GetBinaryAsync($"media/rooms/{file}");
        if (anh is null)
            return NotFound();

        // URL có ?v={phiên bản} đổi mỗi khi thay ảnh nên cache lâu được; không có v thì luôn hỏi lại.
        Response.Headers.CacheControl = Request.Query.ContainsKey("v") ? "public, max-age=604800" : "no-cache";
        return File(anh.Value.Data, anh.Value.ContentType);
    }
}
