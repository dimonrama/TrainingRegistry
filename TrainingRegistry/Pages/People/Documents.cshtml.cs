using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;
using TrainingRegistry.Infrastructure;
using TrainingRegistry.Security;

namespace TrainingRegistry.Pages.People;

// прикрепление паспорта, СНИЛС, диплома и прочих документов к карточке
[Authorize(Policy = Policies.CanEditDocuments)]
public class DocumentsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IFileStorage _storage;

    public DocumentsModel(AppDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public int PersonId { get; private set; }
    public string PersonName { get; private set; } = "";

    [BindProperty, Display(Name = "Вид документа")] public DocumentType Type { get; set; } = DocumentType.Passport;
    [BindProperty, StringLength(200), Display(Name = "Название")] public string? Title { get; set; }
    [BindProperty, Display(Name = "Файл")] public IFormFile? Upload { get; set; }

    public async Task<IActionResult> OnGetAsync(int personId)
        => await LoadPersonAsync(personId) ? Page() : NotFound();

    public async Task<IActionResult> OnPostAsync(int personId)
    {
        if (!await LoadPersonAsync(personId)) return NotFound();

        if (Upload == null || Upload.Length == 0)
            ModelState.AddModelError(nameof(Upload), "Выберите файл");
        else
        {
            var error = _storage.Validate(Upload);
            if (error != null) ModelState.AddModelError(nameof(Upload), error);
        }
        if (Type == DocumentType.Other && string.IsNullOrWhiteSpace(Title))
            ModelState.AddModelError(nameof(Title), "Для другого документа укажите название");
        if (!ModelState.IsValid) return Page();

        // паспорт, СНИЛС и диплом храним в одном экземпляре - новый файл заменяет прежний
        var replaced = new List<string>();
        if (Type != DocumentType.Other)
        {
            var old = await _db.PersonDocuments.Where(d => d.PersonId == personId && d.Type == Type).ToListAsync();
            replaced.AddRange(old.Select(d => d.FilePath));
            _db.PersonDocuments.RemoveRange(old);
        }

        var stored = await _storage.SaveAsync(Upload!, $"people/{personId}");
        _db.PersonDocuments.Add(new PersonDocument
        {
            PersonId = personId,
            Type = Type,
            Title = string.IsNullOrWhiteSpace(Title) ? null : Title.Trim(),
            FilePath = stored.RelativePath,
            OriginalFileName = stored.OriginalName,
            FileSize = Upload!.Length
        });
        try
        {
            await _db.SaveChangesAsync();
        }
        catch
        {
            _storage.Delete(stored.RelativePath);
            throw;
        }
        foreach (var path in replaced) _storage.Delete(path);

        TempData["Success"] = "Документ прикреплён";
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
