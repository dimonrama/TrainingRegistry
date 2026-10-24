using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;
using TrainingRegistry.Infrastructure;
using TrainingRegistry.Security;

namespace TrainingRegistry.Pages.History;

[Authorize(Policy = Policies.CanViewHistory)]
public class IndexModel : ListPageModel
{
    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db) => _db = db;

    public override int PageSize => 50;

    [BindProperty(SupportsGet = true)] public string? ActionName { get; set; }
    [BindProperty(SupportsGet = true)] public string? EntityType { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? DateFrom { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? DateTo { get; set; }

    public record AuditRow(long Id, DateTime At, string UserName, string Action, string EntityType, int EntityId,
        string? EntityTitle, string? Details);

    public PagedResult<AuditRow> Result { get; private set; } = new();

    public List<string> Actions { get; private set; } = new();
    public List<string> EntityTypes { get; private set; } = new();

    private static readonly Dictionary<string, Expression<Func<AuditLog, object?>>> Sorts = new()
    {
        ["at"] = a => a.At,
        ["user"] = a => a.UserName,
        ["action"] = a => a.Action,
        ["entity"] = a => a.EntityType
    };

    protected override Dictionary<string, string> BaseRouteValues()
    {
        var values = base.BaseRouteValues();
        values["ActionName"] = ActionName ?? "";
        values["EntityType"] = EntityType ?? "";
        values["DateFrom"] = DateFrom?.ToString("yyyy-MM-dd") ?? "";
        values["DateTo"] = DateTo?.ToString("yyyy-MM-dd") ?? "";
        return values;
    }

    private IQueryable<AuditLog> BuildQuery()
    {
        var query = _db.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(ActionName)) query = query.Where(a => a.Action == ActionName);
        if (!string.IsNullOrWhiteSpace(EntityType)) query = query.Where(a => a.EntityType == EntityType);
        if (DateFrom != null)
        {
            var from = Fmt.StartOfDayUtc(DateFrom.Value);
            query = query.Where(a => a.At >= from);
        }
        // "по дату" включает весь выбранный день
        if (DateTo != null)
        {
            var to = Fmt.StartOfDayUtc(DateTo.Value.AddDays(1));
            query = query.Where(a => a.At < to);
        }
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var s = Search.Trim().ToLower();
            query = query.Where(a => a.UserName.ToLower().Contains(s) ||
                                     (a.EntityTitle != null && a.EntityTitle.ToLower().Contains(s)));
        }

        var desc = string.IsNullOrEmpty(Sort) || Desc;
        return query.ApplySort(Sort, desc, Sorts, "at");
    }

    public async Task OnGetAsync()
    {
        Actions = await _db.AuditLogs.AsNoTracking().Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync();
        EntityTypes = await _db.AuditLogs.AsNoTracking().Select(a => a.EntityType).Distinct().OrderBy(a => a).ToListAsync();

        Result = await BuildQuery()
            .Select(a => new AuditRow(a.Id, a.At, a.UserName, a.Action, a.EntityType, a.EntityId, a.EntityTitle, a.Details))
            .ToPagedAsync(PageNo, PageSize);
    }

    public string? ObjectUrl(AuditRow row)
    {
        if (row.Action == "Удаление" || row.EntityId <= 0) return null;
        return row.EntityType switch
        {
            nameof(Organization) => Url.Page("/Organizations/Details", new { id = row.EntityId }),
            nameof(Person) => Url.Page("/People/Details", new { id = row.EntityId }),
            nameof(TrainingGroup) => Url.Page("/Groups/Details", new { id = row.EntityId }),
            nameof(Enrollment) => null,
            nameof(Contract) => Url.Page("/Contracts/Details", new { id = row.EntityId }),
            nameof(AppUser) => Url.Page("/Admin/UserEdit", new { id = row.EntityId }),
            _ => null
        };
    }
}
