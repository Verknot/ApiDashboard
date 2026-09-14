using ApiWorkbench.Api.Contracts;
using ApiWorkbench.Api.Domain;
using ApiWorkbench.Api.Infrastructure;
using ApiWorkbench.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiWorkbench.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Admin)]
[Route("api/admin/users")]
public sealed class UsersAdminController(IUserAdminService users, ILogger<UsersAdminController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminUserResponse>>> List(CancellationToken cancellationToken)
    {
        return Ok(await users.ListAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<AdminUserResponse>> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = User.GetUserId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        try
        {
            var created = await users.CreateAsync(actorId.Value, request, cancellationToken);
            logger.LogInformation("Admin {Actor} created user {UserId} ({Email})", actorId, created.Id, created.Email);
            return CreatedAtAction(nameof(List), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:int}/active")]
    public async Task<ActionResult<AdminUserResponse>> SetActive(
        int id,
        [FromBody] SetUserActiveRequest request,
        CancellationToken cancellationToken)
    {
        return await Mutate(id, (actor, token) => users.SetActiveAsync(actor, id, request.IsActive, token), cancellationToken);
    }

    [HttpPatch("{id:int}/role")]
    public async Task<ActionResult<AdminUserResponse>> SetRole(
        int id,
        [FromBody] SetUserRoleRequest request,
        CancellationToken cancellationToken)
    {
        return await Mutate(id, (actor, token) => users.SetRoleAsync(actor, id, request.Role, token), cancellationToken);
    }

    [HttpPost("{id:int}/password")]
    public async Task<ActionResult<AdminUserResponse>> ResetPassword(
        int id,
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        return await Mutate(id, (actor, token) => users.ResetPasswordAsync(actor, id, request.Password, token), cancellationToken);
    }

    private async Task<ActionResult<AdminUserResponse>> Mutate(
        int userId,
        Func<int, CancellationToken, Task<AdminUserResponse>> action,
        CancellationToken cancellationToken)
    {
        var actorId = User.GetUserId();
        if (actorId is null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await action(actorId.Value, cancellationToken));
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogWarning("User {UserId} not found", userId);
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
