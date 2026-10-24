using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;

namespace TrainingRegistry.Infrastructure;

// ищет в базе записи, которые могут оказаться тем же человеком
public class PersonDuplicateService
{
    private readonly AppDbContext _db;

    public PersonDuplicateService(AppDbContext db) => _db = db;

    public record Match(int PersonId, string FullName, string? Organization, DateOnly? BirthDate, string? Snils,
        List<string> Reasons)
    {
        public string ReasonsText => string.Join(", ", Reasons);
    }

    private static string Norm(string? s) => (s ?? "").Trim().ToLower().Replace('ё', 'е');

    public async Task<List<Match>> FindAsync(PersonKey candidate, int? excludeId)
    {
        var snils = Snils.Normalize(candidate.Snils);
        var hasSnils = snils != null && Snils.Digits(snils).Length == 11;
        var email = string.IsNullOrWhiteSpace(candidate.Email) ? null : candidate.Email.Trim().ToLower();
        var last = Norm(candidate.LastName);
        var first = Norm(candidate.FirstName);

        // сначала отбираем в базе по трём признакам, потом точно сверяем в памяти
        var rows = await _db.People.AsNoTracking()
            .Where(p => (excludeId == null || p.Id != excludeId) &&
                        ((hasSnils && p.Snils == snils) ||
                         (email != null && p.Email != null && p.Email.ToLower() == email) ||
                         (p.LastName.ToLower().Replace("ё", "е") == last && p.FirstName.ToLower().Replace("ё", "е") == first)))
            .Select(p => new
            {
                p.Id, p.LastName, p.FirstName, p.MiddleName, p.BirthDate, p.Snils, p.Email,
                Org = p.Organization != null ? (p.Organization.ShortName ?? p.Organization.FullName) : null
            })
            .Take(200)
            .ToListAsync();

        var result = new List<Match>();
        foreach (var r in rows)
        {
            var key = new PersonKey(r.Id, r.LastName, r.FirstName, r.MiddleName, r.BirthDate, r.Snils, r.Email);
            var reasons = DuplicateRules.Reasons(candidate, key);
            if (reasons.Count == 0) continue;
            result.Add(new Match(r.Id, key.FullName, r.Org, r.BirthDate, r.Snils, reasons));
        }
        return result.OrderByDescending(m => m.Reasons.Count).ThenBy(m => m.FullName).ToList();
    }

    // весь список для массовой проверки при импорте: ключи в памяти, чтобы не делать запрос на каждую строку
    public async Task<DuplicateIndex> LoadIndexAsync()
    {
        var rows = await _db.People.AsNoTracking()
            .Select(p => new { p.Id, p.LastName, p.FirstName, p.MiddleName, p.BirthDate, p.Snils, p.Email })
            .ToListAsync();
        return new DuplicateIndex(rows.Select(r =>
            new PersonKey(r.Id, r.LastName, r.FirstName, r.MiddleName, r.BirthDate, r.Snils, r.Email)));
    }
}

public class DuplicateIndex
{
    private readonly Dictionary<string, List<PersonKey>> _bySnils = new();
    private readonly Dictionary<string, List<PersonKey>> _byName = new();
    private readonly Dictionary<string, List<PersonKey>> _byEmail = new();

    public DuplicateIndex(IEnumerable<PersonKey> people)
    {
        foreach (var p in people) Add(p);
    }

    private static string Norm(string? s) => (s ?? "").Trim().ToLowerInvariant().Replace('ё', 'е');
    private static string NameKey(PersonKey p) => Norm(p.LastName) + "|" + Norm(p.FirstName);

    private static void Put(Dictionary<string, List<PersonKey>> map, string key, PersonKey p)
    {
        if (key.Length == 0) return;
        if (!map.TryGetValue(key, out var list)) map[key] = list = new List<PersonKey>();
        list.Add(p);
    }

    public void Add(PersonKey p)
    {
        var digits = Snils.Digits(p.Snils);
        if (digits.Length == 11) Put(_bySnils, digits, p);
        Put(_byName, NameKey(p), p);
        if (!string.IsNullOrWhiteSpace(p.Email)) Put(_byEmail, Norm(p.Email), p);
    }

    // совпадения для кандидата: сам объект и причины
    public List<(PersonKey Person, List<string> Reasons)> Find(PersonKey candidate)
    {
        var found = new Dictionary<int, PersonKey>();
        var digits = Snils.Digits(candidate.Snils);
        void Collect(Dictionary<string, List<PersonKey>> map, string key)
        {
            if (key.Length > 0 && map.TryGetValue(key, out var list))
                foreach (var p in list) found[p.Id] = p;
        }
        if (digits.Length == 11) Collect(_bySnils, digits);
        Collect(_byName, NameKey(candidate));
        if (!string.IsNullOrWhiteSpace(candidate.Email)) Collect(_byEmail, Norm(candidate.Email));

        var result = new List<(PersonKey, List<string>)>();
        foreach (var p in found.Values)
        {
            var reasons = DuplicateRules.Reasons(candidate, p);
            if (reasons.Count > 0) result.Add((p, reasons));
        }
        return result;
    }
}
