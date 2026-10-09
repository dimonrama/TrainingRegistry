using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;
using TrainingRegistry.Security;

namespace TrainingRegistry.Pages.People;

public class DetailsModel : PageModel
{
    private readonly AppDbContext _db;

    public DetailsModel(AppDbContext db) => _db = db;

    public Person Item { get; private set; } = null!;
    public string? OrganizationName { get; private set; }
    public List<Education> Educations { get; private set; } = new();

    public List<TrainingRow> History { get; private set; } = new();

    public record TrainingRow(string GroupName, string ProgramName, DateOnly StartDate, DateOnly? EndDate,
        EnrollmentStatus Status, DateOnly? CompletedDate, DateOnly? NextTrainingDate, string? CertificateNumber);

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var person = await _db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (person == null) return NotFound();
        Item = person;

        if (person.OrganizationId != null)
        {
            var org = await _db.Organizations.AsNoTracking()
                .Where(o => o.Id == person.OrganizationId)
                .Select(o => o.ShortName ?? o.FullName)
                .FirstOrDefaultAsync();
            OrganizationName = org;
        }

        Educations = await _db.Educations.AsNoTracking()
            .Where(e => e.PersonId == id)
            .OrderByDescending(e => e.GraduationYear)
            .ToListAsync();

        History = await _db.Enrollments.AsNoTracking()
            .Where(e => e.PersonId == id)
            .OrderByDescending(e => e.Group!.StartDate)
            .Select(e => new TrainingRow(e.Group!.Name, e.Group.Program!.Name, e.Group.StartDate, e.Group.EndDate,
                e.Status, e.CompletedDate, e.NextTrainingDate, e.CertificateNumber))
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteEducationAsync(int id, int educationId)
    {
        if (!User.CanEditPeople()) return Forbid();

        var education = await _db.Educations.FirstOrDefaultAsync(e => e.Id == educationId && e.PersonId == id);
        if (education != null)
        {
            _db.Educations.Remove(education);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Запись об образовании удалена";
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (!User.CanEditPeople()) return Forbid();

        var person = await _db.People.FirstOrDefaultAsync(p => p.Id == id);
        if (person == null) return NotFound();

        if (await _db.Enrollments.AnyAsync(e => e.PersonId == id))
        {
            TempData["Error"] = "У сотрудника есть записи об обучении, удалить его нельзя. " +
                                "Измените статус на «Уволен» — он будет скрыт из списков.";
            return RedirectToPage(new { id });
        }

        _db.People.Remove(person);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Сотрудник удалён";
        return RedirectToPage("./Index");
    }
}
