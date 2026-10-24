using System.IO.Compression;
using System.Text.RegularExpressions;
using TrainingRegistry.Infrastructure.Documents;
using Xunit;

namespace TrainingRegistry.Tests;

public class DocumentTests
{
    private static byte[] Template(string name)
    {
        using var stream = typeof(OrderDocuments).Assembly.GetManifestResourceStream("Templates." + name)
                           ?? throw new InvalidOperationException("Нет шаблона " + name);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    // весь текст документа одной строкой
    private static string Text(byte[] docx)
    {
        using var zip = new ZipArchive(new MemoryStream(docx));
        using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
        var xml = reader.ReadToEnd();
        var paragraphs = Regex.Split(xml, "</w:p>").Select(p => string.Concat(Regex.Matches(p, "<w:t[^>]*>([^<]*)</w:t>").Select(m => m.Groups[1].Value)));
        return string.Join("\n", paragraphs).Replace("&quot;", "\"").Replace("&amp;", "&");
    }

    private static List<Listener> Listeners(int count) => Enumerable.Range(1, count)
        .Select(i => new Listener($"Иванов{i} Пётр Сергеевич", "ООО «Северный ветер»", "Инженер", "отлично")).ToList();

    private const string Program = "Охрана труда для руководителей и специалистов";

    [Fact]
    public void Enrollment_order_contains_number_program_and_all_listeners()
    {
        var bytes = OrderDocuments.EnrollmentOrder(Template("enrollment-order.docx"), new EnrollmentOrderData(
            "14-У", new DateOnly(2026, 10, 16), Program, "40", new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 23),
            "ОТ-26/14", "Петрова Анна Ивановна", Listeners(23)));

        var text = Text(bytes);

        Assert.Contains(Program, text);
        Assert.Contains("14", text);
        Assert.Contains("Иванов1 Пётр Сергеевич", text);
        Assert.Contains("Иванов23 Пётр Сергеевич", text);
        Assert.Contains("Петрова Анна Ивановна", text);
    }

    [Fact]
    public void Missing_optional_values_leave_blanks_instead_of_failing()
    {
        var bytes = OrderDocuments.EnrollmentOrder(Template("enrollment-order.docx"), new EnrollmentOrderData(
            "1", new DateOnly(2026, 10, 16), Program, null, null, null, null, null, Listeners(2)));

        Assert.Contains("Иванов2 Пётр Сергеевич", Text(bytes));
    }

    [Fact]
    public void Completion_order_lists_listeners_and_dates()
    {
        var bytes = OrderDocuments.CompletionOrder(Template("completion-order.docx"), new CompletionOrderData(
            "21", new DateOnly(2026, 10, 26), Program, "40", new DateOnly(2026, 10, 23), new DateOnly(2026, 10, 26),
            "ОТ-26/14", "Петрова Анна Ивановна", Listeners(7)));

        var text = Text(bytes);

        Assert.Contains("Иванов7 Пётр Сергеевич", text);
        Assert.Contains(Program, text);
    }

    [Fact]
    public void Other_reason_order_has_reason_and_one_person()
    {
        var bytes = OrderDocuments.OtherExpulsionOrder(Template("completion-order.docx"), new OtherExpulsionData(
            "22", new DateOnly(2026, 10, 27), Program, "ОТ-26/14", new DateOnly(2026, 10, 27),
            "по личному заявлению слушателя", Listeners(1)[0]));

        var text = Text(bytes);

        Assert.Contains("по личному заявлению слушателя", text);
        Assert.Contains("Иванов1 Пётр Сергеевич", text);
        Assert.DoesNotContain("Иванов2", text);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(12)]
    public void Protocol_lists_members_secretary_and_listeners(int count)
    {
        var bytes = OrderDocuments.Protocol(Template("attestation-protocol.docx"), new ProtocolData(
            "7", new DateOnly(2026, 10, 23), "14", new DateOnly(2026, 10, 16), Program, "Смирнова Е.В.",
            new[] { "Кузнецов А.Б.", "Орлова Н.П.", "Попов В.В." }, Listeners(count)));

        var text = Text(bytes);

        Assert.Contains("Кузнецов А.Б.", text);
        Assert.Contains("Попов В.В.", text);
        Assert.Contains("Смирнова Е.В.", text);
        Assert.Contains($"Иванов{count} Пётр Сергеевич", text);
        Assert.Contains("отлично", text);
    }

    [Fact]
    public void Result_is_a_valid_docx_package()
    {
        var bytes = OrderDocuments.EnrollmentOrder(Template("enrollment-order.docx"), new EnrollmentOrderData(
            "1", new DateOnly(2026, 10, 16), Program, "40", null, null, null, null, Listeners(1)));

        using var zip = new ZipArchive(new MemoryStream(bytes));
        Assert.NotNull(zip.GetEntry("word/document.xml"));
        Assert.NotNull(zip.GetEntry("[Content_Types].xml"));
    }

    [Theory]
    [InlineData("14-У", "14")]
    [InlineData("21", "21")]
    public void Trailing_order_suffix_is_removed(string input, string expected)
        => Assert.Equal(expected, OrderDocuments.CleanNumber(input));

    [Fact]
    public void Dates_are_written_in_words()
    {
        Assert.Equal("16 октября 2026", RuDate.Words(new DateOnly(2026, 10, 16)));
        Assert.Equal("16.10.2026", RuDate.Numeric(new DateOnly(2026, 10, 16)));
    }
}
