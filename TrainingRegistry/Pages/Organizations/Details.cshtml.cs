using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;
using TrainingRegistry.Security;

namespace TrainingRegistry.Pages.Organizations;

public class DetailsModel : PageModel
{
    private readonly AppDbContext _db;

    public DetailsModel(AppDbContext db) => _db = db;

    public Organization Item { get; private set; } = null!;
    public List<Person> Employees { get; private set; } = new();

    public List<SelectListItem> FreePeople { get; private set; } = new();

    [BindProperty(SupportsGet = true)] public bool ShowDismissed { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        return await LoadAsync(id) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        // права проверяем и на сервере, а не только прячем кнопки
        if (!User.CanEditOrganizations()) return Forbid();

        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == id);
        if (org == null) return NotFound();

        // организацию с сотрудниками не удаляем
        if (await _db.People.AnyAsync(p => p.OrganizationId == id))
        {
            TempData["Error"] = "Нельзя удалить организацию, к которой привязаны сотрудники. " +
                                "Сначала открепите или удалите сотрудников.";
            return RedirectToPage(new { id });
        }

        _db.Organizations.Remove(org);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Организация удалена";
        return RedirectToPage("./Index");
    }

    public async Task<IActionResult> OnPostAttachAsync(int id, int personId)
    {
        if (!User.CanEditOrganizations()) return Forbid();

        var person = await _db.People.FirstOrDefaultAsync(p => p.Id == personId && p.OrganizationId == null);
        if (person == null)
        {
            TempData["Error"] = "Сотрудник не найден или уже привязан к организации";
            return RedirectToPage(new { id });
        }

        person.OrganizationId = id;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Сотрудник прикреплён";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDetachAsync(int id, int personId)
    {
        if (!User.CanEditOrganizations()) return Forbid();

        var person = await _db.People.FirstOrDefaultAsync(p => p.Id == personId && p.OrganizationId == id);
        if (person != null)
        {
            person.OrganizationId = null;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Сотрудник откреплён от организации";
        }

        return RedirectToPage(new { id });
    }

    private async Task<bool> LoadAsync(int id)
    {
        var org = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
        if (org == null) return false;
        Item = org;

        var employees = _db.People.AsNoTracking().Where(p => p.OrganizationId == id);
        if (!ShowDismissed) employees = employees.Where(p => p.Status == PersonStatus.Active);
        Employees = await employees.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ToListAsync();

        var free = await _db.People.AsNoTracking()
            .Where(p => p.OrganizationId == null && p.Status == PersonStatus.Active)
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .Select(p => new { p.Id, p.LastName, p.FirstName, p.MiddleName })
            .ToListAsync();
        FreePeople = free
            .Select(p => new SelectListItem(Infrastructure.Fmt.FullName(p.LastName, p.FirstName, p.MiddleName),
                p.Id.ToString()))
            .ToList();

        return true;
    }
}
