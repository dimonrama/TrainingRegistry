using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;
using TrainingRegistry.Infrastructure;

namespace TrainingRegistry.Pages.Programs;

public class IndexModel : ListPageModel
{
    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public string Show { get; set; } = "active";

    public record ProgramRow(int Id, string Name, decimal Price, RequiredEducation RequiredEducation,
        Recurrence Recurrence, int? CustomMonths, int? Duration, DurationUnit DurationUnit, ProgramStatus Status,
        string? UpdatedBy, DateTime? UpdatedAt)
    {
        public string RecurrenceText => Fmt.RecurrenceLabel(Recurrence, CustomMonths);

        public string DurationText => Duration == null ? "—" : $"{Duration} {DurationUnit.GetDisplayName()}";
    }

    public PagedResult<ProgramRow> Result { get; private set; } = new();

    private static readonly Dictionary<string, Expression<Func<TrainingProgram, object?>>> Sorts = new()
    {
        ["name"] = p => p.Name,
        ["price"] = p => p.Price,
        ["education"] = p => p.RequiredEducation,
        ["recurrence"] = p => p.Recurrence,
        ["status"] = p => p.Status,
        ["updatedBy"] = p => p.UpdatedBy,
        ["updated"] = p => p.UpdatedAt
    };

    protected override Dictionary<string, string> BaseRouteValues()
    {
        var values = base.BaseRouteValues();
        values["Show"] = Show;
        return values;
    }

    private IQueryable<TrainingProgram> BuildQuery()
    {
        var query = _db.Programs.AsNoTracking();

        if (Show == "archived") query = query.Where(p => p.Status == ProgramStatus.Archived);
        else if (Show != "all") query = query.Where(p => p.Status == ProgramStatus.Active);

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var s = Search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(s) ||
                                     (p.Description != null && p.Description.ToLower().Contains(s)));
        }

        return query.ApplySort(Sort, Desc, Sorts, "name");
    }

    public async Task OnGetAsync()
    {
        Result = await BuildQuery()
            .Select(p => new ProgramRow(p.Id, p.Name, p.Price, p.RequiredEducation, p.Recurrence, p.CustomMonths,
                p.Duration, p.DurationUnit, p.Status, p.UpdatedBy, p.UpdatedAt))
            .ToPagedAsync(PageNo, PageSize);
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var rows = await BuildQuery()
            .Select(p => new ProgramRow(p.Id, p.Name, p.Price, p.RequiredEducation, p.Recurrence, p.CustomMonths,
                p.Duration, p.DurationUnit, p.Status, p.UpdatedBy, p.UpdatedAt))
            .ToListAsync();

        return XlsxFile("Программы", "Программы обучения",
            new[] { "Наименование", "Стоимость, руб.", "Требования к образованию", "Повторное обучение", "Срок обучения", "Статус" },
            rows.Select(r => new object?[] { r.Name, r.Price, r.RequiredEducation.GetDisplayName(), r.RecurrenceText,
                r.DurationText, r.Status.GetDisplayName() }));
    }
}
