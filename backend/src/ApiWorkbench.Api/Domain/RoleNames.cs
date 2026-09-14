namespace ApiWorkbench.Api.Domain;

public static class RoleNames
{
    public const string Admin = "admin";
    public const string Tester = "tester";
    public const string Developer = "developer";
    public const string Viewer = "viewer";
    public const string ServiceOwner = "service_owner";

    public static readonly string[] All =
    [
        Admin, Tester, Developer, Viewer, ServiceOwner
    ];
}
