using ApiWorkbench.Api.Contracts;
using ApiWorkbench.Api.Data;
using ApiWorkbench.Api.Domain;
using ApiWorkbench.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiWorkbench.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/pins")]
public sealed class PinsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PinResponse>>> List(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var rows = await db.UserPins
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(Map).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<PinResponse>> Save(
        [FromBody] SavePinRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (!TryNormalize(request.Alias, request.Value, request.Comment, request.SourceKey, out var alias, out var value, out var comment, out var sourceKey, out var error))
        {
            return BadRequest(new { message = error });
        }

        if (request.ServiceId is int serviceId)
        {
            var exists = await db.Services.AsNoTracking().AnyAsync(s => s.Id == serviceId, cancellationToken);
            if (!exists)
            {
                return BadRequest(new { message = "Сервис не найден." });
            }
        }

        var now = DateTimeOffset.UtcNow;
        var existing = await db.UserPins
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Alias == alias, cancellationToken);

        if (existing is not null)
        {
            existing.Value = value;
            existing.Comment = comment;
            existing.SourceKey = sourceKey;
            existing.ServiceId = request.ServiceId;
            existing.UpdatedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            return Ok(Map(existing));
        }

        var entity = new UserPin
        {
            UserId = userId.Value,
            Alias = alias,
            Value = value,
            Comment = comment,
            SourceKey = sourceKey,
            ServiceId = request.ServiceId,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.UserPins.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(Map(entity));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PinResponse>> Update(
        int id,
        [FromBody] UpdatePinRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (!TryNormalize(request.Alias, request.Value, request.Comment, request.SourceKey, out var alias, out var value, out var comment, out var sourceKey, out var error))
        {
            return BadRequest(new { message = error });
        }

        var row = await db.UserPins.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, cancellationToken);
        if (row is null)
        {
            return NotFound();
        }

        var clash = await db.UserPins.AnyAsync(
            p => p.UserId == userId && p.Alias == alias && p.Id != id,
            cancellationToken);
        if (clash)
        {
            return Conflict(new { message = $"Alias «{alias}» уже занят." });
        }

        if (request.ServiceId is int serviceId)
        {
            var exists = await db.Services.AsNoTracking().AnyAsync(s => s.Id == serviceId, cancellationToken);
            if (!exists)
            {
                return BadRequest(new { message = "Сервис не найден." });
            }
        }

        row.Alias = alias;
        row.Value = value;
        row.Comment = comment;
        row.SourceKey = sourceKey;
        row.ServiceId = request.ServiceId;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(Map(row));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var row = await db.UserPins.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, cancellationToken);
        if (row is null)
        {
            return NotFound();
        }

        db.UserPins.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static bool TryNormalize(
        string? rawAlias,
        string? rawValue,
        string? rawComment,
        string? rawSourceKey,
        out string alias,
        out string value,
        out string? comment,
        out string? sourceKey,
        out string error)
    {
        alias = (rawAlias ?? string.Empty).Trim();
        value = (rawValue ?? string.Empty).Trim();
        comment = string.IsNullOrWhiteSpace(rawComment) ? null : rawComment.Trim();
        sourceKey = string.IsNullOrWhiteSpace(rawSourceKey) ? null : rawSourceKey.Trim();
        error = string.Empty;

        if (alias.Length is 0 or > 100)
        {
            error = "Alias: 1–100 символов.";
            return false;
        }

        if (value.Length is 0 or > 500)
        {
            error = "Value: 1–500 символов.";
            return false;
        }

        if (comment is { Length: > 500 })
        {
            error = "Comment: максимум 500 символов.";
            return false;
        }

        if (sourceKey is { Length: > 200 })
        {
            error = "SourceKey: максимум 200 символов.";
            return false;
        }

        return true;
    }

    private static PinResponse Map(UserPin pin) =>
        new(pin.Id, pin.Alias, pin.Value, pin.Comment, pin.SourceKey, pin.ServiceId, pin.CreatedAt, pin.UpdatedAt);
}
