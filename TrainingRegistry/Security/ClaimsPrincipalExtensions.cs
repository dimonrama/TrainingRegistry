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
}
