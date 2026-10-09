using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;
using TrainingRegistry.Infrastructure;

namespace TrainingRegistry.Pages.Groups;

public class IndexModel : ListPageModel
{
    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public GroupStatus? Status { get; set; }
    [BindProperty(SupportsGet = true)] public int? ProgramId { get; set; }

    public record GroupRow(int Id, string Name, int ProgramId, string ProgramName, DateOnly StartDate,
        DateOnly? EndDate, GroupStatus Status, int Members, string? UpdatedBy, DateTime? UpdatedAt);

    public PagedResult<GroupRow> Result { get; private set; } = new();
    public List<SelectListItem> Programs { get; private set; } = new();

    private static readonly Dictionary<string, Expression<Func<TrainingGroup, object?>>> Sorts = new()
    {
        ["name"] = g => g.Name,
        ["program"] = g => g.Program!.Name,
        ["start"] = g => g.StartDate,
        ["end"] = g => g.EndDate,
        ["status"] = g => g.Status,
        ["members"] = g => g.Enrollments.Count,
        ["updatedBy"] = g => g.UpdatedBy,
        ["updated"] = g => g.UpdatedAt
    };

    protected override Dictionary<string, string> BaseRouteValues()
    {
        var values = base.BaseRouteValues();
        values["Status"] = Status?.ToString() ?? "";
        values["ProgramId"] = ProgramId?.ToString() ?? "";
        return values;
    }

    private IQueryable<TrainingGroup> BuildQuery()
    {
        var query = _db.Groups.AsNoTracking();

        if (Status != null) query = query.Where(g => g.Status == Status);
        if (ProgramId != null) query = query.Where(g => g.ProgramId == ProgramId);

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var s = Search.Trim().ToLower();
            query = query.Where(g => g.Name.ToLower().Contains(s) || g.Program!.Name.ToLower().Contains(s));
        }

        return string.IsNullOrEmpty(Sort)
            ? query.OrderByDescending(g => g.StartDate).ThenBy(g => g.Id)
            : query.ApplySort(Sort, Desc, Sorts, "start");
    }

    public async Task OnGetAsync()
    {
        Programs = await _db.Programs.AsNoTracking().OrderBy(p => p.Name)
            .Select(p => new SelectListItem(p.Name, p.Id.ToString())).ToListAsync();

        Result = await BuildQuery()
            .Select(g => new GroupRow(g.Id, g.Name, g.ProgramId, g.Program!.Name, g.StartDate, g.EndDate, g.Status,
                g.Enrollments.Count, g.UpdatedBy, g.UpdatedAt))
            .ToPagedAsync(PageNo, PageSize);
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var rows = await BuildQuery()
            .Select(g => new GroupRow(g.Id, g.Name, g.ProgramId, g.Program!.Name, g.StartDate, g.EndDate, g.Status,
                g.Enrollments.Count, g.UpdatedBy, g.UpdatedAt))
            .ToListAsync();

        return XlsxFile("Группы", "Группы обучения",
            new[] { "Группа", "Программа", "Дата начала", "Дата окончания", "Статус", "Обучающихся" },
            rows.Select(r => new object?[] { r.Name, r.ProgramName, r.StartDate, r.EndDate, r.Status.GetDisplayName(), r.Members }));
    }
}
