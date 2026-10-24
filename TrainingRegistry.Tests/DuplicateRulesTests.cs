using TrainingRegistry.Domain;
using Xunit;

namespace TrainingRegistry.Tests;

public class DuplicateRulesTests
{
    private static PersonKey Key(string last, string first, string? middle = null, DateOnly? birth = null,
        string? snils = null, string? email = null, int id = 0)
        => new(id, last, first, middle, birth, snils, email);

    [Fact]
    public void Same_snils_in_any_notation_is_duplicate()
    {
        var reasons = DuplicateRules.Reasons(Key("Петров", "Иван", snils: "11223344595"), Key("Сидоров", "Олег", snils: "112-233-445 95"));

        Assert.Contains("совпадает СНИЛС", reasons);
    }

    [Fact]
    public void Same_name_and_birth_date_is_duplicate_ignoring_case_and_yo()
    {
        var birth = new DateOnly(1985, 3, 14);

        var reasons = DuplicateRules.Reasons(Key("иванов", "Петр", "сергеевич", birth), Key("Иванов", "Пётр", "Сергеевич", birth));

        Assert.Contains("совпадают ФИО и дата рождения", reasons);
    }

    [Fact]
    public void Same_name_with_different_birth_dates_are_different_people()
    {
        var reasons = DuplicateRules.Reasons(Key("Иванов", "Пётр", birth: new DateOnly(1985, 3, 14)),
            Key("Иванов", "Пётр", birth: new DateOnly(1990, 1, 1)));

        Assert.Empty(reasons);
    }

    [Fact]
    public void Same_name_without_birth_date_is_flagged()
    {
        var reasons = DuplicateRules.Reasons(Key("Иванов", "Пётр"), Key("Иванов", "Пётр", birth: new DateOnly(1990, 1, 1)));

        Assert.Single(reasons);
    }

    [Fact]
    public void Same_email_is_duplicate()
    {
        var reasons = DuplicateRules.Reasons(Key("А", "Б", email: "Ivan@Example.com"), Key("В", "Г", email: "ivan@example.com"));

        Assert.Contains("совпадает электронная почта", reasons);
    }

    [Fact]
    public void Different_people_have_no_reasons()
        => Assert.Empty(DuplicateRules.Reasons(Key("Иванов", "Пётр", snils: "112-233-445 95"), Key("Сидоров", "Олег", snils: "001-001-998 00")));

    [Fact]
    public void Empty_snils_never_matches_empty_snils()
        => Assert.Empty(DuplicateRules.Reasons(Key("А", "Б"), Key("В", "Г")));
}
