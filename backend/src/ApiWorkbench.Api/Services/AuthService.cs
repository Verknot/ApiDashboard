using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ApiWorkbench.Api.Configuration;
using ApiWorkbench.Api.Data;
using ApiWorkbench.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ApiWorkbench.Api.Services;

public sealed record AuthUser(
    int Id,
    string Email,
    string? DisplayName,
    bool IsFirstLogin,
    IReadOnlyList<string> Roles,
    IReadOnlyList<int> OwnedServiceIds);

public interface IAuthService
{
    Task<AuthUser?> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken);
    string CreateToken(AuthUser user);
    Task<AuthUser?> GetByIdAsync(int userId, CancellationToken cancellationToken);
    Task ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken cancellationToken);
}

public sealed class AuthService(AppDbContext db, IOptions<JwtOptions> jwtOptions) : IAuthService
{
    public async Task<AuthUser?> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var user = await db.Users
            .Include(u => u.RoleAssignments)
            .ThenInclude(a => a.Role)
            .FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken);

        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return null;
        }

        if (!user.IsActive)
        {
            throw new InvalidOperationException("Учётная запись заблокирована.");
        }

        return Map(user);
    }

    public string CreateToken(AuthUser user)
    {
        var options = jwtOptions.Value;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key));
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Email, user.Email)
        };

        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(options.ExpirationHours),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<AuthUser?> GetByIdAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .Include(u => u.RoleAssignments)
            .ThenInclude(a => a.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        return user is null ? null : Map(user);
    }

    public async Task ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
                   ?? throw new InvalidOperationException("Пользователь не найден.");

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
        {
            throw new InvalidOperationException("Неверный текущий пароль.");
        }

        if (newPassword.Length < 8)
        {
            throw new InvalidOperationException("Новый пароль должен быть не короче 8 символов.");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.IsFirstLogin = false;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static AuthUser Map(User user)
    {
        var roles = user.RoleAssignments
            .Select(a => a.Role.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var owned = user.RoleAssignments
            .Where(a => a.Role.Name == RoleNames.ServiceOwner && a.ServiceId.HasValue)
            .Select(a => a.ServiceId!.Value)
            .Distinct()
            .ToList();

        return new AuthUser(user.Id, user.Email, user.DisplayName, user.IsFirstLogin, roles, owned);
    }
}
