namespace TrainingRegistry.Domain;

// минимум сведений об обучающемся для сравнения на дубли
public record PersonKey(int Id, string LastName, string FirstName, string? MiddleName,
    DateOnly? BirthDate, string? Snils, string? Email)
{
    public string FullName => string.Join(" ", new[] { LastName, FirstName, MiddleName }
        .Where(s => !string.IsNullOrWhiteSpace(s)));

    public static PersonKey From(Person p) =>
        new(p.Id, p.LastName, p.FirstName, p.MiddleName, p.BirthDate, p.Snils, p.Email);
}

public static class DuplicateRules
{
    private static string Norm(string? s) => (s ?? "").Trim().ToLowerInvariant().Replace('ё', 'е');

    public static bool SameName(PersonKey a, PersonKey b)
        => Norm(a.LastName) == Norm(b.LastName)
           && Norm(a.FirstName) == Norm(b.FirstName)
           && Norm(a.MiddleName) == Norm(b.MiddleName);

    // причины, по которым две записи могут быть одним человеком; пустой список - не похожи
    public static List<string> Reasons(PersonKey candidate, PersonKey existing)
    {
        var reasons = new List<string>();

        var s1 = Snils.Digits(candidate.Snils);
        if (s1.Length == 11 && s1 == Snils.Digits(existing.Snils))
            reasons.Add("совпадает СНИЛС");

        if (SameName(candidate, existing))
        {
            if (candidate.BirthDate != null && candidate.BirthDate == existing.BirthDate)
                reasons.Add("совпадают ФИО и дата рождения");
            else if (candidate.BirthDate == null || existing.BirthDate == null)
                reasons.Add("совпадают ФИО (дата рождения не указана)");
        }

        if (!string.IsNullOrWhiteSpace(candidate.Email) && Norm(candidate.Email) == Norm(existing.Email))
            reasons.Add("совпадает электронная почта");

        return reasons;
    }
}
