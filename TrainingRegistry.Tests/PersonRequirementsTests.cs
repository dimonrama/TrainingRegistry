using TrainingRegistry.Domain;
using Xunit;

namespace TrainingRegistry.Tests;

public class PersonRequirementsTests
{
    private static Person Filled() => new()
    {
        LastName = "Иванов", FirstName = "Пётр", BirthDate = new DateOnly(1985, 3, 14),
        Snils = "112-233-445 95", Phone = "+7 900 000-00-00", Email = "a@b.ru", OrganizationId = 1, Position = "Инженер"
    };

    [Fact]
    public void Nothing_required_means_nothing_missing()
        => Assert.Empty(PersonRequirements.Missing(new Person(), AttachedDocs.None, PersonField.None));

    [Fact]
    public void Missing_fields_are_listed_by_name()
    {
        var person = new Person { LastName = "Иванов", FirstName = "Пётр" };

        var missing = PersonRequirements.Missing(person, AttachedDocs.None, PersonField.BirthDate | PersonField.Snils);

        Assert.Equal(2, missing.Count);
        Assert.Contains("Дата рождения", missing);
        Assert.Contains("Номер СНИЛС", missing);
    }

    [Fact]
    public void Snils_number_and_snils_file_are_independent()
    {
        var person = Filled();

        var withoutFile = PersonRequirements.Missing(person, AttachedDocs.None, PersonField.Snils | PersonField.SnilsFile);
        Assert.Equal(new[] { "Скан СНИЛС" }, withoutFile);

        person.Snils = null;
        var withoutNumber = PersonRequirements.Missing(person, new AttachedDocs(false, true, false), PersonField.Snils | PersonField.SnilsFile);
        Assert.Equal(new[] { "Номер СНИЛС" }, withoutNumber);
    }

    [Fact]
    public void Complete_card_passes_every_requirement()
    {
        var docs = new AttachedDocs(true, true, true);
        var all = PersonRequirements.All.Aggregate(PersonField.None, (acc, f) => acc | f);

        Assert.Empty(PersonRequirements.Missing(Filled(), docs, all));
    }

    [Fact]
    public void Blank_strings_do_not_count_as_filled()
    {
        var person = Filled();
        person.Phone = "   ";

        Assert.Equal(new[] { "Телефон" }, PersonRequirements.Missing(person, AttachedDocs.None, PersonField.Phone));
    }
}
