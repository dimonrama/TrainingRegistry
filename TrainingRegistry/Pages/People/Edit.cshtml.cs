using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;
using TrainingRegistry.Security;

namespace TrainingRegistry.Pages.People;

[Authorize(Policy = Policies.CanEditPeople)]
public class EditModel : PageModel
{
    private readonly AppDbContext _db;

    public EditModel(AppDbContext db) => _db = db;

    public Person Item { get; set; } = new();
    public bool IsNew { get; set; }
    public List<SelectListItem> Organizations { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int? id, int? organizationId)
    {
        IsNew = id == null;

        if (id == null)
        {
            Item.OrganizationId = organizationId;
        }
        else
        {
            var person = await _db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (person == null) return NotFound();
            Item = person;
        }

        await LoadOrganizationsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        IsNew = id == null;
        Person entity;

        if (id == null)
        {
            entity = new Person();
        }
        else
        {
            var existing = await _db.People.FirstOrDefaultAsync(p => p.Id == id);
            if (existing == null) return NotFound();
            entity = existing;
        }

        Item = entity;

        var ok = await TryUpdateModelAsync(entity, nameof(Item),
            x => x.LastName, x => x.FirstName, x => x.MiddleName, x => x.BirthDate,
            x => x.Phone, x => x.Email, x => x.OrganizationId, x => x.Position,
            x => x.Status, x => x.DismissalDate);

        if (entity.BirthDate != null && entity.BirthDate > DateOnly.FromDateTime(DateTime.Today))
            ModelState.AddModelError("Item.BirthDate", "Дата рождения не может быть в будущем");

        if (entity.OrganizationId != null &&
            !await _db.Organizations.AnyAsync(o => o.Id == entity.OrganizationId))
            ModelState.AddModelError("Item.OrganizationId", "Выбранная организация не найдена");

        if (!ok || !ModelState.IsValid)
        {
            await LoadOrganizationsAsync();
            return Page();
        }

        // дата увольнения нужна только у уволенных
        if (entity.Status == PersonStatus.Dismissed)
            entity.DismissalDate ??= DateOnly.FromDateTime(DateTime.Today);
        else
            entity.DismissalDate = null;

        if (id == null) _db.People.Add(entity);
        await _db.SaveChangesAsync();

        TempData["Success"] = id == null ? "Сотрудник добавлен" : "Изменения сохранены";
        return RedirectToPage("./Details", new { id = entity.Id });
    }

    private async Task LoadOrganizationsAsync()
    {
        var orgs = await _db.Organizations.AsNoTracking()
            .OrderBy(o => o.FullName)
            .Select(o => new { o.Id, o.FullName, o.ShortName })
            .ToListAsync();
        Organizations = orgs
            .Select(o => new SelectListItem(o.ShortName ?? o.FullName, o.Id.ToString()))
            .ToList();
    }
}
