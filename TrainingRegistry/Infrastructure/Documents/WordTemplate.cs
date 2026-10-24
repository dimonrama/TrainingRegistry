using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace TrainingRegistry.Infrastructure.Documents;

// работа с готовым docx-шаблоном: ищем в абзацах пустые места из подчёркиваний и подставляем значения,
// оформление шаблона (шрифты, шапка, колонтитулы) остаётся как есть
public sealed class WordTemplate
{
    public static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace W14 = "http://schemas.microsoft.com/office/word/2010/wordml";

    private readonly byte[] _source;
    private readonly XDocument _doc;

    private WordTemplate(byte[] source, XDocument doc)
    {
        _source = source;
        _doc = doc;
    }

    public static WordTemplate Load(byte[] docx)
    {
        using var zip = new ZipArchive(new MemoryStream(docx), ZipArchiveMode.Read);
        var entry = zip.GetEntry("word/document.xml")
                    ?? throw new InvalidDataException("В шаблоне нет word/document.xml, это не файл Word");
        using var stream = entry.Open();
        return new WordTemplate(docx, XDocument.Load(stream));
    }

    public XElement Body => _doc.Root!.Element(W + "body")!;

    // абзацы документа по порядку, включая те, что внутри таблиц
    public List<XElement> Paragraphs() => Body.Descendants(W + "p").ToList();

    public XElement? FindParagraph(Func<string, bool> match, int skip = 0)
        => Paragraphs().Where(p => match(Text(p))).Skip(skip).FirstOrDefault();

    public XElement RequireParagraph(string contains, int skip = 0)
        => FindParagraph(t => t.Contains(contains), skip)
           ?? throw new InvalidDataException($"В шаблоне не найден абзац со словами «{contains}». Возможно, шаблон был изменён.");

    // ---- текст абзаца ----

    private sealed record Piece(XElement Element, int Start, int Length, bool Editable);

    private static IEnumerable<XElement> RunsOf(XElement p)
        => p.Descendants(W + "r").Where(r => ReferenceEquals(r.Ancestors(W + "p").First(), p));

    private static (string Text, List<Piece> Pieces) Flatten(XElement p)
    {
        var sb = new StringBuilder();
        var pieces = new List<Piece>();
        foreach (var run in RunsOf(p))
        {
            foreach (var el in run.Elements())
            {
                if (el.Name == W + "t")
                {
                    var value = el.Value;
                    pieces.Add(new Piece(el, sb.Length, value.Length, true));
                    sb.Append(value);
                }
                else if (el.Name == W + "tab")
                {
                    pieces.Add(new Piece(el, sb.Length, 1, false));
                    sb.Append('\t');
                }
                else if (el.Name == W + "br" || el.Name == W + "cr")
                {
                    pieces.Add(new Piece(el, sb.Length, 1, false));
                    sb.Append('\n');
                }
            }
        }
        return (sb.ToString(), pieces);
    }

    public static string Text(XElement p) => Flatten(p).Text;

    // заменяет первое совпадение, даже если оно разбито на несколько фрагментов текста;
    // новый текст попадает в первый затронутый фрагмент и наследует его оформление
    public static bool ReplaceFirst(XElement p, Regex pattern, string value)
    {
        var (text, pieces) = Flatten(p);
        var match = pattern.Match(text);
        if (!match.Success || match.Length == 0) return false;

        var start = match.Index;
        var end = match.Index + match.Length;
        var placed = false;
        foreach (var piece in pieces.Where(x => x.Editable && x.Start < end && x.Start + x.Length > start))
        {
            var current = piece.Element.Value;
            var from = Math.Max(start, piece.Start) - piece.Start;
            var to = Math.Min(end, piece.Start + piece.Length) - piece.Start;
            piece.Element.Value = current.Substring(0, from) + (placed ? "" : value) + current.Substring(to);
            piece.Element.SetAttributeValue(XNamespace.Xml + "space", "preserve");
            placed = true;
        }
        return placed;
    }

    public static bool ReplaceFirst(XElement p, string pattern, string value)
        => ReplaceFirst(p, new Regex(pattern), value);

    // весь текст абзаца заменяем новым, оформление берём у первого фрагмента
    public static void SetText(XElement p, string text)
    {
        // если в абзаце уже были фрагменты, берём оформление первого из них, иначе оформление знака абзаца
        var firstRun = RunsOf(p).FirstOrDefault();
        var rPr = firstRun != null ? firstRun.Element(W + "rPr") : p.Element(W + "pPr")?.Element(W + "rPr");
        foreach (var child in p.Elements().Where(e => e.Name != W + "pPr").ToList()) child.Remove();

        var run = new XElement(W + "r");
        if (rPr != null) run.Add(new XElement(rPr));
        run.Add(new XElement(W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text));
        p.Add(run);
    }

    // ---- абзацы и таблицы ----

    public static XElement CloneClean(XElement source)
    {
        var copy = new XElement(source);
        // одинаковые paraId в одном файле Word не любит
        foreach (var el in copy.DescendantsAndSelf())
        {
            el.Attribute(W14 + "paraId")?.Remove();
            el.Attribute(W14 + "textId")?.Remove();
        }
        return copy;
    }

    public static void SetCellText(XElement cell, string text)
    {
        var p = cell.Elements(W + "p").First();
        foreach (var child in p.Elements().Where(e => e.Name != W + "pPr").ToList()) child.Remove();
        if (text.Length == 0) return;

        var rPr = p.Element(W + "pPr")?.Element(W + "rPr");
        var run = new XElement(W + "r");
        if (rPr != null) run.Add(new XElement(rPr));
        run.Add(new XElement(W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text));
        p.Add(run);
    }

    public static string CellText(XElement cell) => string.Concat(cell.Descendants(W + "t").Select(t => t.Value));

    public static List<XElement> Rows(XElement table) => table.Elements(W + "tr").ToList();

    public static List<XElement> Cells(XElement row) => row.Elements(W + "tc").ToList();

    public XElement? FindTable(Func<XElement, bool> match) => Body.Descendants(W + "tbl").FirstOrDefault(match);

    // строки таблицы после шапки пересоздаём по образцу первой строки данных;
    // firstColumn - с какой колонки вставлять значения (если в первой нумерация сделана самим Word)
    public static void FillRows(XElement table, int headerRows, int firstColumn, bool numberFirstColumn,
        IReadOnlyList<string[]> data, int? lastRowExclusive = null)
    {
        var rows = Rows(table);
        var prototype = CloneClean(rows[headerRows]);
        var stop = lastRowExclusive ?? rows.Count;
        // убираем старые пустые строки
        for (var i = headerRows; i < stop; i++) rows[i].Remove();

        // новые вставляем перед тем, что шло после списка (подписи и т.п.), либо в конец
        var anchor = stop < rows.Count ? rows[stop] : null;
        for (var i = 0; i < data.Count; i++)
        {
            var row = CloneClean(prototype);
            var cells = Cells(row);
            if (numberFirstColumn) SetCellText(cells[0], (i + 1).ToString());
            for (var c = 0; c < data[i].Length && firstColumn + c < cells.Count; c++)
                SetCellText(cells[firstColumn + c], data[i][c]);
            if (anchor != null) anchor.AddBeforeSelf(row);
            else table.Add(row);
        }
    }

    // ---- сохранение ----

    public byte[] Save()
    {
        using var input = new MemoryStream(_source);
        using var inZip = new ZipArchive(input, ZipArchiveMode.Read);
        using var output = new MemoryStream();
        using (var outZip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in inZip.Entries)
            {
                var target = outZip.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                using var targetStream = target.Open();
                if (entry.FullName == "word/document.xml")
                {
                    var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false) };
                    using var writer = XmlWriter.Create(targetStream, settings);
                    _doc.Save(writer);
                }
                else
                {
                    using var sourceStream = entry.Open();
                    sourceStream.CopyTo(targetStream);
                }
            }
        }
        return output.ToArray();
    }
}
