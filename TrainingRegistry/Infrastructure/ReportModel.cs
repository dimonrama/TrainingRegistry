using TrainingRegistry.Domain;

namespace TrainingRegistry.Infrastructure;

// одна строка отчёта = одно зачисление обучающегося в группу
public record ReportRow(int EnrollmentId, int PersonId, string LastName, string FirstName, string? MiddleName,
    DateOnly? BirthDate, string? Snils, string? Phone, string? Email, string? Organization, string? Position,
    string Program, string Group, string? RegistrationNumber, DateOnly GroupStart, DateOnly? GroupEnd,
    EnrollmentStatus Status, FundingSource? Funding, AttestationResult? Grade, string? CertificateNumber,
    DateOnly? CompletedDate, DateOnly? ExpelledDate, string? ExpelReason, DateOnly? NextTrainingDate)
{
    public string FullName => Fmt.FullName(LastName, FirstName, MiddleName);
}

public record ReportField(string Key, string Title, Func<ReportRow, object?> Value, bool Personal = false, bool Default = true);

public static class ReportFields
{
    public static readonly IReadOnlyList<ReportField> All = new List<ReportField>
    {
        new("name", "ФИО", r => r.FullName),
        new("birth", "Дата рождения", r => r.BirthDate, Personal: true, Default: false),
        new("snils", "СНИЛС", r => r.Snils, Personal: true, Default: false),
        new("phone", "Телефон", r => r.Phone, Personal: true, Default: false),
        new("email", "Электронная почта", r => r.Email, Personal: true, Default: false),
        new("org", "Организация", r => r.Organization ?? "Физическое лицо"),
        new("position", "Должность", r => r.Position),
        new("program", "Программа", r => r.Program),
        new("group", "Группа", r => r.Group),
        new("regnum", "Регистрационный номер группы", r => r.RegistrationNumber, Default: false),
        new("start", "Начало обучения", r => r.GroupStart),
        new("end", "Окончание обучения", r => r.GroupEnd),
        new("funding", "Финансирование", r => FundingRules.Short(r.Funding)),
        new("status", "Статус", r => r.Status.GetDisplayName()),
        new("grade", "Результат аттестации", r => r.Grade?.GetDisplayName(), Default: false),
        new("cert", "Номер удостоверения", r => r.CertificateNumber),
        new("completed", "Дата прохождения", r => r.CompletedDate),
        new("expelled", "Дата отчисления", r => r.ExpelledDate, Default: false),
        new("reason", "Причина отчисления", r => r.ExpelReason, Default: false),
        new("next", "Дата следующего обучения", r => r.NextTrainingDate, Default: false)
    };

    // набор для реестра выданных документов (формат уточняется у заказчика)
    public static readonly string[] RegisterKeys = { "name", "org", "program", "group", "cert", "completed" };

    // по ключам собираем поля в порядке списка; чужие ключи отбрасываем
    public static List<ReportField> Select(IEnumerable<string>? keys, bool allowPersonal)
    {
        var wanted = keys?.ToList();
        var list = wanted == null || wanted.Count == 0
            ? All.Where(f => f.Default).ToList()
            : All.Where(f => wanted.Contains(f.Key)).ToList();
        if (!allowPersonal) list = list.Where(f => !f.Personal).ToList();
        return list;
    }

    // СНИЛС в списках для тех, кому нельзя видеть документы: только последние цифры
    public static string? MaskSnils(string? snils)
    {
        var digits = Snils.Digits(snils);
        return digits.Length == 11 ? "***-***-*** " + digits[9..] : null;
    }
}

public record SummaryLine(string Key, int Total, int Studying, int Completed, int Expelled,
    int StateTask, int Business, int NoFunding);

public static class ReportSummary
{
    public static List<SummaryLine> By(IEnumerable<ReportRow> rows, Func<ReportRow, string> key)
        => rows.GroupBy(key).OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new SummaryLine(g.Key, g.Count(),
                g.Count(r => r.Status is EnrollmentStatus.Enrolled or EnrollmentStatus.Studying),
                g.Count(r => r.Status == EnrollmentStatus.Completed),
                g.Count(r => r.Status == EnrollmentStatus.Expelled),
                g.Count(r => r.Funding == FundingSource.StateTask),
                g.Count(r => r.Funding == FundingSource.Business),
                g.Count(r => r.Funding == null)))
            .ToList();

    public static List<SummaryLine> ByProgram(IEnumerable<ReportRow> rows) => By(rows, r => r.Program);
    public static List<SummaryLine> ByOrganization(IEnumerable<ReportRow> rows) => By(rows, r => r.Organization ?? "Физическое лицо");

    // период - месяц начала обучения группы, сортируем по дате, а не по тексту
    public static List<SummaryLine> ByMonth(IEnumerable<ReportRow> rows)
        => By(rows, r => r.GroupStart.ToString("yyyy-MM")).Select(l => l with { Key = MonthTitle(l.Key) }).ToList();

    private static readonly string[] Months =
        { "январь", "февраль", "март", "апрель", "май", "июнь", "июль", "август", "сентябрь", "октябрь", "ноябрь", "декабрь" };

    public static string MonthTitle(string yearMonth)
    {
        var parts = yearMonth.Split('-');
        return $"{Months[int.Parse(parts[1]) - 1]} {parts[0]}";
    }
}

// условия отбора: все необязательные и работают вместе
public class ReportFilter
{
    public string? Search { get; set; }
    public int? OrganizationId { get; set; }
    public int? ProgramId { get; set; }
    public int? GroupId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string? Certificate { get; set; }
    public FundingSource? Funding { get; set; }
    public EnrollmentStatus? Status { get; set; }
    public bool OnlyWithCertificate { get; set; }

    public string? Validate()
    {
        if (From != null && To != null && From > To) return "Дата начала периода позже даты окончания";
        return null;
    }
}
