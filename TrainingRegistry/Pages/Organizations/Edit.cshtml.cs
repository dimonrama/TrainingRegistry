using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;
using TrainingRegistry.Security;

namespace TrainingRegistry.Pages.Organizations;

[Authorize(Policy = Policies.CanEditOrganizations)]
public class EditModel : PageModel
{
    private readonly AppDbContext _db;

    public EditModel(AppDbContext db) => _db = db;

    public Organization Item { get; set; } = new();

    public bool IsNew { get; set; }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        IsNew = id == null;
        if (id == null) return Page();

        var org = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
        if (org == null) return NotFound();

        Item = org;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        IsNew = id == null;
        Organization entity;

        if (id == null)
        {
            entity = new Organization();
        }
        else
        {
            var existing = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == id);
            if (existing == null) return NotFound();
            entity = existing;
        }

        Item = entity;

        var ok = await TryUpdateModelAsync(entity, nameof(Item),
            x => x.FullName, x => x.ShortName, x => x.Inn, x => x.Kpp, x => x.Ogrn,
            x => x.LegalAddress, x => x.ActualAddress, x => x.Phone, x => x.Email,
            x => x.ContactName, x => x.ContactPosition, x => x.Note);
        if (!ok) return Page();

        if (id == null) _db.Organizations.Add(entity);
        await _db.SaveChangesAsync();

        TempData["Success"] = id == null ? "Организация добавлена" : "Изменения сохранены";
        return RedirectToPage("./Details", new { id = entity.Id });
    }
}
