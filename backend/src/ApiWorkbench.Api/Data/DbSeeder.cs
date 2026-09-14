using ApiWorkbench.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ApiWorkbench.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IConfiguration configuration, CancellationToken cancellationToken)
    {
        await SeedRolesAsync(db, cancellationToken);
        await SeedAdminAsync(db, configuration, cancellationToken);
    }

    private static async Task SeedRolesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var descriptions = new Dictionary<string, string>
        {
            [RoleNames.Admin] = "Полный доступ к пульту",
            [RoleNames.Tester] = "Документация, запросы, DTO, своя история",
            [RoleNames.Developer] = "Документация, запросы, DTO, своя история",
            [RoleNames.Viewer] = "Только просмотр документации и своей истории",
            [RoleNames.ServiceOwner] = "Доступ только к назначенным сервисам"
        };

        var existing = await db.Roles.Select(r => r.Name).ToListAsync(cancellationToken);
        foreach (var name in RoleNames.All)
        {
            if (existing.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            db.Roles.Add(new Role
            {
                Name = name,
                Description = descriptions.GetValueOrDefault(name)
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedAdminAsync(AppDbContext db, IConfiguration configuration, CancellationToken cancellationToken)
    {
        var email = configuration["Seed:AdminEmail"] ?? "admin@local";
        var password = configuration["Seed:AdminPassword"] ?? "Admin123!";

        var admin = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (admin is null)
        {
            var now = DateTimeOffset.UtcNow;
            admin = new User
            {
                Email = email,
                DisplayName = "Administrator",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                IsActive = true,
                IsFirstLogin = false,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Users.Add(admin);
            await db.SaveChangesAsync(cancellationToken);
        }

        var adminRole = await db.Roles.SingleAsync(r => r.Name == RoleNames.Admin, cancellationToken);
        var hasRole = await db.UserRoleAssignments.AnyAsync(
            a => a.UserId == admin.Id && a.RoleId == adminRole.Id && a.ServiceId == null,
            cancellationToken);

        if (!hasRole)
        {
            db.UserRoleAssignments.Add(new UserRoleAssignment
            {
                UserId = admin.Id,
                RoleId = adminRole.Id,
                ServiceId = null,
                GrantedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
