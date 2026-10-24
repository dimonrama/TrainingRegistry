using TrainingRegistry.Infrastructure;
using Xunit;

namespace TrainingRegistry.Tests;

public class ImportTests
{
    private static readonly string[] Headers =
        { "Фамилия", "Имя", "Отчество", "Дата рождения", "СНИЛС", "Телефон", "Электронная почта", "Организация", "ИНН организации", "Должность" };

    private static ParseResult Parse(params object?[][] rows)
        => ImportParser.Parse(XlsxReader.Read(XlsxWriter.Create("Лист", Headers, rows)));

    [Fact]
    public void Good_row_is_parsed_and_normalized()
    {
        var result = Parse(new object?[] { "Иванов", "Пётр", "Сергеевич", new DateOnly(1985, 3, 14), "11223344595", null, null, "ООО Ромашка", "7701234567", "Инженер" });

        var row = Assert.Single(result.Rows);
        Assert.Empty(row.Errors);
        Assert.Equal("112-233-445 95", row.Snils);
        Assert.Equal(new DateOnly(1985, 3, 14), row.BirthDate);
        Assert.Equal("7701234567", row.OrganizationInn);
    }

    [Fact]
    public void Errors_name_the_field_and_row_number()
    {
        var result = Parse(
            new object?[] { "Иванов", "Пётр", null, null, null, null, null, null, null, null },
            new object?[] { "Петрова", "Анна", null, "31.02.2000", "112-233-445 96", null, "bad-mail", null, "123", null });

        Assert.Empty(result.Rows[0].Errors);
        var bad = result.Rows[1];
        Assert.Equal(3, bad.RowNumber);
        var fields = bad.Errors.Select(e => e.Field).ToList();
        Assert.Contains("Дата рождения", fields);
        Assert.Contains("СНИЛС", fields);
        Assert.Contains("Электронная почта", fields);
        Assert.Contains("ИНН организации", fields);
    }

    [Fact]
    public void Missing_surname_is_an_error()
    {
        var result = Parse(new object?[] { null, "Сидор", null, null, null, null, null, null, null, null });

        Assert.Contains(result.Rows[0].Errors, e => e.Field == "Фамилия");
    }

    [Fact]
    public void Single_fio_column_is_split()
    {
        var file = XlsxWriter.Create("x", new[] { "ФИО", "Дата рождения" }, new[] { new object?[] { "Смирнов Алексей Петрович", "01.02.1990" } });

        var result = ImportParser.Parse(XlsxReader.Read(file));

        Assert.Empty(result.StructureErrors);
        Assert.Equal("Смирнов", result.Rows[0].LastName);
        Assert.Equal("Петрович", result.Rows[0].MiddleName);
    }

    [Fact]
    public void File_without_known_columns_is_a_structure_error()
    {
        var file = XlsxWriter.Create("x", new[] { "Колонка1", "Колонка2" }, new[] { new object?[] { "a", "b" } });

        var result = ImportParser.Parse(XlsxReader.Read(file));

        Assert.NotEmpty(result.StructureErrors);
        Assert.Empty(result.Rows);
    }

    [Fact]
    public void Template_has_headers_only()
    {
        var sheet = XlsxReader.Read(ImportParser.BuildTemplate());

        Assert.Single(sheet);
        Assert.Contains("СНИЛС", sheet[0].Cells);
    }

    [Fact]
    public void Garbage_is_rejected_without_crashing()
        => Assert.ThrowsAny<Exception>(() => XlsxReader.Read(new byte[] { 1, 2, 3, 4 }));
}
