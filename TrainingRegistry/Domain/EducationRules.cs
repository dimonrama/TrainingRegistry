namespace TrainingRegistry.Domain;

public static class EducationRules
{
    // уровни переводим в числа, чтобы их можно было сравнивать
    private static int Rank(EducationLevel level) => level switch
    {
        EducationLevel.Higher => 3,
        EducationLevel.SecondaryVocational => 2,
        EducationLevel.SecondaryGeneral => 1,
        _ => 0
    };

    private static int Rank(RequiredEducation required) => required switch
    {
        RequiredEducation.Higher => 3,
        RequiredEducation.SecondaryVocational => 2,
        _ => 0
    };

    // высшее подходит и туда, где достаточно среднего профессионального
    public static bool Satisfies(EducationLevel have, RequiredEducation required)
        => Rank(have) >= Rank(required);

    // достаточно одной записи об образовании, которая подходит под требование программы
    public static bool IsConfirmed(IEnumerable<Education> educations, RequiredEducation required)
        => required == RequiredEducation.None || educations.Any(e => Satisfies(e.Level, required));
}
