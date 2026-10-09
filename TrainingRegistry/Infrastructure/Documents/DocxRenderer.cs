using System.IO.Compression;
using System.Security;
using System.Text;

namespace TrainingRegistry.Infrastructure.Documents;

public static class DocxRenderer
{
    private const int ContentWidth = 9355;
    // docx собираем руками: документ, стили и связи лежат в zip
    public static byte[] Render(IReadOnlyList<Block> blocks)
    {
        var body = new StringBuilder();
        foreach (var block in blocks)
        {
            switch (block)
            {
                case TitleBlock t:
                    body.Append(Para(t.Text, bold: true, size: 30, align: "center", after: t.Subtitle == null ? 240 : 40));
                    if (t.Subtitle != null) body.Append(Para(t.Subtitle, bold: true, size: 26, align: "center", after: 240));
                    break;
                case LineBlock l:
                    body.Append(Table(new[] { ContentWidth / 2, ContentWidth - ContentWidth / 2 }, borders: false,
                        new[] { new[] { Cell(ContentWidth / 2, Para(l.Left, after: 120)), Cell(ContentWidth - ContentWidth / 2, Para(l.Right, align: "right", after: 120)) } }));
                    break;
                case HeadingBlock h:
                    body.Append(Para(h.Text, bold: true, align: "center", before: 160, after: 80, keepNext: true));
                    break;
                case ParaBlock p:
                    body.Append(Para(p.Text, align: "both", after: 80, firstLine: 567));
                    break;
                case TableBlock tb:
                    body.Append(TableFromBlock(tb));
                    break;
                case TwoColumnBlock tc:
                    body.Append(TwoColumn(tc));
                    break;
            }
        }
        body.Append("<w:p/>");

        var document =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>" +
            body +
            "<w:sectPr><w:pgSz w:w=\"11906\" w:h=\"16838\"/>" +
            "<w:pgMar w:top=\"1134\" w:right=\"850\" w:bottom=\"1134\" w:left=\"1701\" w:header=\"709\" w:footer=\"709\" w:gutter=\"0\"/></w:sectPr>" +
            "</w:body></w:document>";
        const string styles =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<w:styles xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:docDefaults>" +
            "<w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Times New Roman\" w:hAnsi=\"Times New Roman\" w:cs=\"Times New Roman\" w:eastAsia=\"Times New Roman\"/>" +
            "<w:sz w:val=\"24\"/><w:szCs w:val=\"24\"/><w:lang w:val=\"ru-RU\"/></w:rPr></w:rPrDefault>" +
            "<w:pPrDefault><w:pPr><w:spacing w:after=\"0\" w:line=\"264\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault>" +
            "</w:docDefaults></w:styles>";
        const string contentTypes =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
            "<Override PartName=\"/word/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml\"/>" +
            "</Types>";
        const string rootRels =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>" +
            "</Relationships>";
        const string documentRels =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
            "</Relationships>";
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(zip, "[Content_Types].xml", contentTypes);
            AddEntry(zip, "_rels/.rels", rootRels);
            AddEntry(zip, "word/document.xml", document);
            AddEntry(zip, "word/styles.xml", styles);
            AddEntry(zip, "word/_rels/document.xml.rels", documentRels);
        }
        return ms.ToArray();
    }
    private static void AddEntry(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = new UTF8Encoding(false).GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }
    private static string Esc(string text) => SecurityElement.Escape(text) ?? "";
    private static string Para(string text, bool bold = false, int size = 0, string align = "left",
        int before = 0, int after = 0, bool keepNext = false, int firstLine = 0)
    {
        var pPr = new StringBuilder("<w:pPr>");
        if (keepNext) pPr.Append("<w:keepNext/>");
        pPr.Append($"<w:spacing w:before=\"{before}\" w:after=\"{after}\"/>");
        if (firstLine > 0) pPr.Append($"<w:ind w:firstLine=\"{firstLine}\"/>");
        pPr.Append($"<w:jc w:val=\"{align}\"/></w:pPr>");
        var rPr = "";
        if (bold || size > 0)
            rPr = "<w:rPr>" + (bold ? "<w:b/>" : "") + (size > 0 ? $"<w:sz w:val=\"{size}\"/><w:szCs w:val=\"{size}\"/>" : "") + "</w:rPr>";
        var runs = new StringBuilder();
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            runs.Append($"<w:r>{rPr}");
            if (i > 0) runs.Append("<w:br/>");
            runs.Append($"<w:t xml:space=\"preserve\">{Esc(lines[i])}</w:t></w:r>");
        }
        return $"<w:p>{pPr}{runs}</w:p>";
    }
    private static string Cell(int width, string content, bool shaded = false)
        => $"<w:tc><w:tcPr><w:tcW w:w=\"{width}\" w:type=\"dxa\"/>" +
           (shaded ? "<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"D9D9D9\"/>" : "") +
           $"</w:tcPr>{content}</w:tc>";
    private static string Table(int[] widths, bool borders, string[][] rows, bool headerRow = false)
    {
        var sb = new StringBuilder("<w:tbl><w:tblPr>");
        sb.Append($"<w:tblW w:w=\"{widths.Sum()}\" w:type=\"dxa\"/>");
        if (borders)
        {
            sb.Append("<w:tblBorders>");
            foreach (var side in new[] { "top", "left", "bottom", "right", "insideH", "insideV" })
                sb.Append($"<w:{side} w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"808080\"/>");
            sb.Append("</w:tblBorders>");
        }
        sb.Append("<w:tblLayout w:type=\"fixed\"/>");
        sb.Append("<w:tblCellMar><w:top w:w=\"40\" w:type=\"dxa\"/><w:left w:w=\"80\" w:type=\"dxa\"/><w:bottom w:w=\"40\" w:type=\"dxa\"/><w:right w:w=\"80\" w:type=\"dxa\"/></w:tblCellMar>");
        sb.Append("</w:tblPr><w:tblGrid>");
        foreach (var w in widths) sb.Append($"<w:gridCol w:w=\"{w}\"/>");
        sb.Append("</w:tblGrid>");
        for (var r = 0; r < rows.Length; r++)
        {
            sb.Append("<w:tr>");
            if (headerRow && r == 0) sb.Append("<w:trPr><w:tblHeader/></w:trPr>");
            sb.Append(string.Concat(rows[r]));
            sb.Append("</w:tr>");
        }
        sb.Append("</w:tbl>");
        return sb.ToString();
    }
    private static string TableFromBlock(TableBlock tb)
    {
        var total = tb.Widths.Sum();
        var widths = tb.Widths.Select(w => (int)Math.Round(ContentWidth * w / total)).ToArray();
        widths[^1] += ContentWidth - widths.Sum();
        var rows = new List<string[]>
        {
            tb.Header.Select((h, i) => Cell(widths[i], Para(h, bold: true), shaded: true)).ToArray()
        };
        foreach (var row in tb.Rows)
            rows.Add(row.Select((v, i) => Cell(widths[i], Para(v))).ToArray());
        return Table(widths, borders: true, rows.ToArray(), headerRow: true) + Para("", after: 120);
    }

    private static string TwoColumn(TwoColumnBlock tc)
    {
        var half = ContentWidth / 2;
        string Column(string title, string[] lines)
            => Para(title, bold: true, after: 80) + string.Concat(lines.Select(l => Para(l, after: 40)));
        return Table(new[] { half, ContentWidth - half }, borders: false, new[]
        {
            new[]
            {
                Cell(half, Column(tc.LeftTitle, tc.LeftLines)),
                Cell(ContentWidth - half, Column(tc.RightTitle, tc.RightLines))
            }
        });
    }
}