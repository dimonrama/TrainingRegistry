using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;
using TrainingRegistry.Infrastructure;

namespace TrainingRegistry.Pages.People;

public class IndexModel : ListPageModel
{
    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int? OrganizationId { get; set; }
    [BindProperty(SupportsGet = true)] public bool IndividualsOnly { get; set; }
    [BindProperty(SupportsGet = true)] public bool ShowDismissed { get; set; }
    [BindProperty(SupportsGet = true)] public EducationLevel? Level { get; set; }

    public record PersonRow(int Id, string LastName, string FirstName, string? MiddleName, int? OrganizationId,
        string? OrganizationName, string? Position, PersonStatus Status, string? UpdatedBy, DateTime? UpdatedAt)
    {
        public string FullName => Fmt.FullName(LastName, FirstName, MiddleName);
    }

    public PagedResult<PersonRow> Result { get; private set; } = new();
    public List<SelectListItem> Organizations { get; private set; } = new();

    private static readonly Dictionary<string, Expression<Func<Person, object?>>> Sorts = new()
    {
        ["name"] = p => p.LastName,
        ["org"] = p => p.Organization!.FullName,
        ["position"] = p => p.Position,
        ["status"] = p => p.Status,
        ["updatedBy"] = p => p.UpdatedBy,
        ["updated"] = p => p.UpdatedAt
    };

    protected override Dictionary<string, string> BaseRouteValues()
    {
        var values = base.BaseRouteValues();
        values["OrganizationId"] = OrganizationId?.ToString() ?? "";
        values["IndividualsOnly"] = IndividualsOnly.ToString().ToLowerInvariant();
        values["ShowDismissed"] = ShowDismissed.ToString().ToLowerInvariant();
        values["Level"] = Level?.ToString() ?? "";
        return values;
    }

    private IQueryable<Person> BuildQuery()
    {
        var query = _db.People.AsNoTracking();

        // уволенных по умолчанию скрываем
        if (!ShowDismissed) query = query.Where(p => p.Status == PersonStatus.Active);

        if (IndividualsOnly) query = query.Where(p => p.OrganizationId == null);
        else if (OrganizationId != null) query = query.Where(p => p.OrganizationId == OrganizationId);

        if (Level != null) query = query.Where(p => p.Educations.Any(e => e.Level == Level));

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var s = Search.Trim().ToLower();
            query = query.Where(p =>
                p.LastName.ToLower().Contains(s) ||
                p.FirstName.ToLower().Contains(s) ||
                (p.MiddleName != null && p.MiddleName.ToLower().Contains(s)) ||
                (p.Phone != null && p.Phone.Contains(s)) ||
                (p.Email != null && p.Email.ToLower().Contains(s)) ||
                (p.Organization != null && p.Organization.FullName.ToLower().Contains(s)));
        }

        return query.ApplySort(Sort, Desc, Sorts, "name");
    }

    public async Task OnGetAsync()
    {
        Result = await BuildQuery()
            .Select(p => new PersonRow(p.Id, p.LastName, p.FirstName, p.MiddleName, p.OrganizationId,
                p.Organization != null ? (p.Organization.ShortName ?? p.Organization.FullName) : null,
                p.Position, p.Status, p.UpdatedBy, p.UpdatedAt))
            .ToPagedAsync(PageNo, PageSize);

        var orgs = await _db.Organizations.AsNoTracking()
            .OrderBy(o => o.FullName)
            .Select(o => new { o.Id, o.FullName, o.ShortName })
            .ToListAsync();
        Organizations = orgs
            .Select(o => new SelectListItem(o.ShortName ?? o.FullName, o.Id.ToString(), o.Id == OrganizationId))
            .ToList();
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var rows = await BuildQuery()
            .Select(p => new
            {
                p.LastName, p.FirstName, p.MiddleName, p.BirthDate, p.Phone, p.Email,
                Org = p.Organization != null ? (p.Organization.ShortName ?? p.Organization.FullName) : null,
                p.Position, p.Status, p.DismissalDate,
                Levels = p.Educations.Select(e => e.Level).ToList()
            })
            .ToListAsync();

        return XlsxFile("Сотрудники", "Сотрудники",
            new[] { "ФИО", "Дата рождения", "Организация", "Должность", "Телефон", "Электронная почта", "Образование", "Статус", "Дата увольнения" },
            rows.Select(r => new object?[]
            {
                Fmt.FullName(r.LastName, r.FirstName, r.MiddleName), r.BirthDate, r.Org ?? "Физическое лицо", r.Position,
                r.Phone, r.Email, string.Join("; ", r.Levels.Distinct().Select(l => l.GetDisplayName())),
                r.Status.GetDisplayName(), r.DismissalDate
            }));
    }
}
