using ApiWorkbench.Api.Contracts;
using ApiWorkbench.Api.Data;
using ApiWorkbench.Api.Infrastructure;
using ApiWorkbench.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiWorkbench.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/endpoints")]
public sealed class EndpointsController(
    AppDbContext db,
    IPermissionService permissions,
    IDtoGeneratorService dtoGenerator) : ControllerBase
{
    [HttpPut("{id:int}/user-tags")]
    public async Task<ActionResult<EndpointResponse>> PutUserTags(
        int id,
        [FromBody] UserTagsRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var endpoint = await db.Endpoints.Include(e => e.Service).FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (endpoint is null)
        {
            return NotFound();
        }

        var access = await permissions.GetAccessAsync(userId.Value, cancellationToken);
        if (!access.CanSeeService(endpoint.ServiceId))
        {
            return NotFound();
        }

        var tags = (request.Tags ?? [])
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .Select(t => t.Length > 40 ? t[..40] : t)
            .ToList();

        endpoint.UserTags = tags;
        await db.SaveChangesAsync(cancellationToken);
        var snapshot = await db.ContractSnapshots.AsNoTracking()
            .Where(s => s.ServiceId == endpoint.ServiceId && s.Module == (endpoint.Module ?? string.Empty))
            .OrderByDescending(s => s.FetchedAt)
            .ThenByDescending(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return Ok(ServicesController.MapEndpoint(endpoint, snapshot?.RawJson));
    }

    [HttpGet("{id:int}/dto")]
    public async Task<IActionResult> DownloadDto(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var access = await permissions.GetAccessAsync(userId.Value, cancellationToken);
        if (!access.CanGenerateDto)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Нет права генерировать DTO." });
        }

        var endpoint = await db.Endpoints.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (endpoint is null || !access.CanSeeService(endpoint.ServiceId))
        {
            return NotFound();
        }

        try
        {
            var (fileName, content) = await dtoGenerator.GenerateAsync(id, cancellationToken);
            return File(System.Text.Encoding.UTF8.GetBytes(content), "text/plain; charset=utf-8", fileName);
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
