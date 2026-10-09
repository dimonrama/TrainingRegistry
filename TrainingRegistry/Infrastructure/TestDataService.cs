using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;

namespace TrainingRegistry.Infrastructure;

public class TestDataService
{
    // по этой метке находим тестовые записи, чтобы потом удалить
    public const string Marker = "[ТЕСТ]";
    public const string EmailDomain = "@test.local";
    private readonly AppDbContext _db;
    public TestDataService(AppDbContext db) => _db = db;

    private static readonly string[] LastNames =
    {
        "Иванов", "Петров", "Сидоров", "Смирнов", "Кузнецов", "Попов", "Васильев", "Соколов", "Михайлов", "Новиков",
        "Фёдоров", "Морозов", "Волков", "Алексеев", "Лебедев", "Семёнов", "Егоров", "Павлов", "Козлов", "Степанов",
        "Николаев", "Орлов", "Андреев", "Макаров", "Никитин", "Захаров", "Зайцев", "Соловьёв", "Борисов", "Яковлев"
    };
    private static readonly string[] MaleNames = { "Александр", "Алексей", "Андрей", "Дмитрий", "Евгений", "Иван", "Максим", "Михаил", "Николай", "Сергей" };
    private static readonly string[] FemaleNames = { "Анна", "Елена", "Ирина", "Мария", "Наталья", "Ольга", "Светлана", "Татьяна", "Юлия", "Екатерина" };
    private static readonly string[] MalePatronymics = { "Александрович", "Андреевич", "Викторович", "Дмитриевич", "Иванович", "Николаевич", "Петрович", "Сергеевич" };
    private static readonly string[] FemalePatronymics = { "Александровна", "Андреевна", "Викторовна", "Дмитриевна", "Ивановна", "Николаевна", "Петровна", "Сергеевна" };
    private static readonly string[] Positions =
    {
        "Инженер", "Мастер участка", "Электромонтёр", "Слесарь", "Начальник цеха", "Специалист по охране труда",
        "Механик", "Сварщик", "Водитель", "Бухгалтер", "Менеджер", "Оператор"
    };
    private static Person NewPerson(Random rnd, int? orgId, bool testMark, int n)
    {
        var female = rnd.Next(2) == 0;
        var last = LastNames[rnd.Next(LastNames.Length)] + (female ? "а" : "");
        return new Person
        {
            LastName = last,
            FirstName = (female ? FemaleNames : MaleNames)[rnd.Next(10)],
            MiddleName = (female ? FemalePatronymics : MalePatronymics)[rnd.Next(8)],
            BirthDate = new DateOnly(rnd.Next(1960, 2002), rnd.Next(1, 13), rnd.Next(1, 28)),
            Phone = $"+7 9{rnd.Next(10, 99)} {rnd.Next(100, 999)}-{rnd.Next(10, 99)}-{rnd.Next(10, 99)}",
            Email = testMark ? $"u{n}{EmailDomain}" : null,
            OrganizationId = orgId,
            Position = orgId == null ? null : Positions[rnd.Next(Positions.Length)],
            Status = PersonStatus.Active
        };
    }
    // демо-данные добавляем только в пустую систему
    public async Task<bool> SeedDemoAsync()
    {
        if (await _db.Organizations.AnyAsync() || await _db.Programs.AnyAsync()) return false;

        var today = Fmt.Today();
        var rnd = new Random(2026);
        var programs = new[]
        {
            Program("Охрана труда для руководителей и специалистов", 4500, Recurrence.ThreeYears, 40, RequiredEducation.None, null),
            Program("Пожарно-технический минимум", 2500, Recurrence.OneYear, 16, RequiredEducation.None, null),
            Program("Электробезопасность (III группа)", 3800, Recurrence.OneYear, 24, RequiredEducation.SecondaryVocational, null),
            Program("Работы на высоте", 3200, Recurrence.ThreeYears, 16, RequiredEducation.None, 45),
            Program("Первая помощь пострадавшим", 1500, Recurrence.ThreeYears, 8, RequiredEducation.None, null),
            Program("Промышленная безопасность", 9500, Recurrence.FiveYears, 72, RequiredEducation.Higher, 90)
        };
        _db.Programs.AddRange(programs);
        var orgs = new[]
        {
            Org("Общество с ограниченной ответственностью «Северный ветер»", "ООО «Северный ветер»", "7701234567", "770101001", "Иванова Ольга Петровна", "Директор"),
            Org("Акционерное общество «Волгамаш»", "АО «Волгамаш»", "6312345678", "631201001", "Кузьмин Андрей Сергеевич", "Начальник отдела кадров"),
            Org("Общество с ограниченной ответственностью «СтройИнвест»", "ООО «СтройИнвест»", "5024567890", "502401001", "Лукина Марина Викторовна", "Специалист по охране труда"),
            Org("Муниципальное унитарное предприятие «Горводоканал»", "МУП «Горводоканал»", "6501122334", "650101001", "Рябов Игорь Николаевич", "Главный инженер"),
            Org("Общество с ограниченной ответственностью «Альфа-Сервис»", "ООО «Альфа-Сервис»", "7812345670", "781201001", "Тихонова Елена Андреевна", "Менеджер по персоналу")
        };
        _db.Organizations.AddRange(orgs);
        await _db.SaveChangesAsync();
        var people = new List<Person>();
        var n = 0;
        foreach (var org in orgs)
            for (var i = 0; i < 6; i++) people.Add(NewPerson(rnd, org.Id, false, n++));
        for (var i = 0; i < 3; i++) people.Add(NewPerson(rnd, null, false, n++));
        people[4].Status = PersonStatus.Dismissed;
        people[4].DismissalDate = today.AddDays(-40);
        _db.People.AddRange(people);
        await _db.SaveChangesAsync();
        foreach (var p in people.Take(18))
        {
            _db.Educations.Add(new Education
            {
                PersonId = p.Id,
                Level = p.Id % 3 == 0 ? EducationLevel.Higher : EducationLevel.SecondaryVocational,
                InstitutionName = p.Id % 3 == 0 ? "Технический университет" : "Политехнический колледж",
                GraduationYear = rnd.Next(1990, 2020),
                Specialty = "Техническая эксплуатация оборудования"
            });
        }
        var pastGroups = programs.Select(pr => new TrainingGroup
        {
            Name = $"{pr.Name} — архивная группа",
            Program = pr,
            StartDate = today.AddDays(-700),
            EndDate = today.AddDays(-690),
            Status = GroupStatus.Completed
        }).ToList();
        _db.Groups.AddRange(pastGroups);
        await _db.SaveChangesAsync();
        var plan = new (int Person, int Program, int DaysLeft)[]
        {
            (0, 1, -12), (1, 1, 14), (2, 2, 33), (3, 2, 58), (5, 0, 45), (6, 0, 72), (7, 3, 80),
            (8, 4, 88), (9, 4, 150), (10, 5, 20), (11, 5, 300), (12, 1, 200), (13, 2, 365),
            (14, 0, 400), (15, 3, 55), (16, 1, 5), (17, 2, 61), (18, 4, 25), (19, 0, 700),
            (20, 1, 130), (21, 2, 10), (22, 0, 90), (23, 4, 1000)
        };
        foreach (var (pi, pr, left) in plan)
        {
            var program = programs[pr];
            var months = program.GetRecurrenceMonths()!.Value;
            var completed = today.AddDays(left).AddMonths(-months);
            var e = new Enrollment
            {
                PersonId = people[pi].Id,
                GroupId = pastGroups[pr].Id,
                CertificateNumber = $"УД-{2026 - months / 12}-{pi + 100}"
            };
            e.MarkCompleted(completed, program);
            _db.Enrollments.Add(e);
        }
        var current = new[]
        {
            new TrainingGroup { Name = "Охрана труда — октябрь", Program = programs[0], StartDate = today.AddDays(-3), EndDate = today.AddDays(4), Status = GroupStatus.InProgress },
            new TrainingGroup { Name = "Электробезопасность — ноябрь", Program = programs[2], StartDate = today.AddDays(25), Status = GroupStatus.Recruiting },
            new TrainingGroup { Name = "Первая помощь — декабрь", Program = programs[4], StartDate = today.AddDays(60), Status = GroupStatus.Recruiting }
        };
        _db.Groups.AddRange(current);
        await _db.SaveChangesAsync();
        var free = people.Where((p, i) => !plan.Any(x => x.Person == i) && p.Status == PersonStatus.Active).ToList();
        for (var i = 0; i < free.Count; i++)
        {
            var g = current[i % 3];
            _db.Enrollments.Add(new Enrollment
            {
                PersonId = free[i].Id,
                GroupId = g.Id,
                Status = g.Status == GroupStatus.InProgress ? EnrollmentStatus.Studying : EnrollmentStatus.Enrolled
            });
        }
        await _db.SaveChangesAsync();
        return true;
        static TrainingProgram Program(string name, decimal price, Recurrence rec, int hours, RequiredEducation edu, int? notify) => new()
        {
            Name = name, Price = price, Recurrence = rec, Duration = hours, DurationUnit = DurationUnit.Hours,
            RequiredEducation = edu, NotifyDaysBefore = notify, Description = "Демонстрационная программа"
        };
        static Organization Org(string full, string shortName, string inn, string kpp, string contact, string position) => new()
        {
            FullName = full, ShortName = shortName, Inn = inn, Kpp = kpp,
            LegalAddress = "г. Москва, ул. Строителей, д. 10", Phone = "+7 495 123-45-67",
            Email = $"info{inn[^3..]}@example.com", ContactName = contact, ContactPosition = position
        };
    }
    // массовая загрузка, чтобы проверить скорость на 10 000 записей
    public async Task<int> AddLoadAsync(int peopleCount)
    {
        var today = Fmt.Today();
        var rnd = new Random(Environment.TickCount);
        var start = await _db.People.CountAsync();
        var programs = await _db.Programs.ToListAsync();
        if (programs.Count == 0)
        {
            programs = new List<TrainingProgram>
            {
                new() { Name = "Охрана труда (тест)", Price = 4500, Recurrence = Recurrence.ThreeYears, Duration = 40, Description = Marker },
                new() { Name = "Пожарно-технический минимум (тест)", Price = 2500, Recurrence = Recurrence.OneYear, Duration = 16, Description = Marker },
                new() { Name = "Электробезопасность (тест)", Price = 3800, Recurrence = Recurrence.OneYear, Duration = 24, Description = Marker }
            };
            _db.Programs.AddRange(programs);
            await _db.SaveChangesAsync();
        }
        var orgCount = Math.Max(10, peopleCount / 30);
        var orgs = Enumerable.Range(1, orgCount).Select(i => new Organization
        {
            FullName = $"Тестовая организация № {start + i}",
            ShortName = $"ООО «Тест-{start + i}»",
            Inn = (7700000000L + start + i).ToString(),
            Note = Marker
        }).ToList();
        _db.Organizations.AddRange(orgs);
        var groups = new List<TrainingGroup>();
        for (var i = 0; i < 40; i++)
        {
            var program = programs[i % programs.Count];
            var inProgress = i < 4;
            groups.Add(new TrainingGroup
            {
                Name = $"Тестовая группа {start + i + 1}",
                ProgramId = program.Id,
                StartDate = today.AddDays(inProgress ? -2 : -rnd.Next(30, 1000)),
                EndDate = today.AddDays(inProgress ? 5 : 0),
                Status = inProgress ? GroupStatus.InProgress : GroupStatus.Completed,
                Note = Marker
            });
        }
        _db.Groups.AddRange(groups);
        await _db.SaveChangesAsync();
        var programById = programs.ToDictionary(p => p.Id);
        for (var offset = 0; offset < peopleCount; offset += 1000)
        {
            var batch = Math.Min(1000, peopleCount - offset);
            for (var i = 0; i < batch; i++)
            {
                var person = NewPerson(rnd, orgs[rnd.Next(orgs.Count)].Id, true, start + offset + i + 1);
                if (rnd.Next(20) == 0)
                {
                    person.Status = PersonStatus.Dismissed;
                    person.DismissalDate = today.AddDays(-rnd.Next(1, 300));
                }
                // каждому сотруднику 1-3 случайные группы
                foreach (var g in groups.OrderBy(_ => rnd.Next()).Take(rnd.Next(1, 4)))
                {
                    var e = new Enrollment { GroupId = g.Id, Note = Marker };
                    var roll = rnd.Next(100);
                    if (g.Status == GroupStatus.InProgress)
                        e.Status = EnrollmentStatus.Studying;
                    else if (roll < 3)
                        e.Status = EnrollmentStatus.Expelled;
                    else
                        e.MarkCompleted(g.StartDate.AddDays(rnd.Next(0, 10)), programById[g.ProgramId]);
                    person.Enrollments.Add(e);
                }
                _db.People.Add(person);
            }
            await _db.SaveChangesAsync();
            // пачками по 1000 и чистим трекер, иначе всё сильно тормозит
            _db.ChangeTracker.Clear();
        }

        return peopleCount;
    }
    // удаляем только то, что помечено как тестовое
    public async Task<int> RemoveLoadAsync()
    {
        var people = _db.People.IgnoreQueryFilters().Where(p => p.Email != null && p.Email.EndsWith(EmailDomain));
        var groups = _db.Groups.IgnoreQueryFilters().Where(g => g.Note == Marker);
        var removed = await people.CountAsync();
        await _db.Enrollments.IgnoreQueryFilters()
            .Where(e => e.Note == Marker || people.Select(p => p.Id).Contains(e.PersonId) || groups.Select(g => g.Id).Contains(e.GroupId))
            .ExecuteDeleteAsync();
        await _db.Educations.IgnoreQueryFilters().Where(e => people.Select(p => p.Id).Contains(e.PersonId)).ExecuteDeleteAsync();
        await people.ExecuteDeleteAsync();
        await groups.ExecuteDeleteAsync();
        await _db.Programs.IgnoreQueryFilters().Where(p => p.Description == Marker).ExecuteDeleteAsync();
        await _db.Organizations.IgnoreQueryFilters().Where(o => o.Note == Marker).ExecuteDeleteAsync();
        return removed;
    }
    public async Task<int> CountLoadPeopleAsync()
        => await _db.People.IgnoreQueryFilters().CountAsync(p => p.Email != null && p.Email.EndsWith(EmailDomain));
}
