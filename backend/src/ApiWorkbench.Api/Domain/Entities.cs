using System.Text.Json;

namespace ApiWorkbench.Api.Domain;

public sealed class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsFirstLogin { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<UserRoleAssignment> RoleAssignments { get; set; } = new List<UserRoleAssignment>();
}

public sealed class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<UserRoleAssignment> Assignments { get; set; } = new List<UserRoleAssignment>();
}

public sealed class UserRoleAssignment
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public int? ServiceId { get; set; }
    public int? GrantedById { get; set; }
    public DateTimeOffset GrantedAt { get; set; }

    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
    public ServiceEntity? Service { get; set; }
    public User? GrantedBy { get; set; }
}

public sealed class RoleAuditLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ChangedById { get; set; }
    public string Action { get; set; } = string.Empty;
    public int? RoleId { get; set; }
    public int? ServiceId { get; set; }
    public DateTimeOffset ChangedAt { get; set; }

    public User User { get; set; } = null!;
    public User ChangedBy { get; set; } = null!;
    public Role? Role { get; set; }
    public ServiceEntity? Service { get; set; }
}

public sealed class ServiceEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? SwaggerUrl { get; set; }
    public string SwaggerAuthType { get; set; } = "none";
    public string? SwaggerVaultPath { get; set; }
    public string? SwaggerVaultUsernamePath { get; set; }
    public string? SwaggerVaultPasswordPath { get; set; }
    public bool SwaggerVaultBase64 { get; set; }
    public string? SwaggerBasicUsername { get; set; }
    public string? SwaggerBasicPassword { get; set; }
    public string AuthType { get; set; } = "none";
    public string? CertPath { get; set; }
    public string? CertBase64 { get; set; }
    public string? CertVaultPath { get; set; }
    public string? CertPassword { get; set; }
    public string? TokenUsername { get; set; }
    public string? TokenPassword { get; set; }
    public string? TokenVaultPath { get; set; }
    public string? TokenVaultUsernamePath { get; set; }
    public string? TokenVaultPasswordPath { get; set; }
    public bool TokenVaultBase64 { get; set; }
    public string? TokenBody { get; set; }
    public string TokenField { get; set; } = "accessToken";
    public bool Proxy { get; set; }
    public string? SplunkUrl { get; set; }
    public bool IsRegional { get; set; }
    public string? DefaultRegion { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<ServiceRegion> Regions { get; set; } = new List<ServiceRegion>();
    public ICollection<ServiceUrl> Urls { get; set; } = new List<ServiceUrl>();
    public ICollection<ServiceTokenUrl> TokenUrls { get; set; } = new List<ServiceTokenUrl>();
    public ICollection<ServiceSwaggerSource> SwaggerSources { get; set; } = new List<ServiceSwaggerSource>();
    public ICollection<EndpointEntity> Endpoints { get; set; } = new List<EndpointEntity>();
}

public sealed class ServiceRegion
{
    public int Id { get; set; }
    public int ServiceId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public ServiceEntity Service { get; set; } = null!;
}

public sealed class ServiceUrl
{
    public int Id { get; set; }
    public int ServiceId { get; set; }
    public string Environment { get; set; } = string.Empty;
    public string RegionCode { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;

    public ServiceEntity Service { get; set; } = null!;
}

public sealed class ServiceTokenUrl
{
    public int Id { get; set; }
    public int ServiceId { get; set; }
    public string Environment { get; set; } = string.Empty;
    public string RegionCode { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;

    public ServiceEntity Service { get; set; } = null!;
}

public sealed class ServiceSwaggerSource
{
    public int Id { get; set; }
    public int ServiceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string Url { get; set; } = string.Empty;
    public string AuthType { get; set; } = "none";
    public string? VaultPath { get; set; }
    public string? VaultUsernamePath { get; set; }
    public string? VaultPasswordPath { get; set; }
    public bool VaultBase64 { get; set; }
    public string? BasicUsername { get; set; }
    public string? BasicPassword { get; set; }

    public ServiceEntity Service { get; set; } = null!;
}

public sealed class EndpointEntity
{
    public int Id { get; set; }
    public int ServiceId { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? OperationId { get; set; }
    public JsonDocument? RequestSchema { get; set; }
    public JsonDocument? ResponseSchema { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<string> UserTags { get; set; } = [];

    public ServiceEntity Service { get; set; } = null!;
}

public sealed class ContractSnapshot
{
    public int Id { get; set; }
    public int ServiceId { get; set; }
    public string Module { get; set; } = string.Empty;
    public DateTimeOffset FetchedAt { get; set; }
    public JsonDocument RawJson { get; set; } = null!;

    public ServiceEntity Service { get; set; } = null!;
}

public sealed class RequestHistory
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? ServiceId { get; set; }
    public int? EndpointId { get; set; }
    public string? Environment { get; set; }
    public string RegionCode { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Method { get; set; }
    public JsonDocument? RequestHeaders { get; set; }
    public JsonDocument? RequestBody { get; set; }
    public int? ResponseStatus { get; set; }
    public string? ResponseBody { get; set; }
    public JsonDocument? ResponseHeaders { get; set; }
    public bool ResponseTruncated { get; set; }
    public int? ResponseTimeMs { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public ServiceEntity? Service { get; set; }
    public EndpointEntity? Endpoint { get; set; }
}

public sealed class RequestTemplate
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int EndpointId { get; set; }
    public string Name { get; set; } = string.Empty;
    public JsonDocument TemplateBody { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public EndpointEntity Endpoint { get; set; } = null!;
}
