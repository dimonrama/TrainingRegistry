using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;
using TrainingRegistry.Infrastructure;
using TrainingRegistry.Security;

namespace TrainingRegistry.Pages.People;

[Authorize(Policy = Policies.CanViewDocuments)]
public class DocumentFileModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IFileStorage _storage;

    public DocumentFileModel(AppDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var doc = await _db.PersonDocuments.AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new { d.FilePath, d.OriginalFileName, d.PersonId })
            .FirstOrDefaultAsync();
        if (doc == null) return NotFound();

        var file = _storage.Open(doc.FilePath);
        if (file == null) return NotFound();

        // просмотр личных документов тоже фиксируем в истории
        await _db.LogAsync("Просмотр документа", nameof(Person), doc.PersonId, doc.OriginalFileName, null);
        return File(file.Value.Stream, file.Value.ContentType);
    }
}
