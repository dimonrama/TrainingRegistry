namespace TrainingRegistry.Domain;

public enum Urgency
{
    Red,
    Yellow,
    Green
}

public static class RetrainingRules
{
    public const int YellowZoneDays = 30;

    // красный - осталось не больше порога, жёлтый - ещё 30 дней после порога, дальше зелёный
    public static Urgency GetUrgency(int daysLeft, int thresholdDays)
        => daysLeft <= thresholdDays ? Urgency.Red
         : daysLeft < thresholdDays + YellowZoneDays ? Urgency.Yellow
         : Urgency.Green;
    public static string DaysLeftText(int daysLeft) => daysLeft switch
    {
        < 0 => $"просрочено на {-daysLeft} дн.",
        0 => "сегодня",
        _ => $"через {daysLeft} дн."
    };
}