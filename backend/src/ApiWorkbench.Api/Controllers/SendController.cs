using ApiWorkbench.Api.Contracts;
using ApiWorkbench.Api.Infrastructure;
using ApiWorkbench.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiWorkbench.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/send")]
public sealed class SendController(
    IProxySendService proxySend,
    IPermissionService permissions) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ProxySendResponse>> Send(
        [FromBody] ProxySendRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var access = await permissions.GetAccessAsync(userId.Value, cancellationToken);
        if (!access.CanSend)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Send is not allowed for this role." });
        }

        if (request.ServiceId is int serviceId && !access.CanSeeService(serviceId))
        {
            return NotFound();
        }

        try
        {
            return Ok(await proxySend.SendAsync(request, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
