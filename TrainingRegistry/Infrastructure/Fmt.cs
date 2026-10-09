using System.Globalization;
using TrainingRegistry.Domain;

namespace TrainingRegistry.Infrastructure;

public static class Fmt
{
    private static readonly CultureInfo Ru = new("ru-RU");
    private static readonly TimeZoneInfo Msk = FindMoscow();

    private static TimeZoneInfo FindMoscow()
    {
        foreach (var id in new[] { "Russian Standard Time", "Europe/Moscow" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("MSK", TimeSpan.FromHours(3), "MSK", "MSK");
    }

    // текущая дата по Москве, а не по часам сервера
    public static DateOnly Today()
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Msk));

    public static DateOnly ToLocalDate(DateTime utc)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Msk));

    // в базе время в UTC, а даты в фильтрах вводят по Москве
    public static DateTime StartOfDayUtc(DateOnly date)
        => TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), Msk);

    public static string Date(DateOnly? date) => date?.ToString("dd.MM.yyyy") ?? "—";

    public static string DateTimeLocal(DateTime? utc)
    {
        if (utc == null) return "—";
        var value = DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(value, Msk).ToString("dd.MM.yyyy, HH:mm");
    }

    public static string Money(decimal value) => value.ToString("N2", Ru) + " руб.";

    public static string FullName(string last, string first, string? middle)
        => string.Join(" ", new[] { last, first, middle }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public static string RecurrenceLabel(Recurrence recurrence, int? customMonths)
        => recurrence == Domain.Recurrence.Custom
            ? (customMonths is > 0 ? $"{customMonths} мес." : "Произвольный срок")
            : recurrence.GetDisplayName();
}
