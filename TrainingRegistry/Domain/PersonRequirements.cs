using System.ComponentModel.DataAnnotations;

namespace TrainingRegistry.Domain;

// поля карточки, которые программа может сделать обязательными перед зачислением
[Flags]
public enum PersonField
{
    None = 0,
    [Display(Name = "Дата рождения")] BirthDate = 1,
    [Display(Name = "Номер СНИЛС")] Snils = 2,
    [Display(Name = "Телефон")] Phone = 4,
    [Display(Name = "Электронная почта")] Email = 8,
    [Display(Name = "Организация")] Organization = 16,
    [Display(Name = "Должность")] Position = 32,
    [Display(Name = "Скан паспорта")] PassportFile = 64,
    [Display(Name = "Скан СНИЛС")] SnilsFile = 128,
    [Display(Name = "Скан диплома об образовании")] DiplomaFile = 256
}

// что из документов у обучающегося уже загружено
public record AttachedDocs(bool Passport, bool Snils, bool Diploma)
{
    public static readonly AttachedDocs None = new(false, false, false);
}

public static class PersonRequirements
{
    public static readonly PersonField[] All =
    {
        PersonField.BirthDate, PersonField.Snils, PersonField.Phone, PersonField.Email,
        PersonField.Organization, PersonField.Position,
        PersonField.PassportFile, PersonField.SnilsFile, PersonField.DiplomaFile
    };

    // ФИО обязательны всегда (это ограничение самой карточки), поэтому здесь их нет
    public static List<string> Missing(Person person, AttachedDocs docs, PersonField required)
    {
        var missing = new List<string>();
        void Check(PersonField field, bool filled)
        {
            if (required.HasFlag(field) && !filled) missing.Add(field.GetDisplayName());
        }

        Check(PersonField.BirthDate, person.BirthDate != null);
        Check(PersonField.Snils, !string.IsNullOrWhiteSpace(person.Snils));
        Check(PersonField.Phone, !string.IsNullOrWhiteSpace(person.Phone));
        Check(PersonField.Email, !string.IsNullOrWhiteSpace(person.Email));
        Check(PersonField.Organization, person.OrganizationId != null);
        Check(PersonField.Position, !string.IsNullOrWhiteSpace(person.Position));
        Check(PersonField.PassportFile, docs.Passport);
        Check(PersonField.SnilsFile, docs.Snils);
        Check(PersonField.DiplomaFile, docs.Diploma);
        return missing;
    }

    public static string Describe(PersonField required)
    {
        var names = All.Where(f => required.HasFlag(f)).Select(f => f.GetDisplayName()).ToList();
        return names.Count == 0 ? "не заданы" : string.Join(", ", names);
    }
}
