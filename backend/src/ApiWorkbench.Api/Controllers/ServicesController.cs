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
[Route("api/services")]
public sealed class ServicesController(
    AppDbContext db,
    IPermissionService permissions,
    IContractDiffService contractDiff,
    ITokenFetchService tokens) : ControllerBase
{
    /// <summary>Каталог сервисов с регионами и URL по средам. Фильтр по RBAC (F-RBAC-4 / F-CFG-6).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ServiceResponse>>> List(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var access = await permissions.GetAccessAsync(userId.Value, cancellationToken);
        var services = await db.Services
            .AsNoTracking()
            .Where(s => s.IsActive)
            .Include(s => s.Regions)
            .Include(s => s.Urls)
            .Include(s => s.Endpoints)
            .Include(s => s.SwaggerSources)
            .Include(s => s.TokenUrls)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

        var visible = services.Where(s => access.CanSeeService(s.Id)).ToList();
        var ids = visible.Select(s => s.Id).ToList();
        var snapshots = ids.Count == 0
            ? []
            : await db.ContractSnapshots.AsNoTracking()
                .Where(s => ids.Contains(s.ServiceId))
                .ToListAsync(cancellationToken);
        var latest = snapshots
            .GroupBy(s => (s.ServiceId, Module: s.Module ?? string.Empty))
            .ToDictionary(g => g.Key, g => g.MaxBy(x => (x.FetchedAt, x.Id))!);

        var result = visible
            .Select(s => new ServiceResponse(
                s.Id,
                s.Name,
                s.Description,
                s.Color,
                s.AuthType,
                s.Proxy,
                s.IsRegional,
                s.DefaultRegion,
                DirectSendSupported: true,
                RequiresClientCertificate: s.AuthType == "certificate" && !s.Proxy,
                s.SplunkUrl,
                s.Regions
                    .OrderBy(r => r.SortOrder)
                    .Select(r => new RegionResponse(r.Code, r.Label, r.SortOrder))
                    .ToList(),
                s.Urls
                    .OrderBy(u => u.Module)
                    .ThenBy(u => u.Environment)
                    .ThenBy(u => u.RegionCode)
                    .Select(u => new ServiceUrlResponse(u.Environment, u.RegionCode, u.BaseUrl, u.Module ?? string.Empty))
                    .ToList(),
                s.Endpoints
                    .OrderBy(e => e.Module)
                    .ThenBy(e => e.Path)
                    .ThenBy(e => e.Method)
                    .Select(e => MapEndpoint(e, latest.GetValueOrDefault((s.Id, e.Module ?? string.Empty))?.RawJson))
                    .ToList(),
                s.Endpoints.Count,
                s.SwaggerSources
                    .OrderBy(x => x.SortOrder)
                    .Select(x => x.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToList(),
                s.AuthType == "token" && s.TokenUrls.Count > 0))
            .ToList();

        return Ok(result);
    }

    [HttpGet("{id:int}/snapshots")]
    public async Task<ActionResult<IReadOnlyList<SnapshotListItem>>> Snapshots(int id, CancellationToken cancellationToken)
    {
        if (await ForbidService(id, cancellationToken) is { } denial)
        {
            return denial;
        }

        return Ok(await contractDiff.ListAsync(id, cancellationToken));
    }

    [HttpGet("{id:int}/diff")]
    public async Task<ActionResult<ContractDiffResponse>> Diff(
        int id,
        [FromQuery] int? fromId,
        [FromQuery] int? toId,
        CancellationToken cancellationToken)
    {
        if (await ForbidService(id, cancellationToken) is { } denial)
        {
            return denial;
        }

        try
        {
            return Ok(await contractDiff.DiffAsync(id, fromId, toId, cancellationToken));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/token")]
    public async Task<ActionResult<FetchTokenResponse>> FetchToken(
        int id,
        [FromBody] FetchTokenRequest request,
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
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Нет права отправлять запросы." });
        }

        if (!access.CanSeeService(id))
        {
            return NotFound();
        }

        try
        {
            return Ok(await tokens.FetchAsync(id, request.Environment, request.RegionCode, cancellationToken));
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

    private async Task<ActionResult?> ForbidService(int serviceId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var access = await permissions.GetAccessAsync(userId.Value, cancellationToken);
        if (!access.CanSeeService(serviceId))
        {
            return NotFound();
        }

        return null;
    }

    internal static EndpointResponse MapEndpoint(Domain.EndpointEntity e, System.Text.Json.JsonDocument? snapshot = null)
    {
        var document = snapshot?.RootElement;
        var requestSchema = JsonDocs.ToElement(e.RequestSchema);
        return new(
            e.Id,
            e.Method,
            e.Path,
            e.Description,
            e.OperationId,
            e.Tags,
            e.UserTags,
            e.Module ?? string.Empty,
            OpenApiSchemaSupport.ResolveElement(requestSchema, document),
            JsonDocs.ToElement(e.ResponseSchema),
            OpenApiSchemaSupport.ExampleElement(requestSchema, document));
    }
}
