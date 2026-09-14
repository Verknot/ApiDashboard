using ApiWorkbench.Api.Contracts;
using ApiWorkbench.Api.Data;
using ApiWorkbench.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ApiWorkbench.Api.Services;

public interface IUserAdminService
{
    Task<IReadOnlyList<AdminUserResponse>> ListAsync(CancellationToken cancellationToken);
    Task<AdminUserResponse> CreateAsync(int actorUserId, CreateUserRequest request, CancellationToken cancellationToken);
    Task<AdminUserResponse> SetActiveAsync(int actorUserId, int userId, bool isActive, CancellationToken cancellationToken);
    Task<AdminUserResponse> SetRoleAsync(int actorUserId, int userId, string roleName, CancellationToken cancellationToken);
    Task<AdminUserResponse> ResetPasswordAsync(int actorUserId, int userId, string password, CancellationToken cancellationToken);
}

public sealed class UserAdminService(AppDbContext db, IPermissionService permissions) : IUserAdminService
{
    private static readonly HashSet<string> AssignableRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        RoleNames.Admin,
        RoleNames.Tester,
        RoleNames.Developer,
        RoleNames.Viewer
    };

    public async Task<IReadOnlyList<AdminUserResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var users = await db.Users
            .AsNoTracking()
            .Include(user => user.RoleAssignments)
            .ThenInclude(assignment => assignment.Role)
            .OrderBy(user => user.Email)
            .ToListAsync(cancellationToken);

        return users.Select(Map).ToList();
    }

    public async Task<AdminUserResponse> CreateAsync(
        int actorUserId,
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var roleName = RequireAssignableRole(request.Role);
        var password = RequirePassword(request.Password);
        var displayName = NormalizeDisplayName(request.DisplayName);

        if (await db.Users.AnyAsync(user => user.Email == email, cancellationToken))
        {
            throw new InvalidOperationException("Email already exists.");
        }

        var role = await RequireRoleAsync(roleName, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            Email = email,
            DisplayName = displayName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            IsActive = true,
            IsFirstLogin = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        db.UserRoleAssignments.Add(new UserRoleAssignment
        {
            UserId = user.Id,
            RoleId = role.Id,
            ServiceId = null,
            GrantedById = actorUserId,
            GrantedAt = now
        });
        db.RoleAuditLogs.Add(Audit(user.Id, actorUserId, "create", role.Id, now));
        await db.SaveChangesAsync(cancellationToken);
        permissions.InvalidateAccessCache();

        return await GetMappedAsync(user.Id, cancellationToken);
    }

    public async Task<AdminUserResponse> SetActiveAsync(
        int actorUserId,
        int userId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(userId, cancellationToken);
        if (user.Id == actorUserId && !isActive)
        {
            throw new InvalidOperationException("Cannot disable your own account.");
        }

        if (!isActive)
        {
            await EnsureNotLastAdminAsync(user.Id, cancellationToken);
        }

        user.IsActive = isActive;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        db.RoleAuditLogs.Add(Audit(user.Id, actorUserId, isActive ? "enable" : "disable", null, user.UpdatedAt));
        await db.SaveChangesAsync(cancellationToken);
        permissions.InvalidateAccessCache();

        return await GetMappedAsync(user.Id, cancellationToken);
    }

    public async Task<AdminUserResponse> SetRoleAsync(
        int actorUserId,
        int userId,
        string roleName,
        CancellationToken cancellationToken)
    {
        var normalized = RequireAssignableRole(roleName);
        var user = await RequireUserAsync(userId, cancellationToken);
        var role = await RequireRoleAsync(normalized, cancellationToken);
        var currentAdmin = user.RoleAssignments.Any(assignment =>
            assignment.ServiceId is null
            && assignment.Role.Name.Equals(RoleNames.Admin, StringComparison.OrdinalIgnoreCase));

        if (currentAdmin && !normalized.Equals(RoleNames.Admin, StringComparison.OrdinalIgnoreCase))
        {
            await EnsureNotLastAdminAsync(user.Id, cancellationToken);
        }

        var global = user.RoleAssignments.Where(assignment => assignment.ServiceId is null).ToList();
        db.UserRoleAssignments.RemoveRange(global);
        var now = DateTimeOffset.UtcNow;
        db.UserRoleAssignments.Add(new UserRoleAssignment
        {
            UserId = user.Id,
            RoleId = role.Id,
            ServiceId = null,
            GrantedById = actorUserId,
            GrantedAt = now
        });
        user.UpdatedAt = now;
        db.RoleAuditLogs.Add(Audit(user.Id, actorUserId, "grant", role.Id, now));
        await db.SaveChangesAsync(cancellationToken);
        permissions.InvalidateAccessCache();

        return await GetMappedAsync(user.Id, cancellationToken);
    }

    public async Task<AdminUserResponse> ResetPasswordAsync(
        int actorUserId,
        int userId,
        string password,
        CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(userId, cancellationToken);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(RequirePassword(password));
        user.IsFirstLogin = true;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        db.RoleAuditLogs.Add(Audit(user.Id, actorUserId, "reset_password", null, user.UpdatedAt));
        await db.SaveChangesAsync(cancellationToken);

        return await GetMappedAsync(user.Id, cancellationToken);
    }

    private async Task<User> RequireUserAsync(int userId, CancellationToken cancellationToken)
    {
        return await db.Users
                   .Include(user => user.RoleAssignments)
                   .ThenInclude(assignment => assignment.Role)
                   .FirstOrDefaultAsync(user => user.Id == userId, cancellationToken)
               ?? throw new KeyNotFoundException("User not found.");
    }

    private async Task<Role> RequireRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        return await db.Roles.SingleOrDefaultAsync(role => role.Name == roleName, cancellationToken)
               ?? throw new InvalidOperationException($"Unknown role '{roleName}'.");
    }

    private async Task<AdminUserResponse> GetMappedAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .AsNoTracking()
            .Include(item => item.RoleAssignments)
            .ThenInclude(assignment => assignment.Role)
            .SingleAsync(item => item.Id == userId, cancellationToken);
        return Map(user);
    }

    private async Task EnsureNotLastAdminAsync(int userId, CancellationToken cancellationToken)
    {
        var isAdmin = await db.UserRoleAssignments.AnyAsync(
            assignment => assignment.UserId == userId
                          && assignment.ServiceId == null
                          && assignment.Role.Name == RoleNames.Admin,
            cancellationToken);
        if (!isAdmin)
        {
            return;
        }

        var adminCount = await db.Users.CountAsync(
            user => user.IsActive
                    && user.RoleAssignments.Any(assignment =>
                        assignment.ServiceId == null && assignment.Role.Name == RoleNames.Admin),
            cancellationToken);
        if (adminCount <= 1)
        {
            throw new InvalidOperationException("Cannot remove the last active admin.");
        }
    }

    private static AdminUserResponse Map(User user)
    {
        var roles = user.RoleAssignments
            .Select(assignment => assignment.Role.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToList();
        return new AdminUserResponse(
            user.Id,
            user.Email,
            user.DisplayName,
            user.IsActive,
            user.IsFirstLogin,
            roles,
            user.CreatedAt);
    }

    private static RoleAuditLog Audit(int userId, int actorUserId, string action, int? roleId, DateTimeOffset at) =>
        new()
        {
            UserId = userId,
            ChangedById = actorUserId,
            Action = action,
            RoleId = roleId,
            ChangedAt = at
        };

    private static string NormalizeEmail(string? email)
    {
        var value = email?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value) || !value.Contains('@') || value.Contains(' '))
        {
            throw new InvalidOperationException("Enter a valid email.");
        }

        return value;
    }

    private static string? NormalizeDisplayName(string? displayName)
    {
        var value = displayName?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string RequirePassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            throw new InvalidOperationException("Password must be at least 8 characters.");
        }

        return password;
    }

    private static string RequireAssignableRole(string? role)
    {
        var value = role?.Trim() ?? string.Empty;
        if (value.Equals(RoleNames.ServiceOwner, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "service_owner is assigned per service. Use admin, tester, developer, or viewer.");
        }

        if (!AssignableRoles.Contains(value))
        {
            throw new InvalidOperationException("Role must be admin, tester, developer, or viewer.");
        }

        return AssignableRoles.First(name => name.Equals(value, StringComparison.OrdinalIgnoreCase));
    }
}
