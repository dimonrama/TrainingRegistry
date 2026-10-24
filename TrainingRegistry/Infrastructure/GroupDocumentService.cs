using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;
using TrainingRegistry.Infrastructure.Documents;

namespace TrainingRegistry.Infrastructure;

// данные, которые пользователь вводит вручную при формировании документов группы
public class DocForm
{
    public string? Number { get; set; }
    public DateOnly? Date { get; set; }
    public string? Hours { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? Curator { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateOnly? AttestationDate { get; set; }
    public DateOnly? ExpelDate { get; set; }
    public bool AllMembers { get; set; }
    public int? EnrollmentId { get; set; }
    public string? Reason { get; set; }
    public bool MarkExpelled { get; set; } = true;
    public string? OrderNumber { get; set; }
    public DateOnly? OrderDate { get; set; }
    public string? Commission { get; set; }
    public string? Secretary { get; set; }
}

public class GenerateResult
{
    public GeneratedDocument? Document { get; set; }
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool Ok => Errors.Count == 0 && Document != null;
}

public record GeneratedFile(byte[] Bytes, string FileName, string ContentType);

// формирование приказов, протокола, ведомости и списков по выбранной группе
public class GroupDocumentService
{
    public const string DocxType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string TemplateEnrollment = "enrollment-order.docx";
    private const string TemplateCompletion = "completion-order.docx";
    private const string TemplateProtocol = "attestation-protocol.docx";

    private readonly AppDbContext _db;
    private readonly IFileStorage _storage;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public GroupDocumentService(AppDbContext db, IFileStorage storage, IConfiguration config, IWebHostEnvironment env)
    {
        _db = db;
        _storage = storage;
        _config = config;
        _env = env;
    }

    public record Member(int EnrollmentId, int PersonId, string LastName, string FirstName, string? MiddleName,
        string? Organization, string? Position, EnrollmentStatus Status, AttestationResult? Grade,
        string? CertificateNumber)
    {
        public string FullName => Fmt.FullName(LastName, FirstName, MiddleName);
        public Listener ToListener(bool withGrade = false)
            => new(FullName, Organization, Position, withGrade ? Grade?.GetDisplayName() : null);
    }

    public record GroupData(TrainingGroup Group, TrainingProgram Program, List<Member> Members);

    public async Task<GroupData?> LoadAsync(int groupId)
    {
        var group = await _db.Groups.AsNoTracking().Include(g => g.Program).FirstOrDefaultAsync(g => g.Id == groupId);
        if (group == null) return null;

        var rows = await _db.Enrollments.AsNoTracking()
            .Where(e => e.GroupId == groupId)
            .OrderBy(e => e.Person!.LastName).ThenBy(e => e.Person!.FirstName)
            .Select(e => new
            {
                e.Id, e.PersonId, e.Person!.LastName, e.Person.FirstName, e.Person.MiddleName,
                Org = e.Person.Organization != null ? (e.Person.Organization.ShortName ?? e.Person.Organization.FullName) : null,
                e.Person.Position, e.Status, e.Grade, e.CertificateNumber
            })
            .ToListAsync();
        var members = rows.Select(r => new Member(r.Id, r.PersonId, r.LastName, r.FirstName, r.MiddleName, r.Org,
            r.Position, r.Status, r.Grade, r.CertificateNumber)).ToList();
        return new GroupData(group, group.Program!, members);
    }

    // ---- шаблоны ----

    // сначала смотрим в папку шаблонов (её можно заменить своими файлами), потом берём встроенные
    private byte[] Template(string fileName)
    {
        var folder = _config["Storage:TemplatesFolder"];
        if (string.IsNullOrWhiteSpace(folder)) folder = Path.Combine(_env.ContentRootPath, "App_Data", "templates");
        var custom = Path.Combine(folder, fileName);
        if (File.Exists(custom)) return File.ReadAllBytes(custom);

        using var stream = typeof(GroupDocumentService).Assembly.GetManifestResourceStream("Templates." + fileName)
                           ?? throw new InvalidOperationException($"Шаблон {fileName} не найден");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    // ---- приказы, протокол, ведомость ----

    public async Task<GenerateResult> GenerateAsync(int groupId, GeneratedDocKind kind, DocForm form)
    {
        var result = new GenerateResult();
        var data = await LoadAsync(groupId);
        if (data == null)
        {
            result.Errors.Add("Группа не найдена");
            return result;
        }
        var (group, program) = (data.Group, data.Program);

        var number = form.Number?.Trim();
        var regNumber = Pick(form.RegistrationNumber, group.RegistrationNumber);
        var curator = Pick(form.Curator, group.CuratorName);
        var hours = Pick(form.Hours, program.AcademicHours?.ToString());
        byte[]? bytes = null;
        int? personId = null;
        DateOnly? docDate = form.Date;

        switch (kind)
        {
            case GeneratedDocKind.EnrollmentOrder:
            {
                RequireNumberAndDate(form, result);
                var listeners = data.Members.Where(m => m.Status != EnrollmentStatus.Expelled).ToList();
                if (listeners.Count == 0) result.Errors.Add("В группе нет слушателей, приказ формировать не из кого");
                if (hours == null) result.Warnings.Add("Не указано количество академических часов — в приказе останется пустое место.");
                if (regNumber == null) result.Warnings.Add("Не указан регистрационный номер группы — в приказе останется пустое место.");
                if (curator == null) result.Warnings.Add("Не указан куратор группы — в приказе останется пустое место.");
                WarnAboutMissingData(listeners, result);
                if (result.Errors.Count > 0) return result;

                bytes = OrderDocuments.EnrollmentOrder(Template(TemplateEnrollment), new EnrollmentOrderData(
                    number!, form.Date!.Value, program.Name, hours, form.StartDate ?? group.StartDate,
                    form.EndDate ?? group.EndDate, regNumber, curator, listeners.Select(m => m.ToListener()).ToList()));
                break;
            }
            case GeneratedDocKind.CompletionOrder:
            {
                RequireNumberAndDate(form, result);
                if (form.ExpelDate == null) result.Errors.Add("Укажите дату отчисления");
                var listeners = data.Members
                    .Where(m => form.AllMembers ? m.Status != EnrollmentStatus.Expelled : m.Status == EnrollmentStatus.Completed)
                    .ToList();
                if (listeners.Count == 0)
                    result.Errors.Add("Нет слушателей со статусом «Прошел обучение». Отметьте их в составе группы " +
                                      "или включите всех слушателей группы.");
                if (hours == null) result.Warnings.Add("Не указано количество академических часов — в приказе останется пустое место.");
                if (form.AttestationDate == null) result.Warnings.Add("Не указана дата итоговой аттестации — в приказе останется пустое место.");
                if (result.Errors.Count > 0) return result;

                bytes = OrderDocuments.CompletionOrder(Template(TemplateCompletion), new CompletionOrderData(
                    number!, form.Date!.Value, program.Name, hours, form.AttestationDate, form.ExpelDate!.Value,
                    regNumber ?? group.Name, curator, listeners.Select(m => m.ToListener()).ToList()));
                break;
            }
            case GeneratedDocKind.OtherExpulsionOrder:
            {
                RequireNumberAndDate(form, result);
                if (form.ExpelDate == null) result.Errors.Add("Укажите дату отчисления");
                if (string.IsNullOrWhiteSpace(form.Reason)) result.Errors.Add("Укажите причину отчисления");
                else if (form.Reason.Trim().Length > 500) result.Errors.Add("Причина отчисления слишком длинная (не более 500 знаков)");
                var member = data.Members.FirstOrDefault(m => m.EnrollmentId == form.EnrollmentId);
                if (member == null) result.Errors.Add("Выберите слушателя, которого отчисляют");
                else if (member.Status == EnrollmentStatus.Completed)
                    result.Errors.Add("Слушатель уже прошёл обучение — отчислять его по иным основаниям нельзя");
                else if (member.Status == EnrollmentStatus.Expelled && form.MarkExpelled)
                    result.Warnings.Add("Слушатель уже отчислен, статус не меняется.");
                if (result.Errors.Count > 0) return result;

                bytes = OrderDocuments.OtherExpulsionOrder(Template(TemplateCompletion), new OtherExpulsionData(
                    number!, form.Date!.Value, program.Name, regNumber ?? group.Name, form.ExpelDate!.Value,
                    form.Reason!.Trim(), member!.ToListener()));
                personId = member.PersonId;

                if (form.MarkExpelled && member.Status != EnrollmentStatus.Expelled)
                {
                    var enrollment = await _db.Enrollments.FirstAsync(e => e.Id == member.EnrollmentId);
                    enrollment.Status = EnrollmentStatus.Expelled;
                    enrollment.ExpelledDate = form.ExpelDate;
                    enrollment.ExpelReason = form.Reason!.Trim();
                    enrollment.CompletedDate = null;
                    enrollment.NextTrainingDate = null;
                }
                break;
            }
            case GeneratedDocKind.Protocol:
            {
                if (string.IsNullOrWhiteSpace(number)) result.Errors.Add("Укажите номер протокола");
                if (form.Date == null) result.Errors.Add("Укажите дату заседания комиссии");

                var commission = SplitLines(Pick(form.Commission, group.CommissionMembers));
                var secretary = Pick(form.Secretary, group.CommissionSecretary);
                var active = data.Members.Where(m => m.Status != EnrollmentStatus.Expelled).ToList();
                var passed = active.Where(m => AttestationRules.IsSuccessful(m.Grade)).ToList();
                foreach (var m in active.Except(passed))
                    result.Warnings.Add($"{m.FullName}: результат «{m.Grade?.GetDisplayName()}» — в протокол на выдачу удостоверения не включён.");
                if (passed.Count == 0) result.Errors.Add("В протокол некого включать: нет слушателей с положительным результатом аттестации");
                if (passed.Count > 0 && passed.All(m => m.Grade == null))
                    result.Warnings.Add("Оценки слушателям не внесены — колонка «Оценка» останется пустой.");
                if (commission.Count == 0) result.Warnings.Add("Не указаны члены комиссии — строки для них останутся пустыми.");
                if (secretary == null) result.Warnings.Add("Не указан секретарь комиссии — в протоколе останется пустое место.");
                if (result.Errors.Count > 0) return result;

                bytes = OrderDocuments.Protocol(Template(TemplateProtocol), new ProtocolData(
                    number!.Trim(), form.Date!.Value, form.OrderNumber, form.OrderDate, program.Name, secretary,
                    commission, passed.Select(m => m.ToListener(withGrade: true)).ToList()));

                // состав комиссии запоминаем в группе, чтобы в следующий раз не вводить заново
                if (string.IsNullOrWhiteSpace(group.CommissionMembers) && commission.Count > 0
                    || string.IsNullOrWhiteSpace(group.CommissionSecretary) && secretary != null)
                {
                    var tracked = await _db.Groups.FirstAsync(g => g.Id == groupId);
                    if (string.IsNullOrWhiteSpace(tracked.CommissionMembers) && commission.Count > 0)
                        tracked.CommissionMembers = string.Join("\n", commission);
                    if (string.IsNullOrWhiteSpace(tracked.CommissionSecretary)) tracked.CommissionSecretary = secretary;
                }
                break;
            }
            case GeneratedDocKind.AttestationSheet:
            {
                docDate ??= Fmt.Today();
                var active = data.Members.Where(m => m.Status != EnrollmentStatus.Expelled).ToList();
                if (active.Count == 0) result.Errors.Add("В группе нет слушателей");
                if (result.Errors.Count > 0) return result;
                bytes = DocxRenderer.Render(SheetBlocks(data, active, docDate.Value, hours, regNumber));
                break;
            }
            default:
                result.Errors.Add("Неизвестный вид документа");
                return result;
        }

        // то, что ввели вручную и чего ещё нет в группе, запоминаем в группе
        var trackedGroup = await _db.Groups.FirstAsync(g => g.Id == groupId);
        if (string.IsNullOrWhiteSpace(trackedGroup.RegistrationNumber) && regNumber != null && regNumber != group.Name)
            trackedGroup.RegistrationNumber = regNumber;
        if (string.IsNullOrWhiteSpace(trackedGroup.CuratorName) && curator != null) trackedGroup.CuratorName = curator;

        var title = kind.GetDisplayName() + (string.IsNullOrWhiteSpace(number) ? "" : $" № {number.Trim()}")
                    + (docDate != null ? $" от {RuDate.Numeric(docDate.Value)}" : "");
        var fileName = SafeFileName(title) + ".docx";
        var path = await _storage.SaveBytesAsync(bytes!, $"documents/{groupId}", ".docx");

        var doc = new GeneratedDocument
        {
            Kind = kind,
            GroupId = groupId,
            PersonId = personId,
            Number = string.IsNullOrWhiteSpace(number) ? null : number.Trim(),
            DocDate = docDate,
            FileName = fileName,
            FilePath = path
        };
        _db.GeneratedDocuments.Add(doc);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch
        {
            // запись не сохранилась - лишний файл не оставляем
            _storage.Delete(path);
            throw;
        }
        result.Document = doc;
        return result;
    }

    private static void RequireNumberAndDate(DocForm form, GenerateResult result)
    {
        if (string.IsNullOrWhiteSpace(form.Number)) result.Errors.Add("Укажите номер приказа");
        else if (form.Number.Trim().Length > 20) result.Errors.Add("Номер приказа слишком длинный (не более 20 знаков)");
        if (form.Date == null) result.Errors.Add("Укажите дату приказа");
    }

    private static void WarnAboutMissingData(List<Member> listeners, GenerateResult result)
    {
        var noOrg = listeners.Count(m => string.IsNullOrWhiteSpace(m.Organization));
        var noPos = listeners.Count(m => string.IsNullOrWhiteSpace(m.Position));
        if (noOrg > 0) result.Warnings.Add($"У слушателей без организации ({noOrg}) в списке будет пустая ячейка.");
        if (noPos > 0) result.Warnings.Add($"У слушателей без должности ({noPos}) в списке будет пустая ячейка.");
    }

    private static string? Pick(string? first, string? second)
        => !string.IsNullOrWhiteSpace(first) ? first.Trim() : string.IsNullOrWhiteSpace(second) ? null : second.Trim();

    public static List<string> SplitLines(string? text)
        => (text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static string SafeFileName(string name)
    {
        var cleaned = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return cleaned.Length > 120 ? cleaned[..120] : cleaned;
    }

    // ---- ведомость (образца от заказчика пока нет, поэтому простая форма) ----

    private static List<Block> SheetBlocks(GroupData data, List<Member> members, DateOnly date, string? hours, string? regNumber)
    {
        var info = new List<string> { $"Программа: «{data.Program.Name}»" };
        if (hours != null) info.Add($"Объём: {hours} академических часов");
        info.Add($"Группа: {regNumber ?? data.Group.Name}");
        info.Add($"Период обучения: {Fmt.Date(data.Group.StartDate)} — {Fmt.Date(data.Group.EndDate)}");

        var rows = members.Select((m, i) => new[]
        {
            (i + 1).ToString(), m.FullName, m.Organization ?? "", m.Grade?.GetDisplayName() ?? "", ""
        }).ToList();

        return new List<Block>
        {
            new TitleBlock("ВЕДОМОСТЬ", "итоговой аттестации"),
            new LineBlock("", $"Дата: {RuDate.Numeric(date)}"),
            new ParaBlock(string.Join("\n", info)),
            new TableBlock(new[] { "№ п/п", "Фамилия, имя, отчество", "Организация", "Оценка", "Подпись" }, rows,
                new[] { 1.0, 5.0, 3.5, 2.2, 2.0 }),
            new ParaBlock("Председатель аттестационной комиссии ____________________\n" +
                          "Секретарь комиссии ____________________")
        };
    }

    // ---- списки, которые не сохраняем, а сразу отдаём файлом ----

    public async Task<GeneratedFile?> BuildListAsync(int groupId, string format)
    {
        var data = await LoadAsync(groupId);
        if (data == null) return null;
        var members = data.Members.Where(m => m.Status != EnrollmentStatus.Expelled).ToList();
        var baseName = SafeFileName($"Список слушателей {data.Group.Name}");

        if (string.Equals(format, "docx", StringComparison.OrdinalIgnoreCase))
        {
            var rows = members.Select((m, i) => new[] { (i + 1).ToString(), m.FullName, m.Organization ?? "", m.Position ?? "" }).ToList();
            var blocks = new List<Block>
            {
                new TitleBlock("СПИСОК СЛУШАТЕЛЕЙ", $"по программе «{data.Program.Name}»"),
                new ParaBlock($"Группа: {data.Group.RegistrationNumber ?? data.Group.Name}\n" +
                              $"Период обучения: {Fmt.Date(data.Group.StartDate)} — {Fmt.Date(data.Group.EndDate)}"),
                new TableBlock(new[] { "№ п/п", "Фамилия, имя, отчество", "Организация", "Должность" }, rows,
                    new[] { 1.0, 5.0, 3.5, 3.5 })
            };
            return new GeneratedFile(DocxRenderer.Render(blocks), baseName + ".docx", DocxType);
        }

        var bytes = XlsxWriter.Create("Список слушателей",
            new[] { "№ п/п", "Фамилия, имя, отчество", "Организация", "Должность" },
            members.Select((m, i) => new object?[] { i + 1, m.FullName, m.Organization, m.Position }));
        return new GeneratedFile(bytes, baseName + ".xlsx", XlsxWriter.ContentType);
    }
}
