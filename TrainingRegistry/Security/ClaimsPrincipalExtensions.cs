using System.Security.Claims;

namespace TrainingRegistry.Security;

public static class ClaimsPrincipalExtensions
{
    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(Roles.Admin);
    // админу можно всё, методисту - только если выдали право
    public static bool CanEditOrganizations(this ClaimsPrincipal user)
        => user.IsAdmin() || user.HasClaim(AppClaims.Permission, AppClaims.PermOrganizations);
    public static bool CanEditPeople(this ClaimsPrincipal user)
        => user.IsAdmin() || user.HasClaim(AppClaims.Permission, AppClaims.PermPeople);
    public static bool CanEnroll(this ClaimsPrincipal user)
        => user.IsAdmin() || user.HasClaim(AppClaims.Permission, AppClaims.PermEnroll);
    public static bool CanViewDocuments(this ClaimsPrincipal user)
        => user.IsAdmin() || user.HasClaim(AppClaims.Permission, AppClaims.PermViewDocuments)
           || user.HasClaim(AppClaims.Permission, AppClaims.PermEditDocuments);
    public static bool CanEditDocuments(this ClaimsPrincipal user)
        => user.IsAdmin() || user.HasClaim(AppClaims.Permission, AppClaims.PermEditDocuments);
    public static bool CanReports(this ClaimsPrincipal user)
        => user.IsAdmin() || user.HasClaim(AppClaims.Permission, AppClaims.PermReports);
    public static bool CanViewHistory(this ClaimsPrincipal user)
        => user.IsAdmin() || user.HasClaim(AppClaims.Permission, AppClaims.PermHistory);
}
