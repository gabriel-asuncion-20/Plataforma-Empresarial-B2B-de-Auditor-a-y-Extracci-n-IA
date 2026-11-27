using System.Security.Claims;
using AuditAI.Application.Common.Cqrs;
using AuditAI.Application.DTOs;
using AuditAI.Application.Features.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditAI.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(ICqrsDispatcher dispatcher) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequestDto request, CancellationToken ct)
    {
        try
        {
            var result = await dispatcher.SendCommandAsync<LoginCommand, AuthResponseDto>(
                new LoginCommand(request.Email, request.Password), ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPost("demo-login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> DemoLogin([FromBody] DemoLoginRequestDto request, CancellationToken ct)
    {
        var result = await dispatcher.SendCommandAsync<DemoLoginCommand, AuthResponseDto>(
            new DemoLoginCommand(request.Role), ct);
        return Ok(result);
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<UserDto> GetCurrentUser()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var name = User.FindFirstValue(ClaimTypes.Name) ?? "Usuario";
        var role = User.FindFirstValue(ClaimTypes.Role) ?? "Analyst";

        return Ok(new UserDto(
            Guid.TryParse(idStr, out var id) ? id : Guid.Empty,
            email,
            name,
            role));
    }
}
