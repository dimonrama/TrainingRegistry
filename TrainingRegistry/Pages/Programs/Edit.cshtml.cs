using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;

namespace TrainingRegistry.Pages.Programs;

public class EditModel : PageModel
{
    private readonly AppDbContext _db;
    public EditModel(AppDbContext db) => _db = db;
    public TrainingProgram Item { get; set; } = new();
    public bool IsNew { get; set; }

    // отмеченные обязательные поля; на странице это набор флажков
    [BindProperty] public List<PersonField> Required { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        IsNew = id == null;
        if (id == null)
        {
            // для новой программы сразу предлагаем минимум для документов об обучении
            Required = new List<PersonField> { PersonField.BirthDate, PersonField.Snils };
            return Page();
        }
        var program = await _db.Programs.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (program == null) return NotFound();

        Item = program;
        Required = PersonRequirements.All.Where(f => program.RequiredFields.HasFlag(f)).ToList();
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(int? id)
    {
        IsNew = id == null;
        TrainingProgram entity;
        if (id == null)
        {
            entity = new TrainingProgram();
        }
        else
        {
            var existing = await _db.Programs.FirstOrDefaultAsync(p => p.Id == id);
            if (existing == null) return NotFound();
            entity = existing;
        }
        Item = entity;
        var ok = await TryUpdateModelAsync(entity, nameof(Item),
            x => x.Name, x => x.Description, x => x.Price, x => x.RequiredEducation,
            x => x.Recurrence, x => x.CustomMonths, x => x.Duration, x => x.DurationUnit,
            x => x.Status, x => x.NotifyDaysBefore);
        entity.RequiredFields = Required.Where(f => PersonRequirements.All.Contains(f))
            .Aggregate(PersonField.None, (acc, f) => acc | f);
        // количество месяцев нужно только для произвольного срока
        if (entity.Recurrence == Recurrence.Custom)
        {
            if (entity.CustomMonths == null)
                ModelState.AddModelError("Item.CustomMonths", "Укажите количество месяцев");
        }
        else
        {
            entity.CustomMonths = null;
            ModelState.Remove("Item.CustomMonths");
        }
        if (!ok || !ModelState.IsValid) return Page();
        if (id == null) _db.Programs.Add(entity);
        await _db.SaveChangesAsync();

        TempData["Success"] = id == null ? "Программа добавлена" : "Изменения сохранены";
        return RedirectToPage("./Index");
    }
}