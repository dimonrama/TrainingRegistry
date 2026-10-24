namespace TrainingRegistry.Domain;

public static class Snils
{
    // оставляем только цифры
    public static string Digits(string? value) => new((value ?? "").Where(char.IsDigit).ToArray());

    // 11 цифр приводим к виду 123-456-789 01, иначе возвращаем как ввели
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var d = Digits(value);
        return d.Length == 11
            ? $"{d.Substring(0, 3)}-{d.Substring(3, 3)}-{d.Substring(6, 3)} {d.Substring(9, 2)}"
            : value.Trim();
    }

    // null - номер в порядке, иначе текст ошибки для пользователя
    public static string? Validate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.Any(c => !char.IsDigit(c) && c != '-' && c != ' '))
            return "СНИЛС может содержать только цифры, пробелы и дефисы";
        var d = Digits(value);
        if (d.Length != 11) return "СНИЛС должен состоять из 11 цифр";
        if (!ControlSumIsValid(d)) return "Контрольное число СНИЛС не сходится, проверьте номер на опечатки";
        return null;
    }

    public static bool ControlSumIsValid(string digits)
    {
        if (digits.Length != 11 || digits.Any(c => !char.IsDigit(c))) return false;
        // у номеров до 001-001-998 контрольное число не считается
        if (long.Parse(digits.Substring(0, 9)) <= 1001998) return true;

        var sum = 0;
        for (var i = 0; i < 9; i++) sum += (digits[i] - '0') * (9 - i);

        int check;
        if (sum < 100) check = sum;
        else if (sum <= 101) check = 0;
        else
        {
            check = sum % 101;
            if (check == 100) check = 0;
        }
        return check == int.Parse(digits.Substring(9, 2));
    }

    // для тестовых данных: из девяти цифр собирает номер с верным контрольным числом
    public static string FromBase(int number)
    {
        var digits = (1001999 + Math.Abs(number) % 900000000).ToString("D9");
        var sum = 0;
        for (var i = 0; i < 9; i++) sum += (digits[i] - '0') * (9 - i);
        var check = sum < 100 ? sum : sum <= 101 ? 0 : sum % 101 == 100 ? 0 : sum % 101;
        return Normalize(digits + check.ToString("D2"))!;
    }
}
