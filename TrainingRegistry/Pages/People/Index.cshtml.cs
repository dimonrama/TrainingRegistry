using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;
using TrainingRegistry.Infrastructure;
using TrainingRegistry.Security;

namespace TrainingRegistry.Pages.People;

public class IndexModel : ListPageModel
{
    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int? OrganizationId { get; set; }
    [BindProperty(SupportsGet = true)] public bool IndividualsOnly { get; set; }
    [BindProperty(SupportsGet = true)] public bool ShowDismissed { get; set; }
    [BindProperty(SupportsGet = true)] public EducationLevel? Level { get; set; }
    [BindProperty(SupportsGet = true)] public int? ProgramId { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true)] public string? Certificate { get; set; }
    [BindProperty(SupportsGet = true)] public FundingSource? Funding { get; set; }

    public record PersonRow(int Id, string LastName, string FirstName, string? MiddleName, int? OrganizationId,
        string? OrganizationName, string? Position, PersonStatus Status, string? UpdatedBy, DateTime? UpdatedAt)
    {
        public string FullName => Fmt.FullName(LastName, FirstName, MiddleName);
    }

    public PagedResult<PersonRow> Result { get; private set; } = new();
    public List<SelectListItem> Organizations { get; private set; } = new();
    public List<SelectListItem> Programs { get; private set; } = new();
    public string? FilterError { get; private set; }

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
        values["ProgramId"] = ProgramId?.ToString() ?? "";
        values["From"] = From?.ToString("yyyy-MM-dd") ?? "";
        values["To"] = To?.ToString("yyyy-MM-dd") ?? "";
        values["Certificate"] = Certificate ?? "";
        values["Funding"] = Funding?.ToString() ?? "";
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

        // фильтры по обучению действуют вместе: один и тот же курс должен подходить по всем условиям
        if (ProgramId != null || From != null || To != null || !string.IsNullOrWhiteSpace(Certificate) || Funding != null)
        {
            var program = ProgramId;
            var from = From;
            var to = To;
            var cert = Certificate?.Trim().ToLower();
            var funding = Funding;
            query = query.Where(p => p.Enrollments.Any(e =>
                (program == null || e.Group!.ProgramId == program) &&
                (from == null || (e.Group!.EndDate ?? e.Group.StartDate) >= from) &&
                (to == null || e.Group!.StartDate <= to) &&
                (string.IsNullOrEmpty(cert) || (e.CertificateNumber != null && e.CertificateNumber.ToLower().Contains(cert))) &&
                (funding == null || (e.FundingSource ?? e.Group!.FundingSource) == funding)));
        }

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var s = Search.Trim().ToLower();
            var digits = Snils.Digits(Search);
            // СНИЛС ищем только тем, кто вправе видеть документы
            var searchSnils = digits.Length >= 4 && User.CanViewDocuments();
            query = query.Where(p =>
                (searchSnils && p.Snils != null && p.Snils.Replace("-", "").Replace(" ", "").Contains(digits)) ||
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
        if (From != null && To != null && From > To)
        {
            FilterError = "Дата начала периода позже даты окончания";
            From = To = null;
        }
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

        Programs = (await _db.Programs.AsNoTracking().OrderBy(p => p.Name).Select(p => new { p.Id, p.Name }).ToListAsync())
            .Select(p => new SelectListItem(p.Name, p.Id.ToString(), p.Id == ProgramId))
            .ToList();
    }

    private record ExportRow(string LastName, string FirstName, string? MiddleName, DateOnly? BirthDate, string? Snils,
        string? Phone, string? Email, string? Org, string? Position, PersonStatus Status, DateOnly? DismissalDate,
        List<EducationLevel> Levels);

    public async Task<IActionResult> OnGetExportAsync()
    {
        // личные данные в выгрузку попадают только у тех, кому разрешён просмотр документов
        var personal = User.CanViewDocuments();
        var rows = await BuildQuery()
            .Select(p => new ExportRow(p.LastName, p.FirstName, p.MiddleName, p.BirthDate, p.Snils, p.Phone, p.Email,
                p.Organization != null ? (p.Organization.ShortName ?? p.Organization.FullName) : null,
                p.Position, p.Status, p.DismissalDate, p.Educations.Select(e => e.Level).ToList()))
            .ToListAsync();

        var headers = new List<string> { "ФИО" };
        if (personal) headers.AddRange(new[] { "Дата рождения", "СНИЛС" });
        headers.AddRange(new[] { "Организация", "Должность" });
        if (personal) headers.AddRange(new[] { "Телефон", "Электронная почта" });
        headers.AddRange(new[] { "Образование", "Статус", "Дата увольнения" });

        object?[] Line(ExportRow r)
        {
            var cells = new List<object?> { Fmt.FullName(r.LastName, r.FirstName, r.MiddleName) };
            if (personal) { cells.Add(r.BirthDate); cells.Add(r.Snils); }
            cells.Add(r.Org ?? "Физическое лицо");
            cells.Add(r.Position);
            if (personal) { cells.Add(r.Phone); cells.Add(r.Email); }
            cells.Add(string.Join("; ", r.Levels.Distinct().Select(l => l.GetDisplayName())));
            cells.Add(r.Status.GetDisplayName());
            cells.Add(r.DismissalDate);
            return cells.ToArray();
        }

        return XlsxFile("Обучающиеся", "Обучающиеся", headers, rows.Select(r => Line(r)));
    }
}
