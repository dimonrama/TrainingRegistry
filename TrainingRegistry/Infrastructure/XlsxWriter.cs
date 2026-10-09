using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace TrainingRegistry.Infrastructure;

public static class XlsxWriter
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const int StyleHeader = 1;
    private const int StyleDate = 2;
    private const int StyleDateTime = 3;
    private const int StyleMoney = 4;
    private static readonly DateOnly Epoch = new(1899, 12, 30);
    // xlsx - это zip с xml внутри, поэтому собираем без сторонних библиотек
    public static byte[] Create(string sheetName, IReadOnlyList<string> headers, IEnumerable<object?[]> rows)
    {
        var data = rows.ToList();
        var widths = ColumnWidths(headers, data);
        var sheet = new StringBuilder();
        sheet.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sheet.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
        sheet.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
        sheet.Append("<cols>");
        for (var i = 0; i < widths.Length; i++)
            sheet.Append(string.Create(CultureInfo.InvariantCulture, $"<col min=\"{i + 1}\" max=\"{i + 1}\" width=\"{widths[i]}\" customWidth=\"1\"/>"));
        sheet.Append("</cols><sheetData>");
        sheet.Append("<row r=\"1\">");
        for (var c = 0; c < headers.Count; c++)
            sheet.Append($"<c r=\"{ColumnName(c)}1\" s=\"{StyleHeader}\" t=\"inlineStr\"><is><t>{Esc(headers[c])}</t></is></c>");
        sheet.Append("</row>");
        for (var r = 0; r < data.Count; r++)
        {
            var rowNumber = r + 2;
            sheet.Append($"<row r=\"{rowNumber}\">");
            for (var c = 0; c < data[r].Length && c < headers.Count; c++)
                sheet.Append(Cell($"{ColumnName(c)}{rowNumber}", data[r][c]));
            sheet.Append("</row>");
        }
        sheet.Append("</sheetData>");
        var lastColumn = ColumnName(Math.Max(headers.Count - 1, 0));
        sheet.Append($"<autoFilter ref=\"A1:{lastColumn}{Math.Max(data.Count + 1, 1)}\"/>");
        sheet.Append("</worksheet>");
        var workbook =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
            "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
            $"<sheets><sheet name=\"{Esc(SafeSheetName(sheetName))}\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>";
        const string workbookRels =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
            "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
            "</Relationships>";
        const string styles =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<numFmts count=\"2\"><numFmt numFmtId=\"164\" formatCode=\"dd\\.mm\\.yyyy\"/><numFmt numFmtId=\"165\" formatCode=\"dd\\.mm\\.yyyy\\ hh:mm\"/></numFmts>" +
            "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
            "<fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFD9E1F2\"/><bgColor indexed=\"64\"/></patternFill></fill></fills>" +
            "<borders count=\"2\"><border><left/><right/><top/><bottom/><diagonal/></border>" +
            "<border><left style=\"thin\"><color rgb=\"FF9E9E9E\"/></left><right style=\"thin\"><color rgb=\"FF9E9E9E\"/></right>" +
            "<top style=\"thin\"><color rgb=\"FF9E9E9E\"/></top><bottom style=\"thin\"><color rgb=\"FF9E9E9E\"/></bottom><diagonal/></border></borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"5\">" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
            "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment vertical=\"center\" wrapText=\"1\"/></xf>" +
            "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"><alignment horizontal=\"left\"/></xf>" +
            "<xf numFmtId=\"165\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"><alignment horizontal=\"left\"/></xf>" +
            "<xf numFmtId=\"4\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
            "</cellXfs><cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles></styleSheet>";
        const string contentTypes =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
            "</Types>";
        const string rootRels =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>";
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "[Content_Types].xml", contentTypes);
            Add(zip, "_rels/.rels", rootRels);
            Add(zip, "xl/workbook.xml", workbook);
            Add(zip, "xl/_rels/workbook.xml.rels", workbookRels);
            Add(zip, "xl/styles.xml", styles);
            Add(zip, "xl/worksheets/sheet1.xml", sheet.ToString());
        }
        return ms.ToArray();
    }
    private static void Add(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = new UTF8Encoding(false).GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }
    // текст пишем как строку, а не формулу, чтобы Excel ничего не вычислял
    private static string Cell(string reference, object? value)
    {
        switch (value)
        {
            case null:
                return "";
            case DateOnly d:
                return $"<c r=\"{reference}\" s=\"{StyleDate}\"><v>{d.DayNumber - Epoch.DayNumber}</v></c>";
            case DateTime dt:
                var seconds = Math.Round(dt.TimeOfDay.TotalSeconds);
                var serial = (dt.Date - new DateTime(1899, 12, 30)).Days + seconds / 86400.0;
                return string.Create(CultureInfo.InvariantCulture, $"<c r=\"{reference}\" s=\"{StyleDateTime}\"><v>{serial:0.000000000}</v></c>");
            case decimal m:
                return string.Create(CultureInfo.InvariantCulture, $"<c r=\"{reference}\" s=\"{StyleMoney}\"><v>{m}</v></c>");
            case int or long or short or byte:
                return string.Create(CultureInfo.InvariantCulture, $"<c r=\"{reference}\"><v>{value}</v></c>");
            case double or float:
                return string.Create(CultureInfo.InvariantCulture, $"<c r=\"{reference}\"><v>{value}</v></c>");
            case bool b:
                return $"<c r=\"{reference}\" t=\"inlineStr\"><is><t>{(b ? "Да" : "Нет")}</t></is></c>";
            default:
                var text = value.ToString() ?? "";
                if (text.Length == 0) return "";
                return $"<c r=\"{reference}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Esc(text)}</t></is></c>";
        }
    }
    private static double[] ColumnWidths(IReadOnlyList<string> headers, List<object?[]> rows)
    {
        var widths = headers.Select(h => (double)h.Length + 4).ToArray();
        foreach (var row in rows.Take(500))
        {
            for (var c = 0; c < row.Length && c < widths.Length; c++)
            {
                var length = row[c] switch
                {
                    null => 0,
                    DateOnly => 11,
                    DateTime => 17,
                    decimal => 14,
                    var other => other.ToString()?.Length ?? 0
                };
                widths[c] = Math.Max(widths[c], length + 2);
            }
        }
        return widths.Select(w => Math.Clamp(w, 8, 60)).ToArray();
    }
    private static string ColumnName(int index)
    {
        var name = "";
        for (var n = index + 1; n > 0; n = (n - 1) / 26)
            name = (char)('A' + (n - 1) % 26) + name;
        return name;
    }
    private static string SafeSheetName(string name)
    {
        var cleaned = new string(name.Where(ch => !"[]:*?/\\".Contains(ch)).ToArray());
        if (cleaned.Length == 0) cleaned = "Лист1";
        return cleaned.Length > 31 ? cleaned[..31] : cleaned;
    }
    private static string Esc(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            // управляющие символы выкидываем, от них Excel ругается на файл
            if (ch < 0x20 && ch != '\t' && ch != '\n' && ch != '\r') continue;
            sb.Append(ch switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                _ => ch.ToString()
            });
        }
        return sb.ToString();
    }
}