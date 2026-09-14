using System.Text.Json;
using ApiWorkbench.Api.Contracts;
using ApiWorkbench.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ApiWorkbench.Api.Services;

public interface IContractDiffService
{
    Task<IReadOnlyList<SnapshotListItem>> ListAsync(int serviceId, CancellationToken cancellationToken = default);
    Task<ContractDiffResponse> DiffAsync(int serviceId, int? fromId, int? toId, CancellationToken cancellationToken = default);
}

public sealed class ContractDiffService(AppDbContext db) : IContractDiffService
{
    private static readonly HashSet<string> HttpMethods =
        new(StringComparer.OrdinalIgnoreCase) { "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS" };

    public async Task<IReadOnlyList<SnapshotListItem>> ListAsync(int serviceId, CancellationToken cancellationToken = default)
    {
        return await db.ContractSnapshots
            .AsNoTracking()
            .Where(s => s.ServiceId == serviceId)
            .OrderByDescending(s => s.FetchedAt)
            .Select(s => new SnapshotListItem(s.Id, s.FetchedAt, s.Module ?? string.Empty))
            .Take(30)
            .ToListAsync(cancellationToken);
    }

    public async Task<ContractDiffResponse> DiffAsync(
        int serviceId,
        int? fromId,
        int? toId,
        CancellationToken cancellationToken = default)
    {
        var service = await db.Services.AsNoTracking()
            .Where(s => s.Id == serviceId)
            .Select(s => new { s.Id, s.Name })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Сервис не найден.");

        var snapshots = await db.ContractSnapshots
            .AsNoTracking()
            .Where(s => s.ServiceId == serviceId)
            .OrderByDescending(s => s.FetchedAt)
            .ThenByDescending(s => s.Id)
            .Take(30)
            .ToListAsync(cancellationToken);

        if (snapshots.Count == 0)
        {
            throw new InvalidOperationException("Снимков контракта ещё нет. Сначала «Обновить все».");
        }

        var to = toId is int tid
            ? snapshots.FirstOrDefault(s => s.Id == tid)
            : snapshots[0];
        if (to is null)
        {
            throw new InvalidOperationException("Снимок «к» не найден.");
        }

        var module = to.Module ?? string.Empty;
        var from = fromId is int fid
            ? snapshots.FirstOrDefault(s => s.Id == fid)
            : snapshots.FirstOrDefault(s => s.Id != to.Id && (s.Module ?? string.Empty) == module);

        if (from is null)
        {
            return new ContractDiffResponse(
                service.Id,
                service.Name,
                null,
                to.FetchedAt,
                null,
                to.Id,
                [],
                [],
                []);
        }

        var counts = CompareDocuments(from.RawJson, to.RawJson);
        return new ContractDiffResponse(
            service.Id,
            service.Name,
            from.FetchedAt,
            to.FetchedAt,
            from.Id,
            to.Id,
            counts.Added,
            counts.Removed,
            counts.Changed);
    }

    public static ContractDiffCounts CompareDocuments(JsonDocument from, JsonDocument to) =>
        Compare(from.RootElement, to.RootElement);

    public static ContractDiffCounts Compare(JsonElement from, JsonElement to)
    {
        var left = Extract(from);
        var right = Extract(to);
        var added = new List<OperationDiff>();
        var removed = new List<OperationDiff>();
        var changed = new List<OperationDiff>();

        foreach (var (key, node) in right)
        {
            if (!left.TryGetValue(key, out var previous))
            {
                added.Add(ToDiff(key, node, "added", "Новый эндпоинт"));
                continue;
            }

            var reasons = DiffReasons(previous, node);
            if (reasons.Count > 0)
            {
                changed.Add(ToDiff(key, node, "changed", string.Join(", ", reasons)));
            }
        }

        foreach (var (key, node) in left.Where(pair => !right.ContainsKey(pair.Key)))
        {
            removed.Add(ToDiff(key, node, "removed", "Удалён из контракта"));
        }

        return new ContractDiffCounts(added, removed, changed);
    }

    private static Dictionary<string, JsonElement> Extract(JsonElement root)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("paths", out var paths)
            || paths.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var path in paths.EnumerateObject())
        {
            if (path.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var operation in path.Value.EnumerateObject())
            {
                if (!HttpMethods.Contains(operation.Name))
                {
                    continue;
                }

                result[$"{operation.Name.ToUpperInvariant()} {path.Name}"] = operation.Value.Clone();
            }
        }

        return result;
    }

    private static List<string> DiffReasons(JsonElement from, JsonElement to)
    {
        var reasons = new List<string>();
        if (!TextEquals(from, to, "summary") || !TextEquals(from, to, "description") || !TextEquals(from, to, "operationId"))
        {
            reasons.Add("описание");
        }

        if (!RawEquals(Pick(from, "requestBody"), Pick(to, "requestBody")))
        {
            reasons.Add("request");
        }

        if (!RawEquals(Pick(from, "responses"), Pick(to, "responses")))
        {
            reasons.Add("response");
        }

        if (!RawEquals(Pick(from, "parameters"), Pick(to, "parameters")))
        {
            reasons.Add("параметры");
        }

        if (!RawEquals(Pick(from, "tags"), Pick(to, "tags")))
        {
            reasons.Add("теги");
        }

        return reasons;
    }

    private static bool TextEquals(JsonElement from, JsonElement to, string name)
    {
        var a = Pick(from, name);
        var b = Pick(to, name);
        var left = a?.ValueKind == JsonValueKind.String ? a.Value.GetString() : a?.GetRawText();
        var right = b?.ValueKind == JsonValueKind.String ? b.Value.GetString() : b?.GetRawText();
        return string.Equals(left, right, StringComparison.Ordinal);
    }

    private static bool RawEquals(JsonElement? from, JsonElement? to)
    {
        if (from is null && to is null)
        {
            return true;
        }

        if (from is null || to is null)
        {
            return false;
        }

        return string.Equals(from.Value.GetRawText(), to.Value.GetRawText(), StringComparison.Ordinal);
    }

    private static JsonElement? Pick(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : null;

    private static OperationDiff ToDiff(string key, JsonElement node, string kind, string detail)
    {
        var space = key.IndexOf(' ');
        var method = space > 0 ? key[..space] : key;
        var path = space > 0 ? key[(space + 1)..] : key;
        var summary = Pick(node, "summary") is { ValueKind: JsonValueKind.String } s ? s.GetString() : null;
        return new OperationDiff(method, path, kind, summary, detail);
    }
}

public sealed record ContractDiffCounts(
    IReadOnlyList<OperationDiff> Added,
    IReadOnlyList<OperationDiff> Removed,
    IReadOnlyList<OperationDiff> Changed);
