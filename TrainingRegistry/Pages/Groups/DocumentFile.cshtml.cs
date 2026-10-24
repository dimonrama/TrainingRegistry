using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Infrastructure;
using TrainingRegistry.Security;

namespace TrainingRegistry.Pages.Groups;

[Authorize(Policy = Policies.CanReports)]
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
        var doc = await _db.GeneratedDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
        if (doc == null) return NotFound();

        var file = _storage.Open(doc.FilePath);
        if (file == null) return NotFound();
        return File(file.Value.Stream, file.Value.ContentType, doc.FileName);
    }
}
