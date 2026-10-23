using KLCN060.Api.DTOs.Chatbot;
using KLCN060.Api.DTOs.Common;
using KLCN060.Api.Services;
using KLCN060.Api.Services.Chatbot;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KLCN060.Api.Controllers;

[ApiController]
[Route("api/v1/chatbot")]
public class ChatbotController : ControllerBase
{
    public const string RateLimitPolicy = "chatbot";

    private readonly IChatbotService _chatbotService;

    public ChatbotController(IChatbotService chatbotService)
    {
        _chatbotService = chatbotService;
    }

    [HttpPost("ask")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicy)]
    public async Task<IActionResult> Ask([FromBody] ChatbotAskRequest request)
    {
        var ketQua = await _chatbotService.AskAsync(request.CauHoi);
        return Ok(ApiResponse<ChatbotAskResponse>.Ok(new ChatbotAskResponse { CauTraLoi = ketQua.CauTraLoi, GoiY = ketQua.GoiY.ToList() }));
    }

    /// <summary>Các câu hỏi gợi ý mặc định cho khung chat (một nguồn duy nhất, cũng là các gợi ý bản từ khóa trả về).</summary>
    [HttpGet("suggestions")]
    [AllowAnonymous]
    public IActionResult Suggestions()
        => Ok(ApiResponse<List<string>>.Ok(ChatbotGoiY.MacDinh.ToList()));
}
