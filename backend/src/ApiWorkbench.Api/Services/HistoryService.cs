using System.Text;
using System.Text.Json;
using ApiWorkbench.Api.Configuration;
using ApiWorkbench.Api.Contracts;
using ApiWorkbench.Api.Data;
using ApiWorkbench.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApiWorkbench.Api.Services;

public interface IHistoryService
{
    Task SaveAsync(int userId, SaveHistoryRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HistoryItemResponse>> ListAsync(
        int userId,
        int? serviceId,
        int? endpointId,
        int? status,
        string? query,
        int take,
        CancellationToken cancellationToken = default);
    Task<HistoryItemResponse?> GetAsync(int userId, int id, CancellationToken cancellationToken = default);
}

public sealed class HistoryService(
    AppDbContext db,
    IOptions<HistoryOptions> options,
    ILogger<HistoryService> logger) : IHistoryService
{
    public async Task SaveAsync(int userId, SaveHistoryRequest request, CancellationToken cancellationToken = default)
    {
        var maxBytes = Math.Max(1024, options.Value.MaxResponseBodyBytes);
        var body = request.ResponseBody ?? string.Empty;
        var truncated = Encoding.UTF8.GetByteCount(body) > maxBytes;
        if (truncated)
        {
            body = TruncateUtf8(body, maxBytes);
        }

        var entity = new RequestHistory
        {
            UserId = userId,
            ServiceId = request.ServiceId,
            EndpointId = request.EndpointId,
            Environment = request.Environment,
            RegionCode = request.RegionCode ?? string.Empty,
            Url = request.Url,
            Method = request.Method,
            RequestHeaders = JsonDocs.ParseOrNull(request.RequestHeaders),
            RequestBody = JsonDocs.ParseOrNull(request.RequestBody),
            ResponseStatus = request.ResponseStatus,
            ResponseBody = body,
            ResponseHeaders = JsonDocs.ParseOrNull(request.ResponseHeaders),
            ResponseTruncated = truncated,
            ResponseTimeMs = request.ResponseTimeMs,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.RequestHistory.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "История {UserId}: {Method} {Url} status={Status}",
            userId,
            request.Method,
            request.Url,
            request.ResponseStatus);
    }

    public async Task<IReadOnlyList<HistoryItemResponse>> ListAsync(
        int userId,
        int? serviceId,
        int? endpointId,
        int? status,
        string? query,
        int take,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);
        var rows = db.RequestHistory
            .AsNoTracking()
            .Include(h => h.Service)
            .Include(h => h.Endpoint)
            .Where(h => h.UserId == userId);

        if (serviceId is int sid)
        {
            rows = rows.Where(h => h.ServiceId == sid);
        }

        if (endpointId is int eid)
        {
            rows = rows.Where(h => h.EndpointId == eid);
        }

        if (status is int code)
        {
            rows = rows.Where(h => h.ResponseStatus == code);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            rows = rows.Where(h =>
                (h.Url != null && h.Url.Contains(q))
                || (h.Method != null && h.Method.Contains(q))
                || (h.ResponseBody != null && h.ResponseBody.Contains(q))
                || (h.Service != null && h.Service.Name.Contains(q)));
        }

        var list = await rows
            .OrderByDescending(h => h.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return list.Select(Map).ToList();
    }

    public async Task<HistoryItemResponse?> GetAsync(int userId, int id, CancellationToken cancellationToken = default)
    {
        var row = await db.RequestHistory
            .AsNoTracking()
            .Include(h => h.Service)
            .Include(h => h.Endpoint)
            .FirstOrDefaultAsync(h => h.Id == id && h.UserId == userId, cancellationToken);
        return row is null ? null : Map(row);
    }

    private static HistoryItemResponse Map(RequestHistory h) =>
        new(
            h.Id,
            h.ServiceId,
            h.Service?.Name,
            h.Service?.Color,
            h.EndpointId,
            h.Environment,
            h.RegionCode,
            h.Url,
            h.Method,
            JsonDocs.ToElement(h.RequestHeaders),
            JsonDocs.ToElement(h.RequestBody),
            h.ResponseStatus,
            h.ResponseBody,
            h.ResponseTruncated,
            h.ResponseTimeMs,
            h.CreatedAt,
            JsonDocs.ToElement(h.ResponseHeaders));

    private static string TruncateUtf8(string value, int maxBytes)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length <= maxBytes)
        {
            return value;
        }

        var cut = maxBytes;
        while (cut > 0 && (bytes[cut] & 0xC0) == 0x80)
        {
            cut--;
        }

        return Encoding.UTF8.GetString(bytes, 0, cut);
    }
}
