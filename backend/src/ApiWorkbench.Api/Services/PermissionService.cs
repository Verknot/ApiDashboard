using System.Security.Cryptography;
using System.Text;
using ApiWorkbench.Api.Data;
using ApiWorkbench.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ApiWorkbench.Api.Services;

public sealed class UserAccess
{
    public bool IsAdmin { get; init; }
    public bool CanSend { get; init; }
    public bool CanGenerateDto { get; init; }
    public IReadOnlySet<int>? RestrictedServiceIds { get; init; }

    public bool CanSeeService(int serviceId)
    {
        if (IsAdmin || RestrictedServiceIds is null)
        {
            return true;
        }

        return RestrictedServiceIds.Contains(serviceId);
    }
}

public interface IPermissionService
{
    Task<UserAccess> GetAccessAsync(int userId, CancellationToken cancellationToken = default);
    void InvalidateAccessCache();
}

public sealed class PermissionService(AppDbContext db, IMemoryCache cache) : IPermissionService
{
    private const string HashCacheKey = "roles-hash";
    private const string AccessCachePrefix = "access:";

    public async Task<UserAccess> GetAccessAsync(int userId, CancellationToken cancellationToken = default)
    {
        var hash = await GetRolesHashAsync(cancellationToken);
        var cacheKey = $"{AccessCachePrefix}{userId}:{hash}";

        if (cache.TryGetValue(cacheKey, out UserAccess? cached) && cached is not null)
        {
            return cached;
        }

        var assignments = await db.UserRoleAssignments
            .Include(a => a.Role)
            .Where(a => a.UserId == userId)
            .ToListAsync(cancellationToken);

        var names = assignments.Select(a => a.Role.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var isAdmin = names.Contains(RoleNames.Admin);
        var isOwnerOnly = names.Contains(RoleNames.ServiceOwner)
                          && !names.Contains(RoleNames.Tester)
                          && !names.Contains(RoleNames.Developer)
                          && !isAdmin;

        var access = new UserAccess
        {
            IsAdmin = isAdmin,
            CanSend = true,
            CanGenerateDto = isAdmin || names.Contains(RoleNames.Tester) || names.Contains(RoleNames.Developer) || names.Contains(RoleNames.ServiceOwner),
            RestrictedServiceIds = isOwnerOnly
                ? assignments
                    .Where(a => a.Role.Name == RoleNames.ServiceOwner && a.ServiceId.HasValue)
                    .Select(a => a.ServiceId!.Value)
                    .ToHashSet()
                : null
        };

        cache.Set(cacheKey, access, TimeSpan.FromSeconds(60));
        return access;
    }

    public void InvalidateAccessCache()
    {
        cache.Remove(HashCacheKey);
    }

    private async Task<string> GetRolesHashAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(HashCacheKey, out string? hash) && hash is not null)
        {
            return hash;
        }

        var rows = await db.UserRoleAssignments
            .AsNoTracking()
            .OrderBy(a => a.UserId)
            .ThenBy(a => a.RoleId)
            .ThenBy(a => a.ServiceId)
            .Select(a => new { a.UserId, a.RoleId, a.ServiceId })
            .ToListAsync(cancellationToken);

        var payload = string.Join(';', rows.Select(a => $"{a.UserId}:{a.RoleId}:{a.ServiceId ?? 0}"));
        var computed = string.IsNullOrEmpty(payload)
            ? "empty"
            : Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

        cache.Set(HashCacheKey, computed, TimeSpan.FromSeconds(60));
        return computed;
    }
}
