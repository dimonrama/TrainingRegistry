using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;
using TrainingRegistry.Infrastructure;
using TrainingRegistry.Security;

namespace TrainingRegistry.Pages.People;

[Authorize(Policy = Policies.CanEditPeople)]
public class EducationModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IFileStorage _storage;

    public EducationModel(AppDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public Education Item { get; set; } = new();
    public int PersonId { get; private set; }
    public string PersonName { get; private set; } = "";
    public bool IsNew { get; private set; }

    [BindProperty] public IFormFile? Upload { get; set; }

    public async Task<IActionResult> OnGetAsync(int personId, int? id)
    {
        if (!await LoadPersonAsync(personId)) return NotFound();
        IsNew = id == null;

        if (id != null)
        {
            var education = await _db.Educations.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id && e.PersonId == personId);
            if (education == null) return NotFound();
            Item = education;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int personId, int? id)
    {
        if (!await LoadPersonAsync(personId)) return NotFound();
        IsNew = id == null;

        Education entity;
        if (id == null)
        {
            entity = new Education { PersonId = personId };
        }
        else
        {
            var existing = await _db.Educations.FirstOrDefaultAsync(e => e.Id == id && e.PersonId == personId);
            if (existing == null) return NotFound();
            entity = existing;
        }

        Item = entity;

        var ok = await TryUpdateModelAsync(entity, nameof(Item),
            x => x.Level, x => x.InstitutionName, x => x.GraduationYear, x => x.Specialty);

        if (Upload is { Length: > 0 })
        {
            var error = _storage.Validate(Upload);
            if (error != null) ModelState.AddModelError(nameof(Upload), error);
        }

        if (!ok || !ModelState.IsValid) return Page();

        string? oldPath = null;
        if (Upload is { Length: > 0 })
        {
            oldPath = entity.FilePath;
            var stored = await _storage.SaveAsync(Upload, $"education/{personId}");
            entity.FilePath = stored.RelativePath;
            entity.OriginalFileName = stored.OriginalName;
        }

        if (id == null) _db.Educations.Add(entity);
        await _db.SaveChangesAsync();

        // старый файл удаляем только после того, как всё сохранилось
        if (oldPath != null) _storage.Delete(oldPath);

        TempData["Success"] = id == null ? "Запись об образовании добавлена" : "Изменения сохранены";
        return RedirectToPage("./Details", new { id = personId });
    }

    private async Task<bool> LoadPersonAsync(int personId)
    {
        var person = await _db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == personId);
        if (person == null) return false;
        PersonId = person.Id;
        PersonName = person.FullName;
        return true;
    }
}
