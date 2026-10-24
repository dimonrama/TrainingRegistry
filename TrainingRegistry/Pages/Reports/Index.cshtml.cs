using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;
using TrainingRegistry.Infrastructure;
using TrainingRegistry.Security;

namespace TrainingRegistry.Pages.Reports;

// сводные отчёты, реестр выданных документов и выгрузка с выбором полей
[Authorize(Policy = Policies.CanReports)]
public class IndexModel : PageModel
{
    private const int PreviewLimit = 200;

    private readonly AppDbContext _db;
    private readonly ReportService _reports;

    public IndexModel(AppDbContext db, ReportService reports)
    {
        _db = db;
        _reports = reports;
    }

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public int? OrganizationId { get; set; }
    [BindProperty(SupportsGet = true)] public int? ProgramId { get; set; }
    [BindProperty(SupportsGet = true)] public int? GroupId { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true)] public string? Certificate { get; set; }
    [BindProperty(SupportsGet = true)] public FundingSource? Funding { get; set; }
    [BindProperty(SupportsGet = true)] public EnrollmentStatus? Status { get; set; }

    // выгрузка
    [BindProperty(SupportsGet = true)] public List<string> Fields { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string Format { get; set; } = "xlsx";
    [BindProperty(SupportsGet = true)] public bool Register { get; set; }

    public string? FilterError { get; private set; }
    public int Total { get; private set; }
    public List<ReportRow> Rows { get; private set; } = new();
    public List<SummaryLine> ByProgram { get; private set; } = new();
    public List<SummaryLine> ByOrganization { get; private set; } = new();
    public List<SummaryLine> ByMonth { get; private set; } = new();
    public List<SelectListItem> Organizations { get; private set; } = new();
    public List<SelectListItem> Programs { get; private set; } = new();
    public List<SelectListItem> GroupItems { get; private set; } = new();

    public bool CanSeePersonal => User.CanViewDocuments();
    public IEnumerable<ReportField> AvailableFields => ReportFields.All.Where(f => !f.Personal || CanSeePersonal);

    private ReportFilter BuildFilter() => new()
    {
        Search = Search, OrganizationId = OrganizationId, ProgramId = ProgramId, GroupId = GroupId,
        From = From, To = To, Certificate = Certificate, Funding = Funding, Status = Status,
        OnlyWithCertificate = Register
    };

    // параметры текущего отбора для ссылок выгрузки
    public IDictionary<string, string> FilterValues()
    {
        var values = new Dictionary<string, string>
        {
            ["Search"] = Search ?? "",
            ["OrganizationId"] = OrganizationId?.ToString() ?? "",
            ["ProgramId"] = ProgramId?.ToString() ?? "",
            ["GroupId"] = GroupId?.ToString() ?? "",
            ["From"] = From?.ToString("yyyy-MM-dd") ?? "",
            ["To"] = To?.ToString("yyyy-MM-dd") ?? "",
            ["Certificate"] = Certificate ?? "",
            ["Funding"] = Funding?.ToString() ?? "",
            ["Status"] = Status?.ToString() ?? ""
        };
        return values;
    }

    public async Task OnGetAsync()
    {
        var filter = BuildFilter();
        var error = filter.Validate();
        if (error != null)
        {
            FilterError = error;
            filter.From = filter.To = null;
            From = To = null;
        }

        var all = await _reports.LoadAsync(filter, maskPersonal: !CanSeePersonal);
        Total = all.Count;
        Rows = all.Take(PreviewLimit).ToList();
        ByProgram = ReportSummary.ByProgram(all);
        ByOrganization = ReportSummary.ByOrganization(all);
        ByMonth = ReportSummary.ByMonth(all);
        await LoadListsAsync();
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var filter = BuildFilter();
        var error = filter.Validate();
        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToPage(FilterValues());
        }

        var rows = await _reports.LoadAsync(filter, maskPersonal: !CanSeePersonal);
        if (rows.Count == 0)
        {
            TempData["Error"] = "По выбранным условиям нет данных для выгрузки";
            return RedirectToPage(FilterValues());
        }

        var fields = ReportFields.Select(Register ? ReportFields.RegisterKeys : Fields, CanSeePersonal);
        if (fields.Count == 0)
        {
            TempData["Error"] = "Отметьте хотя бы одно поле для выгрузки";
            return RedirectToPage(FilterValues());
        }

        var docx = string.Equals(Format, "docx", StringComparison.OrdinalIgnoreCase);
        var name = Register ? "Реестр_выданных_документов" : "Выгрузка_обучающихся";
        var file = docx
            ? _reports.Docx(rows, fields, Register ? "РЕЕСТР ВЫДАННЫХ ДОКУМЕНТОВ" : "ВЫГРУЗКА ПО ОБУЧАЮЩИМСЯ",
                PeriodText(), name)
            : _reports.Xlsx(rows, fields, name, numbered: Register);

        await _db.LogAsync("Выгрузка", "Report", 0, Register ? "Реестр выданных документов" : "Выгрузка по обучающимся",
            $"Строк: {rows.Count}, формат: {(docx ? "Word" : "Excel")}, поля: {string.Join(", ", fields.Select(f => f.Title))}");
        return File(file.Bytes, file.ContentType, file.FileName);
    }

    public async Task<IActionResult> OnGetSummaryAsync()
    {
        var filter = BuildFilter();
        if (filter.Validate() != null)
        {
            TempData["Error"] = filter.Validate();
            return RedirectToPage(FilterValues());
        }
        var rows = await _reports.LoadAsync(filter, maskPersonal: true);
        var file = _reports.SummaryXlsx(rows);
        await _db.LogAsync("Выгрузка", "Report", 0, "Сводный отчёт", $"Строк: {rows.Count}");
        return File(file.Bytes, file.ContentType, file.FileName);
    }

    private string? PeriodText()
        => From == null && To == null ? null : $"период: {Fmt.Date(From)} — {Fmt.Date(To)}";

    private async Task LoadListsAsync()
    {
        var orgs = await _db.Organizations.AsNoTracking().OrderBy(o => o.FullName)
            .Select(o => new { o.Id, o.FullName, o.ShortName }).ToListAsync();
        Organizations = orgs.Select(o => new SelectListItem(o.ShortName ?? o.FullName, o.Id.ToString(), o.Id == OrganizationId)).ToList();
        Programs = (await _db.Programs.AsNoTracking().OrderBy(p => p.Name).Select(p => new { p.Id, p.Name }).ToListAsync())
            .Select(p => new SelectListItem(p.Name, p.Id.ToString(), p.Id == ProgramId)).ToList();
        GroupItems = (await _db.Groups.AsNoTracking().OrderByDescending(g => g.StartDate).Take(300)
                .Select(g => new { g.Id, g.Name }).ToListAsync())
            .Select(g => new SelectListItem(g.Name, g.Id.ToString(), g.Id == GroupId)).ToList();
    }
}
