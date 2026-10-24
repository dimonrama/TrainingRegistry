using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TrainingRegistry.Infrastructure.Documents;

public static class RuDate
{
    private static readonly string[] Months =
    {
        "января", "февраля", "марта", "апреля", "мая", "июня",
        "июля", "августа", "сентября", "октября", "ноября", "декабря"
    };

    // 16.10.2026
    public static string Numeric(DateOnly d) => d.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    // 16 октября 2026
    public static string Words(DateOnly d) => $"{d.Day} {Months[d.Month - 1]} {d.Year}";

    // «16» октября 2026
    public static string QuotedWords(DateOnly d) => $"«{d.Day:00}» {Months[d.Month - 1]} {d.Year}";
}

public record Listener(string FullName, string? Organization, string? Position, string? Grade = null);

public record EnrollmentOrderData(string Number, DateOnly Date, string ProgramName, string? Hours,
    DateOnly? StartDate, DateOnly? EndDate, string? RegistrationNumber, string? Curator,
    IReadOnlyList<Listener> Listeners);

public record CompletionOrderData(string Number, DateOnly Date, string ProgramName, string? Hours,
    DateOnly? AttestationDate, DateOnly ExpelDate, string GroupNumber, string? Curator,
    IReadOnlyList<Listener> Listeners);

public record OtherExpulsionData(string Number, DateOnly Date, string ProgramName, string GroupNumber,
    DateOnly ExpelDate, string Reason, Listener Person);

public record ProtocolData(string ProtocolNumber, DateOnly ProtocolDate, string? OrderNumber, DateOnly? OrderDate,
    string ProgramName, string? Secretary, IReadOnlyList<string> Members, IReadOnlyList<Listener> Listeners);

// заполнение утверждённых шаблонов УМЦ; всё, чего в системе нет, остаётся в документе подчёркиванием для ручной доработки
public static class OrderDocuments
{
    private static readonly Regex NumericDate = new(@"_+\._+\.(?:202_+|20\d\d)");
    private static readonly Regex WordsDate = new(@"_+ _+ 202_+");
    private static readonly Regex QuotedDate = new(@"«_+» _+ 202_+");
    private static readonly Regex ProgramName = new(@"«_{6,}»");
    private static readonly Regex OrderNo = new(@"_+(?=-У)");
    private static readonly Regex Hours = new(@"_+(?= академических)");

    // в шаблоне уже стоит «-У», поэтому из введённого номера его убираем
    public static string CleanNumber(string number)
    {
        var n = number.Trim();
        if (n.EndsWith("-У", StringComparison.OrdinalIgnoreCase)) n = n[..^2].Trim();
        return n;
    }

    private static void Put(XElement p, Regex pattern, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        WordTemplate.ReplaceFirst(p, pattern, value);
    }

    private static void PutProgram(XElement p, string name) => Put(p, ProgramName, $"«{name}»");

    private static string[] ListenerRow(Listener l) => new[] { l.FullName, l.Organization ?? "", l.Position ?? "" };

    // приказ о зачислении
    public static byte[] EnrollmentOrder(byte[] template, EnrollmentOrderData d)
    {
        var t = WordTemplate.Load(template);

        var head = t.FindParagraph(x => x.Contains('№') && x.Contains("-У") && !x.Contains("к приказу"))
                   ?? throw new InvalidDataException("В шаблоне приказа о зачислении не найдена строка с датой и номером");
        Put(head, NumericDate, RuDate.Numeric(d.Date));
        Put(head, OrderNo, CleanNumber(d.Number));

        var title = t.FindParagraph(x => x.TrimStart().StartsWith("«_"))
                    ?? throw new InvalidDataException("В шаблоне приказа о зачислении не найдено название программы");
        PutProgram(title, d.ProgramName);

        var item1 = t.RequireParagraph("Зачислить в состав");
        PutProgram(item1, d.ProgramName);
        Put(item1, Hours, d.Hours);

        var item2 = t.RequireParagraph("Установить срок обучения");
        if (d.StartDate != null) Put(item2, WordsDate, RuDate.Words(d.StartDate.Value));
        if (d.EndDate != null) Put(item2, WordsDate, RuDate.Words(d.EndDate.Value));

        Put(t.RequireParagraph("Присвоить"), new Regex(@"(?<=№)_+"), d.RegistrationNumber);
        Put(t.RequireParagraph("куратора учебной"), new Regex(@"_+(?=\.\s*$)"), d.Curator);

        var annex = t.RequireParagraph("к приказу от");
        Put(annex, NumericDate, RuDate.Numeric(d.Date));
        Put(annex, OrderNo, CleanNumber(d.Number));

        var annexTitle = t.FindParagraph(x => x.TrimStart().StartsWith("по дополнительной") && x.Contains("«_"))
                         ?? throw new InvalidDataException("В приложении шаблона не найдено название программы");
        PutProgram(annexTitle, d.ProgramName);

        FillListeners(t, d.Listeners);
        Put(t.RequireParagraph("Куратор"), new Regex(@"_{20,}"), d.Curator);
        return t.Save();
    }

    // приказ об отчислении в связи с окончанием обучения
    public static byte[] CompletionOrder(byte[] template, CompletionOrderData d)
    {
        var t = WordTemplate.Load(template);

        var head = t.FindParagraph(x => x.Contains('№') && x.Contains("-У") && x.Contains('\t') && !x.Contains("к приказу"))
                   ?? throw new InvalidDataException("В шаблоне приказа об отчислении не найдена строка с датой и номером");
        Put(head, NumericDate, RuDate.Numeric(d.Date));
        Put(head, OrderNo, CleanNumber(d.Number));

        var title = t.FindParagraph(x => x.TrimStart().StartsWith("«_"))
                    ?? throw new InvalidDataException("В шаблоне приказа об отчислении не найдено название программы");
        PutProgram(title, d.ProgramName);

        var basis = t.RequireParagraph("На основании результатов");
        if (d.AttestationDate != null) Put(basis, NumericDate, RuDate.Numeric(d.AttestationDate.Value));
        PutProgram(basis, d.ProgramName);
        Put(basis, Hours, d.Hours);

        var item1 = t.RequireParagraph("Отчислить");
        Put(item1, NumericDate, RuDate.Numeric(d.ExpelDate));
        Put(item1, new Regex(@"(?<=группы )_+"), d.GroupNumber);
        PutProgram(item1, d.ProgramName);

        var annex = t.RequireParagraph("к приказу от");
        Put(annex, NumericDate, RuDate.Numeric(d.Date));
        Put(annex, OrderNo, CleanNumber(d.Number));

        var annexTitle = t.FindParagraph(x => x.TrimStart().StartsWith("по дополнительной") && x.Contains("«_"))
                         ?? throw new InvalidDataException("В приложении шаблона не найдено название программы");
        PutProgram(annexTitle, d.ProgramName);

        FillListeners(t, d.Listeners);
        Put(t.RequireParagraph("Куратор"), new Regex(@"_{20,}"), d.Curator);
        return t.Save();
    }

    // приказ об отчислении по иным основаниям: образца нет, поэтому берём шапку и оформление приказа об окончании обучения
    public static byte[] OtherExpulsionOrder(byte[] completionTemplate, OtherExpulsionData d)
    {
        var t = WordTemplate.Load(completionTemplate);

        var head = t.FindParagraph(x => x.Contains('№') && x.Contains("-У") && x.Contains('\t') && !x.Contains("к приказу"))
                   ?? throw new InvalidDataException("В шаблоне приказа об отчислении не найдена строка с датой и номером");
        Put(head, NumericDate, RuDate.Numeric(d.Date));
        Put(head, OrderNo, CleanNumber(d.Number));

        WordTemplate.SetText(t.RequireParagraph("Об отчислении в связи с окончанием обучения"),
            "Об отчислении слушателя по иным основаниям");

        var title = t.FindParagraph(x => x.TrimStart().StartsWith("«_"))
                    ?? throw new InvalidDataException("В шаблоне приказа об отчислении не найдено название программы");
        PutProgram(title, d.ProgramName);

        var basis = t.RequireParagraph("На основании результатов");
        var reason = d.Reason.Trim();
        WordTemplate.SetText(basis, reason.EndsWith('.') ? reason : reason + ".");

        var item1 = t.RequireParagraph("Отчислить");
        var who = d.Person.FullName;
        var details = string.Join(", ", new[] { d.Person.Organization, d.Person.Position }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        if (details.Length > 0) who += $" ({details})";
        WordTemplate.SetText(item1,
            $"1. Отчислить {RuDate.Numeric(d.ExpelDate)} года слушателя {who} группы {d.GroupNumber} " +
            $"дополнительной образовательной программы повышения квалификации «{d.ProgramName}».");

        // пункт про удостоверение здесь не нужен, пункт про контроль становится вторым
        t.RequireParagraph("Выдать слушателям").Remove();
        var control = t.RequireParagraph("Контроль за исполнение");
        var controlText = WordTemplate.Text(control);
        WordTemplate.SetText(control, Regex.Replace(controlText, @"^\s*3\.\s*", "2. "));

        // приложение со списком не нужно: слушатель назван в самом приказе
        var pageBreak = t.Body.Elements(WordTemplate.W + "p")
            .FirstOrDefault(p => p.Descendants(WordTemplate.W + "br").Any(b => (string?)b.Attribute(WordTemplate.W + "type") == "page")
                                 || WordTemplate.Text(p) == "\n");
        if (pageBreak != null)
        {
            var tail = pageBreak.NodesAfterSelf().Where(n => n is XElement e && e.Name != WordTemplate.W + "sectPr").ToList();
            foreach (var node in tail) node.Remove();
            pageBreak.Remove();
        }
        return t.Save();
    }

    private static bool HeaderHas(XElement table, string text)
    {
        var rows = WordTemplate.Rows(table);
        return rows.Count > 0 && WordTemplate.Cells(rows[0]).Any(c => WordTemplate.CellText(c).Contains(text));
    }

    private static void FillListeners(WordTemplate t, IReadOnlyList<Listener> listeners)
    {
        var table = t.FindTable(x => HeaderHas(x, "Фамилия"))
                    ?? throw new InvalidDataException("В шаблоне не найдена таблица со списком слушателей");
        WordTemplate.FillRows(table, 1, 1, true, listeners.Select(ListenerRow).ToList());
    }

    // протокол итоговой аттестационной комиссии
    public static byte[] Protocol(byte[] template, ProtocolData d)
    {
        var t = WordTemplate.Load(template);

        Put(t.RequireParagraph("ПРОТОКОЛ"), new Regex(@"(?<=№)_+"), d.ProtocolNumber);
        Put(t.RequireParagraph("заседания"), QuotedDate, RuDate.QuotedWords(d.ProtocolDate));

        // члены комиссии идут двумя строками из подчёркиваний между «членов комиссии:» и «секретаря»
        var membersTitle = t.RequireParagraph("членов");
        var lines = new List<XElement>();
        for (var n = membersTitle.ElementsAfterSelf(WordTemplate.W + "p").FirstOrDefault();
             n != null && Regex.IsMatch(WordTemplate.Text(n), @"_{5,}") && !WordTemplate.Text(n).Contains("секретаря");
             n = n.ElementsAfterSelf(WordTemplate.W + "p").FirstOrDefault())
            lines.Add(n);

        var members = d.Members.Where(m => !string.IsNullOrWhiteSpace(m)).ToList();
        if (members.Count > 0 && lines.Count > 0)
        {
            while (lines.Count < members.Count)
            {
                var copy = WordTemplate.CloneClean(lines[^1]);
                lines[^1].AddAfterSelf(copy);
                lines.Add(copy);
            }
            for (var i = 0; i < lines.Count; i++)
            {
                if (i < members.Count) WordTemplate.ReplaceFirst(lines[i], new Regex(@"_{5,}"), members[i]);
                else lines[i].Remove();
            }
        }

        var secretary = t.RequireParagraph("секретаря");
        Put(secretary, new Regex(@"_{5,}"), d.Secretary);

        var order = t.RequireParagraph("приказом");
        Put(order, new Regex(@"(?<=№)_+(?=-У)"), string.IsNullOrWhiteSpace(d.OrderNumber) ? null : CleanNumber(d.OrderNumber));
        if (d.OrderDate != null) Put(order, QuotedDate, RuDate.QuotedWords(d.OrderDate.Value));
        PutProgram(order, d.ProgramName);
        PutProgram(t.RequireParagraph("Признать"), d.ProgramName);

        FillProtocolTable(t, d);
        return t.Save();
    }

    private static void FillProtocolTable(WordTemplate t, ProtocolData d)
    {
        var table = t.FindTable(x => HeaderHas(x, "Оценка"))
                    ?? throw new InvalidDataException("В шаблоне протокола не найдена таблица со списком слушателей");
        var rows = WordTemplate.Rows(table);

        // список слушателей - строки до первой строки с подписью председателя
        var signIndex = rows.FindIndex(r => WordTemplate.CellText(WordTemplate.Cells(r)[0]).Contains("Председатель"));
        if (signIndex < 0) throw new InvalidDataException("В таблице протокола не найдена строка председателя");

        var data = d.Listeners.Select(l => new[] { l.FullName, l.Grade ?? "" }).ToList();
        WordTemplate.FillRows(table, 1, 1, false, data, signIndex);

        rows = WordTemplate.Rows(table);
        var membersRow = rows.FindIndex(r => WordTemplate.CellText(WordTemplate.Cells(r)[0]).Contains("Члены комиссии"));
        var members = d.Members.Where(m => !string.IsNullOrWhiteSpace(m)).ToList();
        if (membersRow >= 0 && members.Count > 0)
        {
            // под «Члены комиссии» идёт пустая строка для второго члена; для остальных копируем её
            var second = membersRow + 1 < rows.Count ? rows[membersRow + 1] : null;
            var extra = members.Count - 2;
            for (var i = 0; i < extra && second != null; i++)
                second.AddAfterSelf(WordTemplate.CloneClean(second));

            rows = WordTemplate.Rows(table);
            for (var i = 0; i < members.Count; i++)
                WordTemplate.SetCellText(WordTemplate.Cells(rows[membersRow + i]).Last(), members[i]);
        }

        var secretaryRow = rows.FindIndex(r => WordTemplate.CellText(WordTemplate.Cells(r)[0]).Contains("Секретарь"));
        if (secretaryRow >= 0 && !string.IsNullOrWhiteSpace(d.Secretary))
            WordTemplate.SetCellText(WordTemplate.Cells(rows[secretaryRow]).Last(), d.Secretary!);
    }
}
