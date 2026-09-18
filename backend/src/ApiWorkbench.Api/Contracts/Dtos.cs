using System.Text.Json;

namespace ApiWorkbench.Api.Contracts;

public sealed record LoginRequest(string Email, string Password);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record MeResponse(
    int Id,
    string Email,
    string? DisplayName,
    bool IsFirstLogin,
    IReadOnlyList<string> Roles,
    IReadOnlyList<int> OwnedServiceIds,
    bool CanSend,
    bool CanGenerateDto,
    bool IsAdmin);

public sealed record RegionResponse(string Code, string Label, int SortOrder);

public sealed record ServiceUrlResponse(string Environment, string RegionCode, string BaseUrl, string Module);

public sealed record ServiceModuleResponse(string Name, string AuthType, bool CanFetchToken);

public sealed record ServiceResponse(
    int Id,
    string Name,
    string? Description,
    string? Color,
    string AuthType,
    bool Proxy,
    bool IsRegional,
    string? DefaultRegion,
    bool DirectSendSupported,
    bool RequiresClientCertificate,
    string? SplunkUrl,
    IReadOnlyList<RegionResponse> Regions,
    IReadOnlyList<ServiceUrlResponse> Urls,
    IReadOnlyList<EndpointResponse> Endpoints,
    int EndpointCount,
    IReadOnlyList<ServiceModuleResponse> Modules,
    bool CanFetchToken);

public sealed record EndpointResponse(
    int Id,
    string Method,
    string Path,
    string? Description,
    string? OperationId,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> UserTags,
    string Module,
    JsonElement? RequestSchema,
    JsonElement? ResponseSchema,
    JsonElement? RequestExample,
    JsonElement? Parameters);

public sealed record SwaggerServiceRefreshResponse(
    string Service,
    string Status,
    int Endpoints,
    string? Error,
    int Added,
    int Removed,
    int Changed);

public sealed record SwaggerRefreshResponse(int Ok, int Failed, IReadOnlyList<SwaggerServiceRefreshResponse> Services);

public sealed record ReloadConfigResponse(int Upserted, int Deactivated, IReadOnlyList<string> Warnings);

public sealed record ConfigFileResponse(string Path, bool Writable, string Content);

public sealed record SaveConfigRequest(string Content);

public sealed record UserTagsRequest(IReadOnlyList<string> Tags);

public sealed record SnapshotListItem(int Id, DateTimeOffset FetchedAt, string Module);

public sealed record OperationDiff(string Method, string Path, string Kind, string? Summary, string Detail);

public sealed record ContractDiffResponse(
    int ServiceId,
    string ServiceName,
    DateTimeOffset? FromFetchedAt,
    DateTimeOffset? ToFetchedAt,
    int? FromSnapshotId,
    int ToSnapshotId,
    IReadOnlyList<OperationDiff> Added,
    IReadOnlyList<OperationDiff> Removed,
    IReadOnlyList<OperationDiff> Changed);

public sealed record SaveHistoryRequest(
    int? ServiceId,
    int? EndpointId,
    string? Environment,
    string? RegionCode,
    string? Url,
    string? Method,
    string? RequestHeaders,
    string? RequestBody,
    int? ResponseStatus,
    string? ResponseBody,
    int? ResponseTimeMs,
    string? ResponseHeaders);

public sealed record HistoryItemResponse(
    int Id,
    int? ServiceId,
    string? ServiceName,
    string? ServiceColor,
    int? EndpointId,
    string? Environment,
    string RegionCode,
    string? Url,
    string? Method,
    JsonElement? RequestHeaders,
    JsonElement? RequestBody,
    int? ResponseStatus,
    string? ResponseBody,
    bool ResponseTruncated,
    int? ResponseTimeMs,
    DateTimeOffset CreatedAt,
    JsonElement? ResponseHeaders);

public sealed record SaveTemplateRequest(
    int EndpointId,
    string Name,
    JsonElement TemplateBody,
    JsonElement? ParamValues = null);

public sealed record TemplateResponse(
    int Id,
    int EndpointId,
    string Name,
    JsonElement TemplateBody,
    JsonElement? ParamValues,
    DateTimeOffset CreatedAt);

public sealed record SaveFavoriteRequest(
    int EndpointId,
    string Name,
    JsonElement? ParamValues = null,
    JsonElement? RequestBody = null);

public sealed record FavoriteRequestResponse(
    int Id,
    int EndpointId,
    int ServiceId,
    string ServiceName,
    string Method,
    string Path,
    string Module,
    string Name,
    JsonElement? ParamValues,
    JsonElement? RequestBody,
    DateTimeOffset CreatedAt);

public sealed record SavePinRequest(
    string Alias,
    string Value,
    string? Comment = null,
    string? SourceKey = null,
    int? ServiceId = null);

public sealed record UpdatePinRequest(
    string Alias,
    string Value,
    string? Comment = null,
    string? SourceKey = null,
    int? ServiceId = null);

public sealed record PinResponse(
    int Id,
    string Alias,
    string Value,
    string? Comment,
    string? SourceKey,
    int? ServiceId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record FetchTokenRequest(string Environment, string? RegionCode, string? Module = null);

public sealed record FetchTokenResponse(string? AccessToken, string? RedirectUrl = null);

public sealed record ProxySendRequest(
    int? ServiceId,
    string Url,
    string Method,
    Dictionary<string, string>? Headers,
    string? Body,
    string? ClientCertPath = null,
    string? ClientCertPassword = null,
    string? ClientCertBase64 = null,
    string? ClientCertVault = null);

public sealed record ResponseHeaderItem(string Name, string Value);

public sealed record ProxySendResponse(
    int? Status,
    int TimeMs,
    string Body,
    string? Error,
    IReadOnlyList<ResponseHeaderItem> Headers);

public sealed record AdminUserResponse(
    int Id,
    string Email,
    string? DisplayName,
    bool IsActive,
    bool IsFirstLogin,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt);

public sealed record CreateUserRequest(string Email, string? DisplayName, string Password, string Role);

public sealed record SetUserActiveRequest(bool IsActive);

public sealed record SetUserRoleRequest(string Role);

public sealed record ResetPasswordRequest(string Password);
