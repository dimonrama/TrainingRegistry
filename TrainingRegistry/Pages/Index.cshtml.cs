using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;
using TrainingRegistry.Infrastructure;

namespace TrainingRegistry.Pages;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db) => _db = db;

    public int OrganizationsCount { get; private set; }
    public int ActiveEmployeesCount { get; private set; }
    public int ActiveGroupsCount { get; private set; }
    public int TrainedCount { get; private set; }
    public int RedCount { get; private set; }
    public int YellowCount { get; private set; }

    public record DueRow(int PersonId, string PersonName, string ProgramName, DateOnly NextTrainingDate, int DaysLeft, Urgency Urgency)
    {
        public string DaysLeftText => RetrainingRules.DaysLeftText(DaysLeft);
    }

    public record GroupRow(int Id, string Name, string ProgramName, DateOnly StartDate, GroupStatus Status, int Students);

    public List<DueRow> NearestDue { get; private set; } = new();
    public List<GroupRow> ActiveGroups { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var today = Fmt.Today();

        OrganizationsCount = await _db.Organizations.CountAsync();
        ActiveEmployeesCount = await _db.People.CountAsync(p => p.Status == PersonStatus.Active);
        ActiveGroupsCount = await _db.Groups.CountAsync(g => g.Status == GroupStatus.Recruiting || g.Status == GroupStatus.InProgress);
        TrainedCount = await _db.Enrollments.Where(e => e.Status == EnrollmentStatus.Completed)
            .Select(e => e.PersonId).Distinct().CountAsync();

        // сначала лёгкая выборка (id, дата, порог), подробности берём только для пяти ближайших
        var pending = await RetrainingQuery.Pending(_db)
            .Select(e => new { e.Id, Next = e.NextTrainingDate!.Value, Notify = e.Group!.Program!.NotifyDaysBefore })
            .ToListAsync();

        var scored = pending.Select(p =>
        {
            var left = p.Next.DayNumber - today.DayNumber;
            return (p.Id, p.Next, Left: left, Urgency: RetrainingRules.GetUrgency(left, p.Notify ?? TrainingProgram.DefaultNotifyDaysBefore));
        }).ToList();

        RedCount = scored.Count(s => s.Urgency == Urgency.Red);
        YellowCount = scored.Count(s => s.Urgency == Urgency.Yellow);

        var top = scored.Where(s => s.Urgency != Urgency.Green).OrderBy(s => s.Next).Take(5).ToList();
        var ids = top.Select(t => t.Id).ToList();
        var details = await _db.Enrollments.AsNoTracking().Where(e => ids.Contains(e.Id))
            .Select(e => new
            {
                e.Id, e.PersonId, e.Person!.LastName, e.Person.FirstName, e.Person.MiddleName,
                ProgramName = e.Group!.Program!.Name
            }).ToDictionaryAsync(x => x.Id);

        NearestDue = top.Where(t => details.ContainsKey(t.Id)).Select(t =>
        {
            var d = details[t.Id];
            return new DueRow(d.PersonId, Fmt.FullName(d.LastName, d.FirstName, d.MiddleName), d.ProgramName, t.Next, t.Left, t.Urgency);
        }).ToList();

        ActiveGroups = await _db.Groups.AsNoTracking()
            .Where(g => g.Status == GroupStatus.Recruiting || g.Status == GroupStatus.InProgress)
            .OrderBy(g => g.StartDate).Take(5)
            .Select(g => new GroupRow(g.Id, g.Name, g.Program!.Name, g.StartDate, g.Status,
                g.Enrollments.Count(e => e.Status != EnrollmentStatus.Expelled)))
            .ToListAsync();
    }
}
