using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;
using TrainingRegistry.Infrastructure;
using TrainingRegistry.Security;

namespace TrainingRegistry.Pages.People;

[Authorize(Policy = Policies.CanEditPeople)]
public class EditModel : PageModel
{
    private readonly AppDbContext _db;

    private readonly PersonDuplicateService _duplicates;

    public EditModel(AppDbContext db, PersonDuplicateService duplicates)
    {
        _db = db;
        _duplicates = duplicates;
    }

    // похожие записи, найденные при сохранении
    public List<PersonDuplicateService.Match> Duplicates { get; private set; } = new();

    // оператор подтвердил, что это другой человек
    [BindProperty] public bool ConfirmNotDuplicate { get; set; }
    [BindProperty] public string? DuplicateComment { get; set; }

    public Person Item { get; set; } = new();
    public bool IsNew { get; set; }
    public List<SelectListItem> Organizations { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int? id, int? organizationId)
    {
        IsNew = id == null;

        if (id == null)
        {
            Item.OrganizationId = organizationId;
        }
        else
        {
            var person = await _db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (person == null) return NotFound();
            Item = person;
        }

        await LoadOrganizationsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        IsNew = id == null;
        Person entity;

        if (id == null)
        {
            entity = new Person();
        }
        else
        {
            var existing = await _db.People.FirstOrDefaultAsync(p => p.Id == id);
            if (existing == null) return NotFound();
            entity = existing;
        }

        Item = entity;

        var ok = await TryUpdateModelAsync(entity, nameof(Item),
            x => x.LastName, x => x.FirstName, x => x.MiddleName, x => x.BirthDate, x => x.Snils,
            x => x.Phone, x => x.Email, x => x.OrganizationId, x => x.Position,
            x => x.Status, x => x.DismissalDate);

        if (entity.BirthDate != null && entity.BirthDate > DateOnly.FromDateTime(DateTime.Today))
            ModelState.AddModelError("Item.BirthDate", "Дата рождения не может быть в будущем");

        // СНИЛС приводим к единому виду и проверяем контрольное число
        if (!string.IsNullOrWhiteSpace(entity.Snils))
        {
            var snilsError = Snils.Validate(entity.Snils);
            if (snilsError != null) ModelState.AddModelError("Item.Snils", snilsError);
            else entity.Snils = Snils.Normalize(entity.Snils);
        }
        else entity.Snils = null;

        if (entity.OrganizationId != null &&
            !await _db.Organizations.AnyAsync(o => o.Id == entity.OrganizationId))
            ModelState.AddModelError("Item.OrganizationId", "Выбранная организация не найдена");

        if (!ok || !ModelState.IsValid)
        {
            await LoadOrganizationsAsync();
            return Page();
        }

        // ищем возможные дубли; если оператор подтвердил, что это другой человек - сохраняем
        // у существующей записи сверяем только если поменяли то, по чему ищем совпадения
        var identity = new[] { nameof(Person.LastName), nameof(Person.FirstName), nameof(Person.MiddleName),
            nameof(Person.BirthDate), nameof(Person.Snils), nameof(Person.Email) };
        var identityChanged = id == null || _db.Entry(entity).Properties
            .Any(p => p.IsModified && identity.Contains(p.Metadata.Name));
        if (identityChanged) Duplicates = await _duplicates.FindAsync(PersonKey.From(entity), id);
        if (Duplicates.Count > 0 && !ConfirmNotDuplicate)
        {
            await LoadOrganizationsAsync();
            return Page();
        }

        // дата увольнения нужна только у уволенных
        if (entity.Status == PersonStatus.Dismissed)
            entity.DismissalDate ??= DateOnly.FromDateTime(DateTime.Today);
        else
            entity.DismissalDate = null;

        if (id == null) _db.People.Add(entity);
        await _db.SaveChangesAsync();

        // сохранение вопреки совпадению оставляем в истории с пояснением
        if (Duplicates.Count > 0)
        {
            var with = string.Join("; ", Duplicates.Select(d => $"{d.FullName} ({d.ReasonsText})"));
            var comment = string.IsNullOrWhiteSpace(DuplicateComment) ? "" : $". Пояснение: {DuplicateComment.Trim()}";
            await _db.LogAsync("Сохранено при совпадении", nameof(Person), entity.Id, entity.FullName,
                Trunc($"Возможный дубль: {with}{comment}", 1000));
        }

        TempData["Success"] = id == null ? "Обучающийся добавлен" : "Изменения сохранены";
        return RedirectToPage("./Details", new { id = entity.Id });
    }

    private static string Trunc(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";

    private async Task LoadOrganizationsAsync()
    {
        var orgs = await _db.Organizations.AsNoTracking()
            .OrderBy(o => o.FullName)
            .Select(o => new { o.Id, o.FullName, o.ShortName })
            .ToListAsync();
        Organizations = orgs
            .Select(o => new SelectListItem(o.ShortName ?? o.FullName, o.Id.ToString()))
            .ToList();
    }
}
