using System.Text;

namespace TrainingRegistry.Infrastructure;

public static class RuWords
{
    private static readonly string[] Ones = { "", "один", "два", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять" };
    private static readonly string[] OnesFem = { "", "одна", "две", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять" };
    private static readonly string[] Teens = { "десять", "одиннадцать", "двенадцать", "тринадцать", "четырнадцать", "пятнадцать", "шестнадцать", "семнадцать", "восемнадцать", "девятнадцать" };
    private static readonly string[] Tens = { "", "", "двадцать", "тридцать", "сорок", "пятьдесят", "шестьдесят", "семьдесят", "восемьдесят", "девяносто" };
    private static readonly string[] Hundreds = { "", "сто", "двести", "триста", "четыреста", "пятьсот", "шестьсот", "семьсот", "восемьсот", "девятьсот" };

    // склонение: 1 рубль, 2 рубля, 5 рублей
    public static string Plural(long n, string one, string few, string many)
    {
        var abs = Math.Abs(n);
        var last2 = abs % 100;
        // 11-14 всегда "много"
        if (last2 is >= 11 and <= 14) return many;
        return (abs % 10) switch { 1 => one, 2 or 3 or 4 => few, _ => many };
    }

    private static string Triple(int n, bool feminine)
    {
        var parts = new List<string>();
        if (n >= 100) parts.Add(Hundreds[n / 100]);
        var rest = n % 100;
        if (rest >= 10 && rest < 20)
        {
            parts.Add(Teens[rest - 10]);
        }
        else
        {
            if (rest >= 20) parts.Add(Tens[rest / 10]);
            var unit = rest % 10;
            if (unit > 0) parts.Add(feminine ? OnesFem[unit] : Ones[unit]);
        }
        return string.Join(" ", parts);
    }

    public static string Number(long value, bool feminine = false)
    {
        if (value == 0) return "ноль";
        if (value < 0) return "минус " + Number(-value, feminine);

        var scales = new (long Divisor, string One, string Few, string Many, bool Fem)[]
        {
            (1_000_000_000_000, "триллион", "триллиона", "триллионов", false),
            (1_000_000_000, "миллиард", "миллиарда", "миллиардов", false),
            (1_000_000, "миллион", "миллиона", "миллионов", false),
            (1_000, "тысяча", "тысячи", "тысяч", true)
        };

        var sb = new List<string>();
        foreach (var s in scales)
        {
            var part = (int)(value / s.Divisor % 1000);
            if (part == 0) continue;
            sb.Add(Triple(part, s.Fem));
            sb.Add(Plural(part, s.One, s.Few, s.Many));
        }

        var tail = (int)(value % 1000);
        if (tail > 0) sb.Add(Triple(tail, feminine));

        return string.Join(" ", sb);
    }

    // сумма прописью для договора
    public static string Rubles(decimal amount)
    {
        amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        var rubles = (long)Math.Truncate(amount);
        var kopecks = (int)Math.Round((amount - rubles) * 100, MidpointRounding.AwayFromZero);

        var text = $"{Number(rubles)} {Plural(rubles, "рубль", "рубля", "рублей")} " +
                   $"{kopecks:00} {Plural(kopecks, "копейка", "копейки", "копеек")}";
        return char.ToUpperInvariant(text[0]) + text[1..];
    }
}
