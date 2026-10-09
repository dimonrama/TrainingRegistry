namespace TrainingRegistry.Security;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Methodist = "Methodist";
}

public static class Policies
{
    public const string AdminOnly = "AdminOnly";
    public const string CanEditOrganizations = "CanEditOrganizations";
    public const string CanEditPeople = "CanEditPeople";
}

public static class AppClaims
{
    public const string DisplayName = "app:display";

    public const string Stamp = "app:stamp";
    public const string Permission = "app:perm";

    public const string PermOrganizations = "org";
    public const string PermPeople = "people";
}