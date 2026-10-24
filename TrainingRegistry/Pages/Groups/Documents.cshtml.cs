using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;
using TrainingRegistry.Infrastructure;
using TrainingRegistry.Security;

namespace TrainingRegistry.Pages.Groups;

// формирование приказов, протокола, ведомости и списка слушателей по группе
[Authorize(Policy = Policies.CanReports)]
public class DocumentsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly GroupDocumentService _docs;

    public DocumentsModel(AppDbContext db, GroupDocumentService docs)
    {
        _db = db;
        _docs = docs;
    }

    public TrainingGroup Item { get; private set; } = null!;
    public TrainingProgram ProgramInfo { get; private set; } = null!;
    public List<GroupDocumentService.Member> Members { get; private set; } = new();
    public List<GeneratedDocument> History { get; private set; } = new();
    public List<string> Errors { get; private set; } = new();

    [BindProperty] public GeneratedDocKind Kind { get; set; } = GeneratedDocKind.EnrollmentOrder;
    [BindProperty] public DocForm Form { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id, GeneratedDocKind? kind)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (kind != null) Kind = kind.Value;
        Form.Date = Fmt.Today();
        Form.ExpelDate = Item.EndDate ?? Fmt.Today();
        Form.RegistrationNumber = Item.RegistrationNumber;
        Form.Curator = Item.CuratorName;
        Form.Hours = ProgramInfo.AcademicHours?.ToString();
        Form.Commission = Item.CommissionMembers;
        Form.Secretary = Item.CommissionSecretary;
        Form.StartDate = Item.StartDate;
        Form.EndDate = Item.EndDate;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();

        var result = await _docs.GenerateAsync(id, Kind, Form);
        if (!result.Ok)
        {
            Errors = result.Errors;
            return Page();
        }

        TempData["Success"] = $"Документ сформирован: {result.Document!.FileName}";
        if (result.Warnings.Count > 0) TempData["Warnings"] = string.Join("|", result.Warnings);
        return RedirectToPage(new { id });
    }

    // список слушателей отдаём сразу файлом, в базе не храним
    public async Task<IActionResult> OnGetListAsync(int id, string format)
    {
        var file = await _docs.BuildListAsync(id, format);
        if (file == null) return NotFound();
        return File(file.Bytes, file.ContentType, file.FileName);
    }

    private async Task<bool> LoadAsync(int id)
    {
        var data = await _docs.LoadAsync(id);
        if (data == null) return false;
        Item = data.Group;
        ProgramInfo = data.Program;
        Members = data.Members;
        History = await _db.GeneratedDocuments.AsNoTracking().Where(d => d.GroupId == id)
            .OrderByDescending(d => d.Id).Take(50).ToListAsync();
        return true;
    }
}
