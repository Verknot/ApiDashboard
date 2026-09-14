using ApiWorkbench.Api.Contracts;
using ApiWorkbench.Api.Infrastructure;
using ApiWorkbench.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiWorkbench.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/history")]
public sealed class HistoryController(IHistoryService history, IPermissionService permissions) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<HistoryItemResponse>>> List(
        [FromQuery] int? serviceId,
        [FromQuery] int? endpointId,
        [FromQuery] int? status,
        [FromQuery] string? q,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        return Ok(await history.ListAsync(userId.Value, serviceId, endpointId, status, q, take, cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<HistoryItemResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var item = await history.GetAsync(userId.Value, id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SaveHistoryRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var access = await permissions.GetAccessAsync(userId.Value, cancellationToken);
        if (!access.CanSend)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Нет права отправлять запросы." });
        }

        await history.SaveAsync(userId.Value, request, cancellationToken);
        return NoContent();
    }
}
