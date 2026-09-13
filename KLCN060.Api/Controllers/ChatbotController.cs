using KLCN060.Api.DTOs.Chatbot;
using KLCN060.Api.DTOs.Common;
using KLCN060.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

[ApiController]
[Route("api/v1/chatbot")]
public class ChatbotController : ControllerBase
{
    private readonly IChatbotService _chatbotService;

    public ChatbotController(IChatbotService chatbotService)
    {
        _chatbotService = chatbotService;
    }

    [HttpPost("ask")]
    [AllowAnonymous]
    public async Task<IActionResult> Ask([FromBody] ChatbotAskRequest request)
    {
        var cauTraLoi = await _chatbotService.AskAsync(request.CauHoi);
        return Ok(ApiResponse<ChatbotAskResponse>.Ok(new ChatbotAskResponse { CauTraLoi = cauTraLoi }));
    }
}
