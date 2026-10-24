using System.Globalization;
using System.Text.RegularExpressions;
using TrainingRegistry.Domain;

namespace TrainingRegistry.Infrastructure;

public enum ImportColumn
{
    FullName, LastName, FirstName, MiddleName, BirthDate, Snils, Phone, Email, Organization, OrganizationInn, Position
}

public record RowError(string Field, string Message);

public class ParsedRow
{
    public int RowNumber { get; init; }
    public string LastName { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string? MiddleName { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string? Snils { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Organization { get; set; }
    public string? OrganizationInn { get; set; }
    public string? Position { get; set; }
    public List<RowError> Errors { get; } = new();

    public string FullName => Fmt.FullName(LastName, FirstName, MiddleName);
    public PersonKey ToKey() => new(0, LastName, FirstName, MiddleName, BirthDate, Snils, Email);
}

public class ParseResult
{
    // проблемы всего файла: нет нужных колонок, пустой лист и т.п.
    public List<string> StructureErrors { get; } = new();
    public List<ParsedRow> Rows { get; } = new();
}

// разбор таблицы из Excel в строки для импорта; к базе не обращается
public static class ImportParser
{
    // заголовки шаблона для скачивания
    public static readonly (ImportColumn Column, string Title, bool Required)[] TemplateColumns =
    {
        (ImportColumn.LastName, "Фамилия", true),
        (ImportColumn.FirstName, "Имя", true),
        (ImportColumn.MiddleName, "Отчество", false),
        (ImportColumn.BirthDate, "Дата рождения", false),
        (ImportColumn.Snils, "СНИЛС", false),
        (ImportColumn.Phone, "Телефон", false),
        (ImportColumn.Email, "Электронная почта", false),
        (ImportColumn.Organization, "Организация", false),
        (ImportColumn.OrganizationInn, "ИНН организации", false),
        (ImportColumn.Position, "Должность", false)
    };

    // допустимые названия колонок (после нормализации)
    private static readonly Dictionary<string, ImportColumn> Synonyms = new()
    {
        ["фио"] = ImportColumn.FullName,
        ["фамилияимяотчество"] = ImportColumn.FullName,
        ["слушатель"] = ImportColumn.FullName,
        ["обучающийся"] = ImportColumn.FullName,
        ["фамилия"] = ImportColumn.LastName,
        ["имя"] = ImportColumn.FirstName,
        ["отчество"] = ImportColumn.MiddleName,
        ["датарождения"] = ImportColumn.BirthDate,
        ["дрождения"] = ImportColumn.BirthDate,
        ["датарожд"] = ImportColumn.BirthDate,
        ["снилс"] = ImportColumn.Snils,
        ["номерснилс"] = ImportColumn.Snils,
        ["телефон"] = ImportColumn.Phone,
        ["тел"] = ImportColumn.Phone,
        ["электроннаяпочта"] = ImportColumn.Email,
        ["почта"] = ImportColumn.Email,
        ["email"] = ImportColumn.Email,
        ["емейл"] = ImportColumn.Email,
        ["организация"] = ImportColumn.Organization,
        ["наименованиеорганизации"] = ImportColumn.Organization,
        ["местоработы"] = ImportColumn.Organization,
        ["иннорганизации"] = ImportColumn.OrganizationInn,
        ["инн"] = ImportColumn.OrganizationInn,
        ["должность"] = ImportColumn.Position
    };

    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private static string NormalizeHeader(string header)
        => new string(header.ToLowerInvariant().Replace('ё', 'е').Where(char.IsLetterOrDigit).ToArray());

    public static ParseResult Parse(IReadOnlyList<XlsxRow> sheet)
    {
        var result = new ParseResult();
        if (sheet.Count == 0)
        {
            result.StructureErrors.Add("В файле нет данных");
            return result;
        }

        // заголовки ищем в первых строках: шаблон может начинаться с названия или пустых строк
        var headerRow = sheet.Take(10).FirstOrDefault(r => r.Cells.Count(c => Synonyms.ContainsKey(NormalizeHeader(c))) >= 2);
        if (headerRow == null)
        {
            result.StructureErrors.Add("Не найдена строка заголовков. Ожидаются колонки «Фамилия», «Имя», «Отчество» (или «ФИО»), «Дата рождения», «СНИЛС» и т.д. " +
                                       "Скачайте шаблон и перенесите данные в него.");
            return result;
        }

        var columns = new Dictionary<ImportColumn, int>();
        var unknown = new List<string>();
        for (var i = 0; i < headerRow.Cells.Length; i++)
        {
            var raw = headerRow.Cells[i];
            if (string.IsNullOrWhiteSpace(raw)) continue;
            if (Synonyms.TryGetValue(NormalizeHeader(raw), out var col))
            {
                if (columns.ContainsKey(col))
                    result.StructureErrors.Add($"Колонка «{raw}» встречается в файле больше одного раза");
                else
                    columns[col] = i;
            }
            else unknown.Add(raw);
        }

        var hasSplitName = columns.ContainsKey(ImportColumn.LastName) && columns.ContainsKey(ImportColumn.FirstName);
        if (!hasSplitName && !columns.ContainsKey(ImportColumn.FullName))
            result.StructureErrors.Add("Нужны колонки «Фамилия» и «Имя» (или одна колонка «ФИО»)");
        if (result.StructureErrors.Count > 0) return result;

        string Get(XlsxRow row, ImportColumn col)
            => columns.TryGetValue(col, out var i) && i < row.Cells.Length ? row.Cells[i].Trim() : "";

        foreach (var row in sheet.Where(r => r.Number > headerRow.Number))
        {
            var parsed = new ParsedRow { RowNumber = row.Number };

            if (hasSplitName)
            {
                parsed.LastName = Get(row, ImportColumn.LastName);
                parsed.FirstName = Get(row, ImportColumn.FirstName);
                parsed.MiddleName = Blank(Get(row, ImportColumn.MiddleName));
            }
            else
            {
                var parts = Get(row, ImportColumn.FullName).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                parsed.LastName = parts.Length > 0 ? parts[0] : "";
                parsed.FirstName = parts.Length > 1 ? parts[1] : "";
                parsed.MiddleName = parts.Length > 2 ? string.Join(" ", parts.Skip(2)) : null;
            }

            parsed.Phone = Blank(Get(row, ImportColumn.Phone));
            parsed.Email = Blank(Get(row, ImportColumn.Email));
            parsed.Organization = Blank(Get(row, ImportColumn.Organization));
            parsed.OrganizationInn = Blank(Get(row, ImportColumn.OrganizationInn));
            parsed.Position = Blank(Get(row, ImportColumn.Position));

            var snils = Blank(Get(row, ImportColumn.Snils));
            var birth = Blank(Get(row, ImportColumn.BirthDate));

            Validate(parsed, snils, birth);
            result.Rows.Add(parsed);
        }

        if (result.Rows.Count == 0) result.StructureErrors.Add("Под строкой заголовков нет ни одной записи");
        return result;
    }

    private static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Validate(ParsedRow row, string? snils, string? birth)
    {
        if (string.IsNullOrWhiteSpace(row.LastName)) row.Errors.Add(new RowError("Фамилия", "Не указана фамилия"));
        if (string.IsNullOrWhiteSpace(row.FirstName)) row.Errors.Add(new RowError("Имя", "Не указано имя"));
        if (row.LastName.Length > 100) row.Errors.Add(new RowError("Фамилия", "Слишком длинная фамилия (не более 100 знаков)"));
        if (row.FirstName.Length > 100) row.Errors.Add(new RowError("Имя", "Слишком длинное имя (не более 100 знаков)"));
        if (row.MiddleName?.Length > 100) row.Errors.Add(new RowError("Отчество", "Слишком длинное отчество (не более 100 знаков)"));

        if (birth != null)
        {
            if (TryParseDate(birth, out var date))
            {
                if (date > Fmt.Today()) row.Errors.Add(new RowError("Дата рождения", "Дата рождения в будущем"));
                else if (date.Year < 1900) row.Errors.Add(new RowError("Дата рождения", "Дата рождения слишком ранняя"));
                else row.BirthDate = date;
            }
            else row.Errors.Add(new RowError("Дата рождения", $"Не удалось распознать дату «{birth}». Нужен формат дд.мм.гггг"));
        }

        if (snils != null)
        {
            var error = Snils.Validate(snils);
            if (error != null) row.Errors.Add(new RowError("СНИЛС", error));
            else row.Snils = Snils.Normalize(snils);
        }

        if (row.Email != null && (row.Email.Length > 200 || !EmailRegex.IsMatch(row.Email)))
            row.Errors.Add(new RowError("Электронная почта", $"Некорректный адрес «{row.Email}»"));
        if (row.Phone?.Length > 50) row.Errors.Add(new RowError("Телефон", "Слишком длинный номер (не более 50 знаков)"));
        if (row.Position?.Length > 200) row.Errors.Add(new RowError("Должность", "Слишком длинная должность (не более 200 знаков)"));
        if (row.OrganizationInn != null && !Regex.IsMatch(row.OrganizationInn, @"^(\d{10}|\d{12})$"))
            row.Errors.Add(new RowError("ИНН организации", "ИНН должен состоять из 10 или 12 цифр"));
    }

    public static bool TryParseDate(string text, out DateOnly date)
    {
        var formats = new[] { "dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd.MM.yy" };
        return DateOnly.TryParseExact(text.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    // файл-шаблон для скачивания: только заголовки, чтобы пример случайно не попал в импорт
    public static byte[] BuildTemplate()
        => XlsxWriter.Create("Обучающиеся", TemplateColumns.Select(c => c.Title).ToArray(), Array.Empty<object?[]>());
}
