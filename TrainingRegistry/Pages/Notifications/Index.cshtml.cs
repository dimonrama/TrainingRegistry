using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;
using TrainingRegistry.Infrastructure;

namespace TrainingRegistry.Pages.Notifications;

public class IndexModel : ListPageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;
    [BindProperty(SupportsGet = true)] public int? OrganizationId { get; set; }
    [BindProperty(SupportsGet = true)] public int? ProgramId { get; set; }

    [BindProperty(SupportsGet = true)] public string Show { get; set; } = "due";
    public record NotificationRow(int PersonId, string PersonName, int? OrganizationId, string? OrganizationName,
        int ProgramId, string ProgramName, DateOnly CompletedDate, DateOnly NextTrainingDate, int ThresholdDays,
        int DaysLeft, Urgency Urgency)
    {
        public string DaysLeftText => RetrainingRules.DaysLeftText(DaysLeft);
    }
    public PagedResult<NotificationRow> Result { get; private set; } = new();
    public List<SelectListItem> Organizations { get; private set; } = new();
    public List<SelectListItem> Programs { get; private set; } = new();
    public int RedCount { get; private set; }
    public int YellowCount { get; private set; }
    protected override Dictionary<string, string> BaseRouteValues()
    {
        var values = base.BaseRouteValues();
        values["OrganizationId"] = OrganizationId?.ToString() ?? "";
        values["ProgramId"] = ProgramId?.ToString() ?? "";
        values["Show"] = Show;
        return values;
    }
    private async Task<List<NotificationRow>> GetSortedAsync()
    {
        var rows = await QueryAsync(Fmt.Today());
        RedCount = rows.Count(r => r.Urgency == Urgency.Red);
        YellowCount = rows.Count(r => r.Urgency == Urgency.Yellow);
        IEnumerable<NotificationRow> shown = Show switch
        {
            "all" => rows,
            "red" => rows.Where(r => r.Urgency == Urgency.Red),
            _ => rows.Where(r => r.Urgency != Urgency.Green)
        };
        return (Sort switch
        {
            "person" => Order(shown, r => r.PersonName),
            "org" => Order(shown, r => r.OrganizationName ?? ""),
            "program" => Order(shown, r => r.ProgramName),
            _ => Order(shown, r => r.NextTrainingDate)
        }).ToList();
    }
    public async Task OnGetAsync()
    {
        Organizations = await _db.Organizations.AsNoTracking().OrderBy(o => o.FullName)
            .Select(o => new SelectListItem(o.ShortName ?? o.FullName, o.Id.ToString())).ToListAsync();
        Programs = await _db.Programs.AsNoTracking().OrderBy(p => p.Name)
            .Select(p => new SelectListItem(p.Name, p.Id.ToString())).ToListAsync();
        var sorted = await GetSortedAsync();
        var total = sorted.Count;
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        var page = Math.Clamp(PageNo, 1, pages);

        Result = new PagedResult<NotificationRow>
        {
            Items = sorted.Skip((page - 1) * PageSize).Take(PageSize).ToList(),
            Page = page,
            PageSize = PageSize,
            TotalCount = total
        };
    }
    public async Task<IActionResult> OnGetExportAsync()
    {
        var sorted = await GetSortedAsync();

        return XlsxFile("Уведомления", "Повторное обучение",
            new[] { "Сотрудник", "Организация", "Программа", "Дата прохождения", "Следующее обучение", "Осталось дней", "Срочность" },
            sorted.Select(r => new object?[]
            {
                r.PersonName, r.OrganizationName ?? "Физическое лицо", r.ProgramName, r.CompletedDate, r.NextTrainingDate,
                r.DaysLeft, r.Urgency switch { Urgency.Red => "Срочно", Urgency.Yellow => "Скоро", _ => "В срок" }
            }));
    }
    private IOrderedEnumerable<NotificationRow> Order<TKey>(IEnumerable<NotificationRow> rows, Func<NotificationRow, TKey> key)
        => Desc ? rows.OrderByDescending(key) : rows.OrderBy(key);
    private async Task<List<NotificationRow>> QueryAsync(DateOnly today)
    {
        var query = RetrainingQuery.Pending(_db);
        if (OrganizationId != null) query = query.Where(e => e.Person!.OrganizationId == OrganizationId);
        if (ProgramId != null) query = query.Where(e => e.Group!.ProgramId == ProgramId);
        if (!string.IsNullOrWhiteSpace(Search))
        {
            foreach (var word in Search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var w = word.ToLower();
                query = query.Where(e => e.Person!.LastName.ToLower().Contains(w)
                                         || e.Person.FirstName.ToLower().Contains(w)
                                         || (e.Person.MiddleName != null && e.Person.MiddleName.ToLower().Contains(w))
                                         || e.Group!.Program!.Name.ToLower().Contains(w)
                                         || (e.Person.Organization != null && e.Person.Organization.FullName.ToLower().Contains(w)));
            }
        }
        var raw = await query.Select(e => new
        {
            e.PersonId,
            e.Person!.LastName, e.Person.FirstName, e.Person.MiddleName,
            e.Person.OrganizationId,
            Org = e.Person.Organization != null ? (e.Person.Organization.ShortName ?? e.Person.Organization.FullName) : null,
            ProgramId = e.Group!.ProgramId,
            ProgramName = e.Group.Program!.Name,
            Completed = e.CompletedDate,
            Next = e.NextTrainingDate,
            Notify = e.Group.Program.NotifyDaysBefore
        }).ToListAsync();
        // у каждой программы свой порог, поэтому остаток дней и цвет считаем уже тут
        return raw.Select(r =>
        {
            var threshold = r.Notify ?? TrainingProgram.DefaultNotifyDaysBefore;
            var next = r.Next!.Value;
            var left = next.DayNumber - today.DayNumber;
            return new NotificationRow(r.PersonId, Fmt.FullName(r.LastName, r.FirstName, r.MiddleName),
                r.OrganizationId, r.Org, r.ProgramId, r.ProgramName, r.Completed ?? next, next, threshold, left,
                RetrainingRules.GetUrgency(left, threshold));
        }).ToList();
    }
}