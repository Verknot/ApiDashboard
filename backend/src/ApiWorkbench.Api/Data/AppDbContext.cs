using ApiWorkbench.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ApiWorkbench.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRoleAssignment> UserRoleAssignments => Set<UserRoleAssignment>();
    public DbSet<RoleAuditLog> RoleAuditLogs => Set<RoleAuditLog>();
    public DbSet<ServiceEntity> Services => Set<ServiceEntity>();
    public DbSet<ServiceRegion> ServiceRegions => Set<ServiceRegion>();
    public DbSet<ServiceUrl> ServiceUrls => Set<ServiceUrl>();
    public DbSet<ServiceTokenUrl> ServiceTokenUrls => Set<ServiceTokenUrl>();
    public DbSet<ServiceSwaggerSource> ServiceSwaggerSources => Set<ServiceSwaggerSource>();
    public DbSet<EndpointEntity> Endpoints => Set<EndpointEntity>();
    public DbSet<ContractSnapshot> ContractSnapshots => Set<ContractSnapshot>();
    public DbSet<RequestHistory> RequestHistory => Set<RequestHistory>();
    public DbSet<RequestTemplate> RequestTemplates => Set<RequestTemplate>();
    public DbSet<UserFavoriteRequest> UserFavoriteRequests => Set<UserFavoriteRequest>();
    public DbSet<UserPin> UserPins => Set<UserPin>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Email).HasMaxLength(255).IsRequired();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.DisplayName).HasMaxLength(255);
            entity.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.IsFirstLogin).HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<ServiceEntity>(entity =>
        {
            entity.ToTable("services");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Color).HasMaxLength(7);
            entity.Property(x => x.SwaggerUrl).HasMaxLength(500);
            entity.Property(x => x.SwaggerAuthType).HasMaxLength(20).HasDefaultValue("none");
            entity.Property(x => x.SwaggerVaultPath).HasMaxLength(500);
            entity.Property(x => x.SwaggerVaultUsernamePath).HasMaxLength(500);
            entity.Property(x => x.SwaggerVaultPasswordPath).HasMaxLength(500);
            entity.Property(x => x.SwaggerVaultBase64).HasDefaultValue(false);
            entity.Property(x => x.SwaggerBasicUsername).HasMaxLength(255);
            entity.Property(x => x.SwaggerBasicPassword).HasMaxLength(500);
            entity.Property(x => x.AuthType).HasMaxLength(20).HasDefaultValue("none");
            entity.Property(x => x.CertPath).HasMaxLength(500);
            entity.Property(x => x.CertBase64).HasColumnType("text");
            entity.Property(x => x.CertVaultPath).HasMaxLength(500);
            entity.Property(x => x.CertPassword).HasMaxLength(500);
            entity.Property(x => x.TokenUsername).HasMaxLength(255);
            entity.Property(x => x.TokenPassword).HasMaxLength(500);
            entity.Property(x => x.TokenVaultPath).HasMaxLength(500);
            entity.Property(x => x.TokenVaultUsernamePath).HasMaxLength(500);
            entity.Property(x => x.TokenVaultPasswordPath).HasMaxLength(500);
            entity.Property(x => x.TokenVaultBase64).HasDefaultValue(false);
            entity.Property(x => x.TokenBody).HasMaxLength(4000);
            entity.Property(x => x.TokenField).HasMaxLength(100).HasDefaultValue("accessToken");
            entity.Property(x => x.SplunkUrl).HasMaxLength(2000);
            entity.Property(x => x.DefaultRegion).HasMaxLength(50);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<ServiceRegion>(entity =>
        {
            entity.ToTable("service_regions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Label).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => new { x.ServiceId, x.Code }).IsUnique();
            entity.HasOne(x => x.Service)
                .WithMany(x => x.Regions)
                .HasForeignKey(x => x.ServiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ServiceUrl>(entity =>
        {
            entity.ToTable("service_urls");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Environment).HasMaxLength(20).IsRequired();
            entity.Property(x => x.RegionCode).HasMaxLength(50).HasDefaultValue(string.Empty);
            entity.Property(x => x.Module).HasMaxLength(100).HasDefaultValue(string.Empty);
            entity.Property(x => x.BaseUrl).HasMaxLength(500).IsRequired();
            entity.HasIndex(x => new { x.ServiceId, x.Module, x.Environment, x.RegionCode }).IsUnique();
            entity.HasOne(x => x.Service)
                .WithMany(x => x.Urls)
                .HasForeignKey(x => x.ServiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ServiceTokenUrl>(entity =>
        {
            entity.ToTable("service_token_urls");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Environment).HasMaxLength(20).IsRequired();
            entity.Property(x => x.RegionCode).HasMaxLength(50).HasDefaultValue(string.Empty);
            entity.Property(x => x.Module).HasMaxLength(100).HasDefaultValue(string.Empty);
            entity.Property(x => x.Url).HasMaxLength(500).IsRequired();
            entity.HasIndex(x => new { x.ServiceId, x.Module, x.Environment, x.RegionCode }).IsUnique();
            entity.HasOne(x => x.Service)
                .WithMany(x => x.TokenUrls)
                .HasForeignKey(x => x.ServiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ServiceSwaggerSource>(entity =>
        {
            entity.ToTable("service_swagger_sources");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).HasDefaultValue(string.Empty);
            entity.Property(x => x.Url).HasMaxLength(500).IsRequired();
            entity.Property(x => x.AuthType).HasMaxLength(20).HasDefaultValue("none");
            entity.Property(x => x.VaultPath).HasMaxLength(500);
            entity.Property(x => x.VaultUsernamePath).HasMaxLength(500);
            entity.Property(x => x.VaultPasswordPath).HasMaxLength(500);
            entity.Property(x => x.BasicUsername).HasMaxLength(255);
            entity.Property(x => x.BasicPassword).HasMaxLength(500);
            entity.Property(x => x.VaultBase64).HasDefaultValue(false);
            entity.Property(x => x.Insecure).HasDefaultValue(false);
            entity.Property(x => x.ApiAuthType).HasMaxLength(20);
            entity.Property(x => x.CertPath).HasMaxLength(500);
            entity.Property(x => x.CertBase64).HasColumnType("text");
            entity.Property(x => x.CertVaultPath).HasMaxLength(500);
            entity.Property(x => x.CertPassword).HasMaxLength(500);
            entity.Property(x => x.TokenField).HasMaxLength(100);
            entity.HasIndex(x => new { x.ServiceId, x.Name }).IsUnique();
            entity.HasOne(x => x.Service)
                .WithMany(x => x.SwaggerSources)
                .HasForeignKey(x => x.ServiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserRoleAssignment>(entity =>
        {
            entity.ToTable("user_role_assignments");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.UserId, x.RoleId, x.ServiceId })
                .IsUnique()
                .HasFilter("service_id IS NOT NULL")
                .HasDatabaseName("uq_user_role_service");
            entity.HasIndex(x => new { x.UserId, x.RoleId })
                .IsUnique()
                .HasFilter("service_id IS NULL")
                .HasDatabaseName("uq_user_role_global");
            entity.HasOne(x => x.User)
                .WithMany(x => x.RoleAssignments)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Role)
                .WithMany(x => x.Assignments)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Service)
                .WithMany()
                .HasForeignKey(x => x.ServiceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.GrantedBy)
                .WithMany()
                .HasForeignKey(x => x.GrantedById)
                .OnDelete(DeleteBehavior.SetNull);
            entity.Property(x => x.GrantedAt).HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<RoleAuditLog>(entity =>
        {
            entity.ToTable("role_audit_log");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(20).IsRequired();
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ChangedBy)
                .WithMany()
                .HasForeignKey(x => x.ChangedById)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Role)
                .WithMany()
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Service)
                .WithMany()
                .HasForeignKey(x => x.ServiceId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.Property(x => x.ChangedAt).HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<EndpointEntity>(entity =>
        {
            entity.ToTable("endpoints");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Path).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Method).HasMaxLength(10).IsRequired();
            entity.Property(x => x.Module).HasMaxLength(100).HasDefaultValue(string.Empty);
            entity.Property(x => x.OperationId).HasMaxLength(100);
            entity.Property(x => x.RequestSchema).HasColumnType("jsonb");
            entity.Property(x => x.ResponseSchema).HasColumnType("jsonb");
            entity.Property(x => x.Parameters).HasColumnType("jsonb");
            entity.Property(x => x.Tags).HasColumnType("text[]");
            entity.Property(x => x.UserTags).HasColumnType("text[]").HasDefaultValueSql("'{}'");
            entity.HasIndex(x => new { x.ServiceId, x.Module, x.Path, x.Method }).IsUnique();
            entity.HasOne(x => x.Service)
                .WithMany(x => x.Endpoints)
                .HasForeignKey(x => x.ServiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContractSnapshot>(entity =>
        {
            entity.ToTable("contract_snapshots");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RawJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.Module).HasMaxLength(100).HasDefaultValue(string.Empty);
            entity.Property(x => x.FetchedAt).HasDefaultValueSql("NOW()");
            entity.HasIndex(x => new { x.ServiceId, x.FetchedAt }).HasDatabaseName("idx_contract_snapshots_service_fetched");
            entity.HasOne(x => x.Service)
                .WithMany()
                .HasForeignKey(x => x.ServiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RequestHistory>(entity =>
        {
            entity.ToTable("request_history");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Environment).HasMaxLength(20);
            entity.Property(x => x.RegionCode).HasMaxLength(50).HasDefaultValue(string.Empty);
            entity.Property(x => x.Url).HasMaxLength(500);
            entity.Property(x => x.Method).HasMaxLength(10);
            entity.Property(x => x.RequestHeaders).HasColumnType("jsonb");
            entity.Property(x => x.RequestBody).HasColumnType("jsonb");
            entity.Property(x => x.ResponseHeaders).HasColumnType("jsonb");
            entity.Property(x => x.ResponseTruncated).HasDefaultValue(false);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
            entity.HasIndex(x => new { x.UserId, x.CreatedAt }).IsDescending(false, true)
                .HasDatabaseName("idx_request_history_user_created");
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Service)
                .WithMany()
                .HasForeignKey(x => x.ServiceId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Endpoint)
                .WithMany()
                .HasForeignKey(x => x.EndpointId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RequestTemplate>(entity =>
        {
            entity.ToTable("request_templates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.TemplateBody).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.ParamValues).HasColumnType("jsonb");
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Endpoint)
                .WithMany()
                .HasForeignKey(x => x.EndpointId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserFavoriteRequest>(entity =>
        {
            entity.ToTable("user_favorite_requests");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ParamValues).HasColumnType("jsonb");
            entity.Property(x => x.RequestBody).HasColumnType("jsonb");
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
            entity.HasIndex(x => new { x.UserId, x.CreatedAt }).IsDescending(false, true)
                .HasDatabaseName("ix_user_favorite_requests_user_created");
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Endpoint)
                .WithMany()
                .HasForeignKey(x => x.EndpointId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserPin>(entity =>
        {
            entity.ToTable("user_pins");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Alias).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Value).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Comment).HasMaxLength(500);
            entity.Property(x => x.SourceKey).HasMaxLength(200);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("NOW()");
            entity.HasIndex(x => new { x.UserId, x.Alias }).IsUnique()
                .HasDatabaseName("ix_user_pins_user_alias");
            entity.HasIndex(x => new { x.UserId, x.UpdatedAt }).IsDescending(false, true)
                .HasDatabaseName("ix_user_pins_user_updated");
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Service)
                .WithMany()
                .HasForeignKey(x => x.ServiceId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
