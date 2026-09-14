using ApiWorkbench.Api.Contracts;
using ApiWorkbench.Api.Domain;
using ApiWorkbench.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiWorkbench.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Admin)]
[Route("api/admin")]
public sealed class AdminController(
    ICatalogSyncService catalogSync,
    ILogger<AdminController> logger) : ControllerBase
{
    /// <summary>Текущий текст services.yaml.</summary>
    [HttpGet("config")]
    public async Task<ActionResult<ConfigFileResponse>> GetConfig(CancellationToken cancellationToken)
    {
        try
        {
            var file = await catalogSync.GetYamlAsync(cancellationToken);
            return Ok(new ConfigFileResponse(file.Path, file.Writable, file.Content));
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Сохраняет YAML на диск и сразу синхронизирует каталог.</summary>
    [HttpPut("config")]
    public async Task<ActionResult<ReloadConfigResponse>> SaveConfig(
        [FromBody] SaveConfigRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await catalogSync.SaveYamlAsync(request.Content, cancellationToken);
            logger.LogInformation(
                "Конфигурация сохранена из UI: upserted={Upserted}, deactivated={Deactivated}",
                result.Upserted,
                result.Deactivated);
            return Ok(new ReloadConfigResponse(result.Upserted, result.Deactivated, result.Warnings));
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status409Conflict, new { message = ex.Message });
        }
        catch (IOException ex)
        {
            return StatusCode(StatusCodes.Status409Conflict, new { message = ex.Message });
        }
    }

    /// <summary>Перечитывает services.yaml и синхронизирует каталог (F-CFG-3, F-ADMIN-6).</summary>
    [HttpPost("config/reload")]
    public async Task<ActionResult<ReloadConfigResponse>> Reload(CancellationToken cancellationToken)
    {
        try
        {
            var result = await catalogSync.ReloadFromYamlAsync(cancellationToken);
            logger.LogInformation(
                "Конфигурация обновлена: upserted={Upserted}, deactivated={Deactivated}, warnings={WarningCount}",
                result.Upserted,
                result.Deactivated,
                result.Warnings.Count);
            return Ok(new ReloadConfigResponse(result.Upserted, result.Deactivated, result.Warnings));
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
