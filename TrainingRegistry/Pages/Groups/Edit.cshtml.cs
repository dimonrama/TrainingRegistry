using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;
using TrainingRegistry.Infrastructure;

namespace TrainingRegistry.Pages.Groups;

public class EditModel : PageModel
{
    private readonly AppDbContext _db;

    public EditModel(AppDbContext db) => _db = db;

    public TrainingGroup Item { get; set; } = new();
    public bool IsNew { get; set; }
    public List<SelectListItem> Programs { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        IsNew = id == null;
        if (id == null)
        {
            Item = new TrainingGroup { StartDate = Fmt.Today() };
            await LoadProgramsAsync(null);
            return Page();
        }

        var group = await _db.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id);
        if (group == null) return NotFound();

        Item = group;
        await LoadProgramsAsync(group.ProgramId);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        IsNew = id == null;
        TrainingGroup entity;

        if (id == null)
        {
            entity = new TrainingGroup();
        }
        else
        {
            var existing = await _db.Groups.FirstOrDefaultAsync(g => g.Id == id);
            if (existing == null) return NotFound();
            entity = existing;
        }

        Item = entity;
        var previousProgramId = id == null ? (int?)null : entity.ProgramId;

        // привязываем только нужные поля, чтобы нельзя было подсунуть лишнее
        await TryUpdateModelAsync(entity, nameof(Item),
            x => x.ProgramId, x => x.StartDate, x => x.EndDate, x => x.Status, x => x.Name, x => x.Note,
            x => x.RegistrationNumber, x => x.FundingSource, x => x.CuratorName, x => x.CommissionMembers,
            x => x.CommissionSecretary);

        await LoadProgramsAsync(previousProgramId);

        TrainingProgram? program = null;
        if (entity.ProgramId <= 0)
        {
            ModelState.AddModelError("Item.ProgramId", "Выберите программу обучения");
        }
        else
        {
            program = await _db.Programs.AsNoTracking().FirstOrDefaultAsync(p => p.Id == entity.ProgramId);
            if (program == null)
                ModelState.AddModelError("Item.ProgramId", "Выбранная программа не найдена");
            else if (program.Status == ProgramStatus.Archived && entity.ProgramId != previousProgramId)
                ModelState.AddModelError("Item.ProgramId", "Программа находится в архиве");
        }

        if (entity.StartDate == default)
            ModelState.AddModelError("Item.StartDate", "Укажите дату начала обучения");

        if (entity.EndDate != null && entity.EndDate < entity.StartDate)
            ModelState.AddModelError("Item.EndDate", "Дата окончания не может быть раньше даты начала");

        // если название не ввели - собираем из названия программы и даты начала
        if (string.IsNullOrWhiteSpace(entity.Name))
        {
            ModelState.Remove("Item.Name");
            if (program != null && entity.StartDate != default)
                entity.Name = $"{program.Name}, {Fmt.Date(entity.StartDate)}";
        }

        if (!ModelState.IsValid) return Page();

        if (id == null) _db.Groups.Add(entity);
        await _db.SaveChangesAsync();

        TempData["Success"] = id == null ? "Группа создана" : "Изменения сохранены";
        return RedirectToPage("./Details", new { id = entity.Id });
    }

    private async Task LoadProgramsAsync(int? currentProgramId)
    {
        Programs = await _db.Programs.AsNoTracking()
            .Where(p => p.Status == ProgramStatus.Active || p.Id == currentProgramId)
            .OrderBy(p => p.Name)
            .Select(p => new SelectListItem(p.Name, p.Id.ToString()))
            .ToListAsync();
    }
}
