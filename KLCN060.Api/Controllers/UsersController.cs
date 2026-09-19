using KLCN060.Api.DTOs.Common;
using KLCN060.Api.DTOs.Guests;
using KLCN060.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KLCN060.Api.Controllers;

[ApiController]
[Route("api/v1/users/me")]
[Authorize(Roles = "KHACH_HANG")]
public class UsersController : ControllerBase
{
    private readonly IGuestService _guestService;

    public UsersController(IGuestService guestService)
    {
        _guestService = guestService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMe()
    {
        var result = await _guestService.GetMeAsync(CurrentUser.From(User).MaKhach);
        return Ok(ApiResponse<GuestDto>.Ok(result));
    }

    [HttpPut]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request)
    {
        var result = await _guestService.UpdateMeAsync(CurrentUser.From(User).MaKhach, request);
        return Ok(ApiResponse<GuestDto>.Ok(result));
    }
}
