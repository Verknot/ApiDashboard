using System.Text.Json;
using ApiWorkbench.Api.Contracts;
using ApiWorkbench.Api.Data;
using ApiWorkbench.Api.Domain;
using ApiWorkbench.Api.Infrastructure;
using ApiWorkbench.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiWorkbench.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/templates")]
public sealed class TemplatesController(AppDbContext db, IPermissionService permissions) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TemplateResponse>>> List(
        [FromQuery] int endpointId,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var rows = await db.RequestTemplates
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.EndpointId == endpointId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(Map).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<TemplateResponse>> Save(
        [FromBody] SaveTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var name = request.Name.Trim();
        if (name.Length is 0 or > 100)
        {
            return BadRequest(new { message = "Имя шаблона 1–100 символов." });
        }

        var endpoint = await db.Endpoints.AsNoTracking().FirstOrDefaultAsync(e => e.Id == request.EndpointId, cancellationToken);
        if (endpoint is null)
        {
            return NotFound(new { message = "Эндпоинт не найден." });
        }

        var access = await permissions.GetAccessAsync(userId.Value, cancellationToken);
        if (!access.CanSeeService(endpoint.ServiceId))
        {
            return NotFound();
        }

        var entity = new RequestTemplate
        {
            UserId = userId.Value,
            EndpointId = request.EndpointId,
            Name = name,
            TemplateBody = JsonDocument.Parse(request.TemplateBody.GetRawText()),
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.RequestTemplates.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(Map(entity));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var row = await db.RequestTemplates.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, cancellationToken);
        if (row is null)
        {
            return NotFound();
        }

        db.RequestTemplates.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static TemplateResponse Map(RequestTemplate t) =>
        new(t.Id, t.EndpointId, t.Name, t.TemplateBody.RootElement.Clone(), t.CreatedAt);
}
