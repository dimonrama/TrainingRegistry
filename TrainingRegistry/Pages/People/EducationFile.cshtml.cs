using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Infrastructure;
using TrainingRegistry.Security;

namespace TrainingRegistry.Pages.People;

[Authorize(Policy = Policies.CanViewDocuments)]
public class EducationFileModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IFileStorage _storage;

    public EducationFileModel(AppDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    // файлы лежат вне wwwroot, поэтому отдаём через страницу и только вошедшим
    public async Task<IActionResult> OnGetAsync(int id)
    {
        var path = await _db.Educations.AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => e.FilePath)
            .FirstOrDefaultAsync();
        if (path == null) return NotFound();

        var file = _storage.Open(path);
        if (file == null) return NotFound();

        return File(file.Value.Stream, file.Value.ContentType);
    }
}
