using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;
using TrainingRegistry.Infrastructure;

namespace TrainingRegistry.Pages.Contracts;

public class IndexModel : ListPageModel
{
    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int? Year { get; set; }

    public record ContractRow(int Id, string Number, DateTime CreatedAt, string? OrgName, string? LastName,
        string? FirstName, string? MiddleName, int GroupId, string GroupName, string ProgramName, int StudentsCount,
        decimal TotalPrice, string? UpdatedBy, DateTime? UpdatedAt)
    {
        public string CustomerName => OrgName ?? Fmt.FullName(LastName ?? "", FirstName ?? "", MiddleName);
    }

    public PagedResult<ContractRow> Result { get; private set; } = new();
    public List<int> Years { get; private set; } = new();

    private static readonly Dictionary<string, Expression<Func<Contract, object?>>> Sorts = new()
    {
        ["number"] = c => c.Number,
        ["date"] = c => c.CreatedAt,
        ["customer"] = c => c.CustomerOrganization != null ? c.CustomerOrganization.FullName : c.CustomerPerson!.LastName,
        ["group"] = c => c.Group!.Name,
        ["students"] = c => c.StudentsCount,
        ["total"] = c => c.TotalPrice,
        ["updatedBy"] = c => c.UpdatedBy,
        ["updated"] = c => c.UpdatedAt
    };

    protected override Dictionary<string, string> BaseRouteValues()
    {
        var values = base.BaseRouteValues();
        values["Year"] = Year?.ToString() ?? "";
        return values;
    }

    public async Task OnGetAsync()
    {
        Years = await _db.Contracts.AsNoTracking().Select(c => c.Year).Distinct().OrderByDescending(y => y).ToListAsync();

        var query = _db.Contracts.AsNoTracking();

        if (Year != null) query = query.Where(c => c.Year == Year);

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var s = Search.Trim().ToLower();
            query = query.Where(c => c.Number.ToLower().Contains(s)
                || c.Group!.Name.ToLower().Contains(s)
                || c.Group.Program!.Name.ToLower().Contains(s)
                || (c.CustomerOrganization != null && c.CustomerOrganization.FullName.ToLower().Contains(s))
                || (c.CustomerPerson != null && c.CustomerPerson.LastName.ToLower().Contains(s)));
        }

        query = string.IsNullOrEmpty(Sort)
            ? query.OrderByDescending(c => c.Year).ThenByDescending(c => c.Sequence)
            : query.ApplySort(Sort, Desc, Sorts, "number");

        Result = await query
            .Select(c => new ContractRow(c.Id, c.Number, c.CreatedAt,
                c.CustomerOrganization != null ? (c.CustomerOrganization.ShortName ?? c.CustomerOrganization.FullName) : null,
                c.CustomerPerson != null ? c.CustomerPerson.LastName : null,
                c.CustomerPerson != null ? c.CustomerPerson.FirstName : null,
                c.CustomerPerson != null ? c.CustomerPerson.MiddleName : null,
                c.GroupId, c.Group!.Name, c.Group.Program!.Name, c.StudentsCount, c.TotalPrice, c.UpdatedBy, c.UpdatedAt))
            .ToPagedAsync(PageNo, PageSize);
    }
}
