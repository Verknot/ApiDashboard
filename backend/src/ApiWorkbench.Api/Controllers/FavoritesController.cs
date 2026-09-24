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
[Route("api/favorites")]
public sealed class FavoritesController(AppDbContext db, IPermissionService permissions) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FavoriteRequestResponse>>> List(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var access = await permissions.GetAccessAsync(userId.Value, cancellationToken);
        var rows = await db.UserFavoriteRequests
            .AsNoTracking()
            .Include(f => f.Endpoint)
            .ThenInclude(e => e.Service)
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(rows
            .Where(f => access.CanSeeService(f.Endpoint.ServiceId))
            .Select(f => Map(f, f.Endpoint))
            .ToList());
    }

    [HttpPost]
    public async Task<ActionResult<FavoriteRequestResponse>> Save(
        [FromBody] SaveFavoriteRequest request,
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
            return BadRequest(new { message = "Имя 1–100 символов." });
        }

        var endpoint = await db.Endpoints
            .AsNoTracking()
            .Include(e => e.Service)
            .FirstOrDefaultAsync(e => e.Id == request.EndpointId, cancellationToken);
        if (endpoint is null)
        {
            return NotFound(new { message = "Эндпоинт не найден." });
        }

        var access = await permissions.GetAccessAsync(userId.Value, cancellationToken);
        if (!access.CanSeeService(endpoint.ServiceId))
        {
            return NotFound();
        }

        // Do not assign AsNoTracking Endpoint/Service navigations — EF would treat them as inserts
        // and hit pk_services / pk_endpoints on SaveChanges.
        var entity = new UserFavoriteRequest
        {
            UserId = userId.Value,
            EndpointId = request.EndpointId,
            Name = name,
            ParamValues = ParseOptionalObject(request.ParamValues),
            RequestBody = ParseOptionalObject(request.RequestBody),
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.UserFavoriteRequests.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(Map(entity, endpoint));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var row = await db.UserFavoriteRequests.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId, cancellationToken);
        if (row is null)
        {
            return NotFound();
        }

        db.UserFavoriteRequests.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static FavoriteRequestResponse Map(UserFavoriteRequest f, EndpointEntity endpoint) =>
        new(
            f.Id,
            f.EndpointId,
            endpoint.ServiceId,
            endpoint.Service?.Name ?? string.Empty,
            endpoint.Method,
            endpoint.Path,
            endpoint.Module ?? string.Empty,
            f.Name,
            JsonDocs.ToElement(f.ParamValues),
            JsonDocs.ToElement(f.RequestBody),
            f.CreatedAt);

    private static JsonDocument? ParseOptionalObject(JsonElement? element)
    {
        if (element is null || element.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        if (element.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return JsonDocument.Parse(element.Value.GetRawText());
    }
}
