using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;
using TrainingRegistry.Infrastructure;

namespace TrainingRegistry.Pages.Organizations;

public class IndexModel : ListPageModel
{
    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db) => _db = db;

    public record OrgRow(int Id, string FullName, string? ShortName, string? Inn, string? Phone,
        string? ContactName, int EmployeesCount, string? UpdatedBy, DateTime? UpdatedAt);

    public PagedResult<OrgRow> Result { get; private set; } = new();

    private static readonly Dictionary<string, Expression<Func<Organization, object?>>> Sorts = new()
    {
        ["name"] = o => o.FullName,
        ["inn"] = o => o.Inn,
        ["contact"] = o => o.ContactName,
        ["updatedBy"] = o => o.UpdatedBy,
        ["updated"] = o => o.UpdatedAt
    };

    private IQueryable<Organization> BuildQuery()
    {
        var query = _db.Organizations.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var s = Search.Trim().ToLower();
            query = query.Where(o =>
                o.FullName.ToLower().Contains(s) ||
                (o.ShortName != null && o.ShortName.ToLower().Contains(s)) ||
                (o.Inn != null && o.Inn.Contains(s)) ||
                (o.ContactName != null && o.ContactName.ToLower().Contains(s)));
        }

        return query.ApplySort(Sort, Desc, Sorts, "name");
    }

    public async Task OnGetAsync()
    {
        Result = await BuildQuery()
            .Select(o => new OrgRow(o.Id, o.FullName, o.ShortName, o.Inn, o.Phone, o.ContactName,
                o.Employees.Count(), o.UpdatedBy, o.UpdatedAt))
            .ToPagedAsync(PageNo, PageSize);
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var rows = await BuildQuery()
            .Select(o => new
            {
                o.FullName, o.ShortName, o.Inn, o.Kpp, o.Ogrn, o.LegalAddress, o.ActualAddress, o.Phone, o.Email,
                o.ContactName, o.ContactPosition, Employees = o.Employees.Count()
            })
            .ToListAsync();

        return XlsxFile("Организации", "Организации",
            new[] { "Полное наименование", "Краткое наименование", "ИНН", "КПП", "ОГРН", "Юридический адрес", "Фактический адрес",
                    "Телефон", "Электронная почта", "Контактное лицо", "Должность контактного лица", "Сотрудников" },
            rows.Select(r => new object?[] { r.FullName, r.ShortName, r.Inn, r.Kpp, r.Ogrn, r.LegalAddress, r.ActualAddress,
                r.Phone, r.Email, r.ContactName, r.ContactPosition, r.Employees }));
    }
}
