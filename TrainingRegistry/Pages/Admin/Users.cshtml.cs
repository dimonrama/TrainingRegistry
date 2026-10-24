using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;
using TrainingRegistry.Infrastructure;

namespace TrainingRegistry.Pages.Admin;

public class UsersModel : ListPageModel
{
    private readonly AppDbContext _db;

    public UsersModel(AppDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public UserRole? Role { get; set; }

    [BindProperty(SupportsGet = true)] public string Show { get; set; } = "all";

    public record UserRow(int Id, string Login, string FullName, UserRole Role, bool IsBlocked,
        bool CanEditOrganizations, bool CanEditPeople, bool CanEnroll, bool CanViewDocuments, bool CanEditDocuments,
        bool CanReports, bool CanViewHistory, DateTime? LastLoginAt);

    public PagedResult<UserRow> Result { get; private set; } = new();

    private static readonly Dictionary<string, Expression<Func<AppUser, object?>>> Sorts = new()
    {
        ["login"] = u => u.Login,
        ["name"] = u => u.FullName,
        ["role"] = u => u.Role,
        ["status"] = u => u.IsBlocked,
        ["lastLogin"] = u => u.LastLoginAt
    };

    protected override Dictionary<string, string> BaseRouteValues()
    {
        var values = base.BaseRouteValues();
        values["Role"] = Role?.ToString() ?? "";
        values["Show"] = Show;
        return values;
    }

    public async Task OnGetAsync()
    {
        var query = _db.AppUsers.AsNoTracking();

        if (Role != null) query = query.Where(u => u.Role == Role);
        if (Show == "active") query = query.Where(u => !u.IsBlocked);
        else if (Show == "blocked") query = query.Where(u => u.IsBlocked);

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var s = Search.Trim().ToLower();
            query = query.Where(u => u.Login.ToLower().Contains(s) || u.FullName.ToLower().Contains(s));
        }

        query = query.ApplySort(Sort, Desc, Sorts, "login");

        Result = await query
            .Select(u => new UserRow(u.Id, u.Login, u.FullName, u.Role, u.IsBlocked, u.CanEditOrganizations,
                u.CanEditPeople, u.CanEnroll, u.CanViewDocuments, u.CanEditDocuments, u.CanReports, u.CanViewHistory,
                u.LastLoginAt))
            .ToPagedAsync(PageNo, PageSize);
    }
}
