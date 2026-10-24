using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;

namespace TrainingRegistry.Infrastructure;

public enum ImportRowStatus { Ok, Duplicate, Error }

public class ImportPreviewRow
{
    public ParsedRow Row { get; init; } = null!;
    public ImportRowStatus Status { get; set; } = ImportRowStatus.Ok;
    public int? OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
    // тексты проблем: «Поле: что не так»
    public List<string> Problems { get; } = new();
    // с кем совпало в базе
    public List<PersonDuplicateService.Match> Duplicates { get; } = new();
}

public class ImportPreview
{
    public List<string> StructureErrors { get; } = new();
    public List<ImportPreviewRow> Rows { get; } = new();
    public int OkCount => Rows.Count(r => r.Status == ImportRowStatus.Ok);
    public int DuplicateCount => Rows.Count(r => r.Status == ImportRowStatus.Duplicate);
    public int ErrorCount => Rows.Count(r => r.Status == ImportRowStatus.Error);
}

public record ImportResult(int Added, int SkippedDuplicates, List<string> Errors);

// массовая загрузка обучающихся из Excel: разбор, проверка, предпросмотр и сама загрузка
public class ImportService
{
    private readonly AppDbContext _db;
    private readonly PersonDuplicateService _duplicates;

    public ImportService(AppDbContext db, PersonDuplicateService duplicates)
    {
        _db = db;
        _duplicates = duplicates;
    }

    private static string Norm(string? s) => (s ?? "").Trim().ToLowerInvariant().Replace('ё', 'е');

    public async Task<ImportPreview> BuildPreviewAsync(byte[] file)
    {
        var preview = new ImportPreview();

        List<XlsxRow> sheet;
        try
        {
            sheet = XlsxReader.Read(file);
        }
        catch (InvalidDataException ex)
        {
            preview.StructureErrors.Add(ex.Message);
            return preview;
        }
        catch (System.Xml.XmlException)
        {
            preview.StructureErrors.Add("Файл повреждён или это не книга Excel (.xlsx)");
            return preview;
        }

        var parsed = ImportParser.Parse(sheet);
        preview.StructureErrors.AddRange(parsed.StructureErrors);
        if (preview.StructureErrors.Count > 0) return preview;

        var orgs = await _db.Organizations.AsNoTracking()
            .Select(o => new { o.Id, o.FullName, o.ShortName, o.Inn })
            .ToListAsync();
        var byInn = new Dictionary<string, int>();
        var byName = new Dictionary<string, List<int>>();
        var orgNames = new Dictionary<int, string>();
        foreach (var o in orgs)
        {
            if (!string.IsNullOrWhiteSpace(o.Inn)) byInn.TryAdd(o.Inn.Trim(), o.Id);
            foreach (var name in new[] { o.FullName, o.ShortName })
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                var key = Norm(name);
                if (!byName.TryGetValue(key, out var ids)) byName[key] = ids = new List<int>();
                if (!ids.Contains(o.Id)) ids.Add(o.Id);
            }
            orgNames[o.Id] = o.ShortName ?? o.FullName;
        }

        var index = await _duplicates.LoadIndexAsync();
        var fileKeys = new List<(PersonKey Key, int RowNumber)>();

        foreach (var row in parsed.Rows)
        {
            var item = new ImportPreviewRow { Row = row };
            foreach (var e in row.Errors) item.Problems.Add($"{e.Field}: {e.Message}");

            ResolveOrganization(row, item, byInn, byName, orgNames);

            if (item.Problems.Count > 0)
            {
                item.Status = ImportRowStatus.Error;
                preview.Rows.Add(item);
                continue;
            }

            var key = row.ToKey() with { Id = -row.RowNumber };

            // совпадения с тем, что уже есть в системе
            foreach (var (person, reasons) in index.Find(key))
                item.Duplicates.Add(new PersonDuplicateService.Match(person.Id, person.FullName, null, person.BirthDate,
                    person.Snils, reasons));

            // совпадения с более ранними строками этого же файла
            foreach (var (other, line) in fileKeys)
            {
                var reasons = DuplicateRules.Reasons(key, other);
                if (reasons.Count > 0)
                    item.Duplicates.Add(new PersonDuplicateService.Match(0, $"строка {line} этого файла", null,
                        other.BirthDate, other.Snils, reasons));
            }

            if (item.Duplicates.Count > 0) item.Status = ImportRowStatus.Duplicate;
            fileKeys.Add((key, row.RowNumber));
            preview.Rows.Add(item);
        }
        return preview;
    }

    private static void ResolveOrganization(ParsedRow row, ImportPreviewRow item, Dictionary<string, int> byInn,
        Dictionary<string, List<int>> byName, Dictionary<int, string> names)
    {
        if (row.OrganizationInn != null)
        {
            if (byInn.TryGetValue(row.OrganizationInn, out var id))
            {
                item.OrganizationId = id;
                item.OrganizationName = names[id];
                return;
            }
            // по ИНН не нашли, но есть название - попробуем по нему
            if (row.Organization == null)
            {
                item.Problems.Add($"Организация: организация с ИНН {row.OrganizationInn} не найдена. Сначала добавьте её в разделе «Организации»");
                return;
            }
        }

        if (row.Organization == null) return;

        if (byName.TryGetValue(Norm(row.Organization), out var ids))
        {
            if (ids.Count > 1)
            {
                item.Problems.Add($"Организация: под названием «{row.Organization}» найдено несколько организаций. Укажите ИНН");
                return;
            }
            item.OrganizationId = ids[0];
            item.OrganizationName = names[ids[0]];
            return;
        }
        item.Problems.Add($"Организация: «{row.Organization}» не найдена. Сначала добавьте её в разделе «Организации» или поправьте название");
    }

    public async Task<ImportResult> ImportAsync(byte[] file, bool includeDuplicates, string fileName)
    {
        var preview = await BuildPreviewAsync(file);
        if (preview.StructureErrors.Count > 0) return new ImportResult(0, 0, preview.StructureErrors);

        var toAdd = preview.Rows
            .Where(r => r.Status == ImportRowStatus.Ok || (includeDuplicates && r.Status == ImportRowStatus.Duplicate))
            .ToList();

        var errors = preview.Rows.Where(r => r.Status == ImportRowStatus.Error)
            .Select(r => $"Строка {r.Row.RowNumber}: {string.Join("; ", r.Problems)}")
            .ToList();
        var skippedDuplicates = includeDuplicates ? 0 : preview.DuplicateCount;

        foreach (var item in toAdd)
        {
            var r = item.Row;
            _db.People.Add(new Person
            {
                LastName = r.LastName,
                FirstName = r.FirstName,
                MiddleName = r.MiddleName,
                BirthDate = r.BirthDate,
                Snils = r.Snils,
                Phone = r.Phone,
                Email = r.Email,
                OrganizationId = item.OrganizationId,
                Position = r.Position,
                Status = PersonStatus.Active
            });
        }

        // по каждой записи в журнал не пишем, вместо этого одна общая строка
        var previous = _db.SuppressAudit;
        _db.SuppressAudit = true;
        try
        {
            await _db.SaveChangesAsync();
        }
        finally
        {
            _db.SuppressAudit = previous;
        }

        await _db.LogAsync("Импорт из Excel", nameof(Person), 0, fileName,
            $"Добавлено: {toAdd.Count}, с ошибками пропущено: {errors.Count}, возможных дублей пропущено: {skippedDuplicates}");
        return new ImportResult(toAdd.Count, skippedDuplicates, errors);
    }
}
