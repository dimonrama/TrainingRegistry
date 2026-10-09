using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Infrastructure;

namespace TrainingRegistry.Pages.Contracts;

public class DownloadModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IFileStorage _storage;

    public DownloadModel(AppDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<IActionResult> OnGetAsync(int id, string format)
    {
        var contract = await _db.Contracts.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new { c.Number, c.DocxPath, c.PdfPath })
            .FirstOrDefaultAsync();
        if (contract == null) return NotFound();

        var isPdf = string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase);
        if (!isPdf && !string.Equals(format, "docx", StringComparison.OrdinalIgnoreCase)) return NotFound();

        var path = isPdf ? contract.PdfPath : contract.DocxPath;
        if (path == null) return NotFound();

        var file = _storage.Open(path);
        if (file == null) return NotFound();

        return File(file.Value.Stream, file.Value.ContentType, $"{contract.Number}.{(isPdf ? "pdf" : "docx")}");
    }
}
