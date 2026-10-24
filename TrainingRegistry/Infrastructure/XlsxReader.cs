using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TrainingRegistry.Infrastructure;

public record XlsxRow(int Number, string[] Cells);

// читает первый лист xlsx без сторонних библиотек (так же, как XlsxWriter пишет файлы);
// все значения отдаются текстом, даты - в виде дд.мм.гггг
public static class XlsxReader
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PkgRel = "http://schemas.openxmlformats.org/package/2006/relationships";

    // защита от слишком больших файлов (в том числе «бомб» из архива)
    private const long MaxPartBytes = 60 * 1024 * 1024;
    public const int MaxRows = 20000;

    public static List<XlsxRow> Read(byte[] data)
    {
        ZipArchive zip;
        try
        {
            zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read);
        }
        catch (InvalidDataException)
        {
            throw new InvalidDataException("Файл не похож на книгу Excel (.xlsx). Сохраните его в формате «Книга Excel».");
        }

        using (zip)
        {
            var sheetPath = FindFirstSheet(zip);
            var sheetEntry = zip.GetEntry(sheetPath)
                             ?? throw new InvalidDataException("В книге не найден лист с данными");
            var shared = ReadSharedStrings(zip);
            var dateStyles = ReadDateStyles(zip);

            var doc = Load(sheetEntry);
            var result = new List<XlsxRow>();
            foreach (var row in doc.Descendants(Main + "row"))
            {
                if (result.Count >= MaxRows)
                    throw new InvalidDataException($"В файле больше {MaxRows} строк, разделите его на части");

                var rowNumber = (int?)row.Attribute("r") ?? (result.Count + 1);
                var cells = new List<string>();
                foreach (var c in row.Elements(Main + "c"))
                {
                    var index = ColumnIndex((string?)c.Attribute("r"), cells.Count);
                    while (cells.Count <= index) cells.Add("");
                    cells[index] = CellText(c, shared, dateStyles);
                }
                if (cells.Any(x => x.Length > 0)) result.Add(new XlsxRow(rowNumber, cells.ToArray()));
            }
            return result;
        }
    }

    private static XDocument Load(ZipArchiveEntry entry)
    {
        if (entry.Length > MaxPartBytes) throw new InvalidDataException("Файл слишком большой");
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static string FindFirstSheet(ZipArchive zip)
    {
        var workbook = zip.GetEntry("xl/workbook.xml");
        var rels = zip.GetEntry("xl/_rels/workbook.xml.rels");
        if (workbook != null && rels != null)
        {
            var sheet = Load(workbook).Descendants(Main + "sheet").FirstOrDefault();
            var id = (string?)sheet?.Attribute(Rel + "id");
            if (id != null)
            {
                var target = Load(rels).Descendants(PkgRel + "Relationship")
                    .FirstOrDefault(r => (string?)r.Attribute("Id") == id)?.Attribute("Target")?.Value;
                if (!string.IsNullOrEmpty(target))
                    return target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
            }
        }
        return "xl/worksheets/sheet1.xml";
    }

    private static List<string> ReadSharedStrings(ZipArchive zip)
    {
        var entry = zip.GetEntry("xl/sharedStrings.xml");
        var list = new List<string>();
        if (entry == null) return list;
        foreach (var si in Load(entry).Descendants(Main + "si"))
        {
            // в «богатом тексте» строка разбита на куски, фонетические подсказки пропускаем
            var text = string.Concat(si.Descendants(Main + "t")
                .Where(t => t.Ancestors(Main + "rPh").Any() == false)
                .Select(t => t.Value));
            list.Add(text);
        }
        return list;
    }

    // номера стилей ячеек, у которых числовой формат - дата
    private static HashSet<int> ReadDateStyles(ZipArchive zip)
    {
        var set = new HashSet<int>();
        var entry = zip.GetEntry("xl/styles.xml");
        if (entry == null) return set;
        var doc = Load(entry);

        var custom = doc.Descendants(Main + "numFmt")
            .ToDictionary(n => (int)n.Attribute("numFmtId")!, n => (string?)n.Attribute("formatCode") ?? "");

        var xfs = doc.Descendants(Main + "cellXfs").FirstOrDefault()?.Elements(Main + "xf").ToList();
        if (xfs == null) return set;
        for (var i = 0; i < xfs.Count; i++)
        {
            var id = (int?)xfs[i].Attribute("numFmtId") ?? 0;
            var builtinDate = id is >= 14 and <= 22 or >= 45 and <= 47;
            if (builtinDate || (custom.TryGetValue(id, out var code) && LooksLikeDate(code))) set.Add(i);
        }
        return set;
    }

    private static bool LooksLikeDate(string code)
    {
        // убираем текст в кавычках и в скобках, дальше ищем день, месяц или год
        var cleaned = Regex.Replace(code, "\"[^\"]*\"|\\[[^\\]]*\\]|\\\\.", "");
        return Regex.IsMatch(cleaned, "[dmyDMY]");
    }

    private static int ColumnIndex(string? reference, int fallback)
    {
        if (string.IsNullOrEmpty(reference)) return fallback;
        var index = 0;
        foreach (var ch in reference)
        {
            if (!char.IsLetter(ch)) break;
            index = index * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
        }
        return index == 0 ? fallback : index - 1;
    }

    private static string CellText(XElement c, List<string> shared, HashSet<int> dateStyles)
    {
        var type = (string?)c.Attribute("t");
        switch (type)
        {
            case "inlineStr":
                return string.Concat(c.Element(Main + "is")?.Descendants(Main + "t").Select(t => t.Value) ?? Array.Empty<string>()).Trim();
            case "s":
                return int.TryParse(c.Element(Main + "v")?.Value, out var i) && i >= 0 && i < shared.Count
                    ? shared[i].Trim()
                    : "";
            case "str":
                return (c.Element(Main + "v")?.Value ?? "").Trim();
            case "b":
                return c.Element(Main + "v")?.Value == "1" ? "Да" : "Нет";
            case "e":
                return "";
        }

        var raw = c.Element(Main + "v")?.Value;
        if (string.IsNullOrWhiteSpace(raw)) return "";
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return raw.Trim();

        var style = (int?)c.Attribute("s") ?? 0;
        if (dateStyles.Contains(style) && number >= 1 && number < 2958466)
            return new DateOnly(1899, 12, 30).AddDays((int)Math.Floor(number)).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

        // целые числа (телефоны, ИНН) пишем без дробной части и без степени
        if (Math.Abs(number - Math.Round(number)) < 1e-9 && Math.Abs(number) < 1e15)
            return ((long)Math.Round(number)).ToString(CultureInfo.InvariantCulture);
        return number.ToString("0.##########", CultureInfo.InvariantCulture);
    }
}
