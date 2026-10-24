namespace TrainingRegistry.Domain;

public static class FundingRules
{
    // если у обучающегося указано своё направление, оно главнее группового
    public static FundingSource? Effective(FundingSource? group, FundingSource? enrollment)
        => enrollment ?? group;

    // у группы и обучающегося указано разное
    public static bool Conflicts(FundingSource? group, FundingSource? enrollment)
        => group != null && enrollment != null && group != enrollment;

    public static string Short(FundingSource? source) => source switch
    {
        FundingSource.StateTask => "ГЗ",
        FundingSource.Business => "ПД",
        _ => "не указано"
    };
}

public static class AttestationRules
{
    // с такой оценкой слушатель попадает в протокол на выдачу удостоверения
    public static bool IsSuccessful(AttestationResult? result)
        => result is null or AttestationResult.Excellent or AttestationResult.Good
            or AttestationResult.Satisfactory or AttestationResult.Passed;
}
