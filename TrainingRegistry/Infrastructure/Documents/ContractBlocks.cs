using System.Globalization;

namespace TrainingRegistry.Infrastructure.Documents;

public abstract record Block;
public record TitleBlock(string Text, string? Subtitle = null) : Block;
public record LineBlock(string Left, string Right) : Block;
public record HeadingBlock(string Text) : Block;
public record ParaBlock(string Text) : Block;
public record TableBlock(string[] Header, List<string[]> Rows, double[] Widths) : Block;
public record TwoColumnBlock(string LeftTitle, string[] LeftLines, string RightTitle, string[] RightLines) : Block;

public static class ContractBlocks
{
    private static readonly string[] MonthsGenitive =
    {
        "января", "февраля", "марта", "апреля", "мая", "июня",
        "июля", "августа", "сентября", "октября", "ноября", "декабря"
    };

    public static string LongDate(DateOnly d) => $"«{d.Day:00}» {MonthsGenitive[d.Month - 1]} {d.Year} г.";

    private static string MoneyText(decimal value)
        => value.ToString("N2", new CultureInfo("ru-RU")) + " руб.";

    public static List<Block> Build(ContractDocModel m, ContractSettings s)
    {
        var c = m.Customer;
        var executorSigner = string.IsNullOrWhiteSpace(s.ExecutorSignerName)
            ? s.ExecutorSignerPosition
            : $"{s.ExecutorSignerPosition} ({s.ExecutorSignerName})";

        string customerPart;
        if (c.IsOrganization)
        {
            var signer = string.IsNullOrWhiteSpace(c.SignerName)
                ? "руководителя"
                : $"представителя ({(string.IsNullOrWhiteSpace(c.SignerPosition) ? "" : c.SignerPosition + " ")}{c.SignerName})";
            customerPart = $"{c.Name}, именуемое в дальнейшем «Заказчик», в лице {signer}";
        }
        else
        {
            customerPart = $"гражданин(ка) {c.Name}, именуемый(ая) в дальнейшем «Заказчик»";
        }

        var period = m.EndDate == null
            ? $"начало обучения — {LongDate(m.StartDate)}"
            : $"с {LongDate(m.StartDate)} по {LongDate(m.EndDate.Value)}";

        var blocks = new List<Block>
        {
            new TitleBlock($"ДОГОВОР № {m.Number}", "об оказании образовательных услуг"),
            new LineBlock($"г. {s.City}", LongDate(m.Date)),
            new ParaBlock($"{s.ExecutorName}, именуемое в дальнейшем «Исполнитель», в лице {executorSigner}, " +
                          $"действующего на основании {s.ExecutorBasis}, с одной стороны, и {customerPart}, с другой стороны, " +
                          "совместно именуемые «Стороны», заключили настоящий договор о нижеследующем:"),

            new HeadingBlock("1. Предмет договора"),
            new ParaBlock($"1.1. Исполнитель обязуется оказать образовательные услуги по программе «{m.ProgramName}» " +
                          $"(объём обучения: {m.DurationText}), а Заказчик обязуется оплатить эти услуги."),
            new ParaBlock($"1.2. Обучение проводится в группе «{m.GroupName}», {period.TrimEnd('.')}."),
            new ParaBlock($"1.3. Количество обучающихся — {m.StudentsCount} чел. Список обучающихся приведён в конце настоящего договора."),

            new HeadingBlock("2. Стоимость услуг и порядок расчётов"),
            new ParaBlock($"2.1. Стоимость обучения одного обучающегося составляет {MoneyText(m.PricePerPerson)} ({RuWords.Rubles(m.PricePerPerson)})."),
            new ParaBlock($"2.2. Общая стоимость услуг по настоящему договору составляет {MoneyText(m.TotalPrice)} ({RuWords.Rubles(m.TotalPrice)})."),
            new ParaBlock("2.3. Оплата производится Заказчиком путём безналичного перечисления на расчётный счёт Исполнителя " +
                          "в течение 5 (пяти) рабочих дней с даты подписания настоящего договора."),

            new HeadingBlock("3. Права и обязанности сторон"),
            new ParaBlock("3.1. Исполнитель обязуется организовать и провести обучение по утверждённой программе, " +
                          "а обучающимся, успешно прошедшим обучение, выдать документ о прохождении обучения."),
            new ParaBlock("3.2. Заказчик обязуется своевременно оплатить услуги и обеспечить явку обучающихся на занятия."),

            new HeadingBlock("4. Заключительные положения"),
            new ParaBlock("4.1. За неисполнение или ненадлежащее исполнение обязательств Стороны несут ответственность " +
                          "в соответствии с законодательством Российской Федерации."),
            new ParaBlock("4.2. Договор вступает в силу с момента подписания и действует до полного исполнения Сторонами обязательств."),
            new ParaBlock("4.3. Договор составлен в двух экземплярах, имеющих равную юридическую силу, по одному для каждой из Сторон."),

            new HeadingBlock("Список обучающихся"),
            new TableBlock(
                new[] { "№", "ФИО", "Должность" },
                m.Students.Select((st, i) => new[] { (i + 1).ToString(), st.FullName, string.IsNullOrWhiteSpace(st.Position) ? "—" : st.Position! }).ToList(),
                new[] { 1.0, 7.0, 5.0 }),

            new HeadingBlock("Реквизиты и подписи сторон"),
            new TwoColumnBlock("Исполнитель", ExecutorLines(s), "Заказчик", CustomerLines(c))
        };

        return blocks;
    }

    private static string[] ExecutorLines(ContractSettings s)
    {
        var lines = new List<string> { s.ExecutorName };
        if (!string.IsNullOrWhiteSpace(s.ExecutorInn)) lines.Add($"ИНН {s.ExecutorInn}");
        if (!string.IsNullOrWhiteSpace(s.ExecutorAddress)) lines.Add($"Адрес: {s.ExecutorAddress}");
        if (!string.IsNullOrWhiteSpace(s.ExecutorPhone)) lines.Add($"Телефон: {s.ExecutorPhone}");
        lines.Add("");
        lines.Add(string.IsNullOrWhiteSpace(s.ExecutorSignerName)
            ? "____________________ / ____________________"
            : $"____________________ / {s.ExecutorSignerName}");
        return lines.ToArray();
    }

    private static string[] CustomerLines(CustomerInfo c)
    {
        var lines = new List<string> { c.Name };
        var inn = string.Join(" / ", new[]
        {
            c.Inn != null ? $"ИНН {c.Inn}" : null,
            c.Kpp != null ? $"КПП {c.Kpp}" : null
        }.Where(x => x != null));
        if (inn.Length > 0) lines.Add(inn);
        if (!string.IsNullOrWhiteSpace(c.Ogrn)) lines.Add($"ОГРН {c.Ogrn}");
        if (!string.IsNullOrWhiteSpace(c.Address)) lines.Add($"Адрес: {c.Address}");
        if (!string.IsNullOrWhiteSpace(c.Phone)) lines.Add($"Телефон: {c.Phone}");
        if (!string.IsNullOrWhiteSpace(c.Email)) lines.Add($"Эл. почта: {c.Email}");
        lines.Add("");
        lines.Add(string.IsNullOrWhiteSpace(c.SignerName)
            ? "____________________ / ____________________"
            : $"____________________ / {c.SignerName}");
        return lines.ToArray();
    }
}
