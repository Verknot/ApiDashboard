using System.Security.Claims;
using ApiWorkbench.Api.Configuration;
using ApiWorkbench.Api.Contracts;
using ApiWorkbench.Api.Domain;
using ApiWorkbench.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ApiWorkbench.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAuthService authService,
    IPermissionService permissions,
    IOptions<JwtOptions> jwtOptions,
    IHostEnvironment environment,
    ILogger<AuthController> logger) : ControllerBase
{
    /// <summary>Вход: выставляет HttpOnly cookie access_token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<MeResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Укажите email и пароль." });
        }

        try
        {
            var user = await authService.ValidateCredentialsAsync(request.Email.Trim(), request.Password, cancellationToken);
            if (user is null)
            {
                return Unauthorized(new { message = "Неверный email или пароль." });
            }

            AppendAccessCookie(authService.CreateToken(user));
            logger.LogInformation("Пользователь {UserId} вошёл в систему", user.Id);
            return Ok(await ToMeAsync(user, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(AuthCookieNames.AccessToken, new CookieOptions
        {
            Path = "/",
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax
        });
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var user = await authService.GetByIdAsync(userId.Value, cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(await ToMeAsync(user, cancellationToken));
    }

    /// <summary>Смена пароля, в том числе обязательная при первом входе (F-AUTH-9).</summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            await authService.ChangePasswordAsync(userId.Value, request.CurrentPassword, request.NewPassword, cancellationToken);
            logger.LogInformation("Пользователь {UserId} сменил пароль", userId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private async Task<MeResponse> ToMeAsync(AuthUser user, CancellationToken cancellationToken)
    {
        var access = await permissions.GetAccessAsync(user.Id, cancellationToken);
        return new MeResponse(
            user.Id,
            user.Email,
            user.DisplayName,
            user.IsFirstLogin,
            user.Roles,
            user.OwnedServiceIds,
            access.CanSend,
            access.CanGenerateDto,
            access.IsAdmin);
    }

    private void AppendAccessCookie(string token)
    {
        Response.Cookies.Append(AuthCookieNames.AccessToken, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddHours(jwtOptions.Value.ExpirationHours)
        });
    }

    private int? GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        return int.TryParse(sub, out var id) ? id : null;
    }
}
