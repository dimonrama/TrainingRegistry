using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;
using TrainingRegistry.Infrastructure;
using TrainingRegistry.Security;

namespace TrainingRegistry.Pages.Groups;

public class DetailsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ContractService _contracts;
    public DetailsModel(AppDbContext db, ContractService contracts)
    {
        _db = db;
        _contracts = contracts;
    }
    public List<ContractService.CustomerOption> Customers { get; private set; } = new();
    public TrainingGroup Item { get; private set; } = null!;
    public TrainingProgram ProgramInfo { get; private set; } = null!;
    public List<MemberRow> Members { get; private set; } = new();
    public List<SelectListItem> Candidates { get; private set; } = new();

    [BindProperty(SupportsGet = true)] public string? PersonSearch { get; set; }
    public List<string> Warnings { get; private set; } = new();
    // чего не хватает в карточке для зачисления: пока не заполнено, зачислить нельзя
    public List<string> BlockedMissing { get; private set; } = new();
    public List<GeneratedDocument> GeneratedDocs { get; private set; } = new();
    public int? PendingPersonId { get; private set; }
    public string? PendingPersonName { get; private set; }

    public bool IsClosed => Item.Status is GroupStatus.Completed or GroupStatus.Cancelled;
    public record MemberRow(int EnrollmentId, int PersonId, string FullName, string? OrganizationName, string? Position,
        bool Dismissed, EnrollmentStatus Status, DateOnly? CompletedDate, DateOnly? NextTrainingDate, string? CertificateNumber,
        FundingSource? Funding, FundingSource? OwnFunding, AttestationResult? Grade, string? ExpelReason);

    public IEnumerable<SelectListItem> FundingItems(FundingSource? current)
        => Enum.GetValues<FundingSource>().Select(f => new SelectListItem(FundingRules.Short(f), f.ToString(), f == current));

    public IEnumerable<SelectListItem> GradeItems(AttestationResult? current)
        => Enum.GetValues<AttestationResult>().Select(g => new SelectListItem(g.GetDisplayName(), g.ToString(), g == current));

    public IEnumerable<SelectListItem> StatusItems(EnrollmentStatus current)
        => Enum.GetValues<EnrollmentStatus>()
            .Select(s => new SelectListItem(s.GetDisplayName(), s.ToString(), s == current));

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        return Page();
    }
    public async Task<IActionResult> OnGetExportAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();

        var bytes = XlsxWriter.Create("Состав группы",
            new[] { "ФИО", "Организация", "Должность", "Финансирование", "Статус обучения", "Результат аттестации",
                "Дата прохождения", "Следующее обучение", "№ удостоверения" },
            Members.Select(m => new object?[]
            {
                m.FullName, m.OrganizationName ?? "Физическое лицо", m.Position, FundingRules.Short(m.Funding),
                m.Status.GetDisplayName(), m.Grade?.GetDisplayName(), m.CompletedDate, m.NextTrainingDate, m.CertificateNumber
            }));

        var safeName = string.Concat(Item.Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return File(bytes, XlsxWriter.ContentType, $"Состав_группы_{safeName}_{Fmt.Today():yyyy-MM-dd}.xlsx");
    }

    // зачисление: сначала запреты, потом предупреждения (их можно подтвердить)
    public async Task<IActionResult> OnPostEnrollAsync(int id, int personId, bool confirm)
    {
        if (!User.CanEnroll()) return Forbid();
        var group = await _db.Groups.AsNoTracking().Include(g => g.Program).FirstOrDefaultAsync(g => g.Id == id);
        if (group == null) return NotFound();
        if (group.Status is GroupStatus.Completed or GroupStatus.Cancelled)
        {
            TempData["Error"] = "В завершённую или отменённую группу зачислять нельзя. Измените статус группы.";
            return RedirectToPage(new { id });
        }
        var person = await _db.People.AsNoTracking().Include(p => p.Educations)
            .FirstOrDefaultAsync(p => p.Id == personId);
        if (person == null)
        {
            TempData["Error"] = "Обучающийся не найден";
            return RedirectToPage(new { id });
        }
        // дубли запрещаем
        if (await _db.Enrollments.AnyAsync(e => e.GroupId == id && e.PersonId == personId))
        {
            TempData["Error"] = $"{person.FullName} уже зачислен в эту группу";
            return RedirectToPage(new { id });
        }
        // обязательные для программы данные: пока их нет, зачислить нельзя
        var docTypes = await _db.PersonDocuments.AsNoTracking().Where(d => d.PersonId == personId)
            .Select(d => d.Type).Distinct().ToListAsync();
        var docs = new AttachedDocs(docTypes.Contains(DocumentType.Passport), docTypes.Contains(DocumentType.Snils),
            docTypes.Contains(DocumentType.Diploma) || person.Educations.Any(e => e.FilePath != null));
        var missing = PersonRequirements.Missing(person, docs, group.Program!.RequiredFields);
        if (missing.Count > 0)
        {
            await LoadAsync(id);
            BlockedMissing = missing;
            PendingPersonId = person.Id;
            PendingPersonName = person.FullName;
            return Page();
        }

        var warnings = new List<string>();
        // уволенный - только предупреждение
        if (person.IsDismissed)
            warnings.Add($"Обучающийся уволен ({Fmt.Date(person.DismissalDate)}).");

        var required = group.Program!.RequiredEducation;
        // образование не подтверждено - тоже только предупреждение
        if (!EducationRules.IsConfirmed(person.Educations, required))
            warnings.Add($"Образование не подтверждено: программа требует «{required.GetDisplayName()}».");
        if (warnings.Count > 0 && !confirm)
        {
            await LoadAsync(id);
            Warnings = warnings;
            PendingPersonId = person.Id;
            PendingPersonName = person.FullName;
            return Page();
        }
        _db.Enrollments.Add(new Enrollment { PersonId = personId, GroupId = id, FundingSource = group.FundingSource });
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = $"{person.FullName} уже зачислен в эту группу";
            return RedirectToPage(new { id });
        }
        TempData["Success"] = warnings.Count > 0
            ? $"{person.FullName} зачислен (с предупреждением)"
            : $"{person.FullName} зачислен";
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostStatusAsync(int id, int enrollmentId, EnrollmentStatus status,
        DateOnly? completedDate, string? certificate, FundingSource? funding, AttestationResult? grade)
    {
        if (!User.CanEnroll()) return Forbid();
        var enrollment = await _db.Enrollments
            .Include(e => e.Group).ThenInclude(g => g!.Program)
            .Include(e => e.Person)
            .FirstOrDefaultAsync(e => e.Id == enrollmentId && e.GroupId == id);
        if (enrollment == null) return NotFound();
        certificate = string.IsNullOrWhiteSpace(certificate) ? null : certificate.Trim();
        if (certificate?.Length > 100)
        {
            TempData["Error"] = "Номер удостоверения слишком длинный (не более 100 символов)";
            return RedirectToPage(new { id });
        }
        enrollment.FundingSource = funding;
        enrollment.Grade = grade;

        // при "Прошел обучение" ставим дату и считаем следующее обучение
        if (status == EnrollmentStatus.Completed)
        {
            var date = completedDate ?? Fmt.Today();
            // дату прохождения в будущем ставить нельзя
            if (date > Fmt.Today())
            {
                TempData["Error"] = "Дата прохождения не может быть в будущем";
                return RedirectToPage(new { id });
            }
            enrollment.MarkCompleted(date, enrollment.Group!.Program!);
            enrollment.ExpelledDate = null;
            enrollment.ExpelReason = null;
            enrollment.CertificateNumber = certificate;
            var next = enrollment.NextTrainingDate;
            TempData["Success"] = next == null
                ? $"{enrollment.Person!.FullName}: обучение пройдено {Fmt.Date(date)}. Повторное обучение не требуется"
                : $"{enrollment.Person!.FullName}: обучение пройдено {Fmt.Date(date)}. Следующее — до {Fmt.Date(next)}";
        }
        else
        {
            // дату отчисления ставим один раз; если вернули из отчисленных - очищаем
            if (status == EnrollmentStatus.Expelled) enrollment.ExpelledDate ??= Fmt.Today();
            else
            {
                enrollment.ExpelledDate = null;
                enrollment.ExpelReason = null;
            }
            enrollment.Status = status;
            enrollment.CompletedDate = null;
            enrollment.NextTrainingDate = null;
            enrollment.CertificateNumber = certificate;
            TempData["Success"] = $"{enrollment.Person!.FullName}: статус «{status.GetDisplayName()}»";
        }
        await _db.SaveChangesAsync();
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostRemoveAsync(int id, int enrollmentId)
    {
        if (!User.CanEnroll()) return Forbid();
        var enrollment = await _db.Enrollments.Include(e => e.Person)
            .FirstOrDefaultAsync(e => e.Id == enrollmentId && e.GroupId == id);
        if (enrollment == null) return NotFound();
        // прошедшего обучение не исключаем, это история
        if (enrollment.Status == EnrollmentStatus.Completed)
        {
            TempData["Error"] = "Запись о пройденном обучении удалить нельзя. При ошибке измените статус.";
            return RedirectToPage(new { id });
        }
        _db.Enrollments.Remove(enrollment);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"{enrollment.Person!.FullName} исключён из группы";
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostContractAsync(int id, int? organizationId, int? personId)
    {
        var (contract, error) = await _contracts.CreateAsync(id, organizationId, personId);
        if (contract == null)
        {
            TempData["Error"] = error;
            return RedirectToPage(new { id });
        }
        TempData["Success"] = $"Договор {contract.Number} сформирован";
        return RedirectToPage("/Contracts/Details", new { id = contract.Id });
    }
    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == id);
        if (group == null) return NotFound();
        if (await _db.Enrollments.AnyAsync(e => e.GroupId == id) || await _db.Contracts.AnyAsync(c => c.GroupId == id))
        {
            TempData["Error"] = "В группе есть обучающиеся или договор, удалить её нельзя. Переведите группу в статус «Отменена».";
            return RedirectToPage(new { id });
        }
        _db.Groups.Remove(group);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Группа удалена";
        return RedirectToPage("./Index");
    }
    private async Task<bool> LoadAsync(int id)
    {
        var group = await _db.Groups.AsNoTracking().Include(g => g.Program).FirstOrDefaultAsync(g => g.Id == id);
        if (group == null) return false;

        Item = group;
        ProgramInfo = group.Program!;
        var rows = await _db.Enrollments.AsNoTracking()
            .Where(e => e.GroupId == id)
            .OrderBy(e => e.Person!.LastName).ThenBy(e => e.Person!.FirstName)
            .Select(e => new
            {
                e.Id, e.PersonId,
                e.Person!.LastName, e.Person.FirstName, e.Person.MiddleName,
                Org = e.Person.Organization != null ? (e.Person.Organization.ShortName ?? e.Person.Organization.FullName) : null,
                e.Person.Position, PStatus = e.Person.Status,
                EStatus = e.Status, e.CompletedDate, e.NextTrainingDate, e.CertificateNumber,
                e.FundingSource, e.Grade, e.ExpelReason
            })
            .ToListAsync();
        Members = rows.Select(r => new MemberRow(r.Id, r.PersonId, Fmt.FullName(r.LastName, r.FirstName, r.MiddleName),
            r.Org, r.Position, r.PStatus == PersonStatus.Dismissed, r.EStatus, r.CompletedDate, r.NextTrainingDate,
            r.CertificateNumber, FundingRules.Effective(group.FundingSource, r.FundingSource), r.FundingSource, r.Grade,
            r.ExpelReason)).ToList();
        GeneratedDocs = await _db.GeneratedDocuments.AsNoTracking().Where(d => d.GroupId == id)
            .OrderByDescending(d => d.Id).Take(30).ToListAsync();
        Customers = await _contracts.GetCustomersAsync(id);
        if (!string.IsNullOrWhiteSpace(PersonSearch) && !IsClosed)
        {
            var query = _db.People.AsNoTracking().Where(p => !p.Enrollments.Any(e => e.GroupId == id));
            foreach (var word in PersonSearch.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var w = word.ToLower();
                query = query.Where(p => p.LastName.ToLower().Contains(w) || p.FirstName.ToLower().Contains(w) ||
                                         (p.MiddleName != null && p.MiddleName.ToLower().Contains(w)) ||
                                         (p.Organization != null && p.Organization.FullName.ToLower().Contains(w)));
            }
            var found = await query.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).Take(50)
                .Select(p => new
                {
                    p.Id, p.LastName, p.FirstName, p.MiddleName, p.Status,
                    Org = p.Organization != null ? (p.Organization.ShortName ?? p.Organization.FullName) : null
                })
                .ToListAsync();
            Candidates = found.Select(p =>
            {
                var text = Fmt.FullName(p.LastName, p.FirstName, p.MiddleName);
                text += p.Org != null ? $" — {p.Org}" : " — физическое лицо";
                if (p.Status == PersonStatus.Dismissed) text += " (уволен)";
                return new SelectListItem(text, p.Id.ToString());
            }).ToList();
        }
        return true;
    }
}