using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;
using TrainingRegistry.Infrastructure.Documents;

namespace TrainingRegistry.Infrastructure;

public class ReportService
{
    private readonly AppDbContext _db;

    public ReportService(AppDbContext db) => _db = db;

    private static string Norm(string? s) => (s ?? "").Trim().ToLower();

    public IQueryable<Enrollment> Query(ReportFilter f)
    {
        var q = _db.Enrollments.AsNoTracking().AsQueryable();

        if (f.OrganizationId != null) q = q.Where(e => e.Person!.OrganizationId == f.OrganizationId);
        if (f.ProgramId != null) q = q.Where(e => e.Group!.ProgramId == f.ProgramId);
        if (f.GroupId != null) q = q.Where(e => e.GroupId == f.GroupId);
        if (f.Status != null) q = q.Where(e => e.Status == f.Status);

        // период: группа пересекается с выбранным отрезком
        if (f.From != null)
        {
            var from = f.From.Value;
            q = q.Where(e => (e.Group!.EndDate ?? e.Group.StartDate) >= from);
        }
        if (f.To != null)
        {
            var to = f.To.Value;
            q = q.Where(e => e.Group!.StartDate <= to);
        }

        if (f.Funding != null)
        {
            var funding = f.Funding;
            q = q.Where(e => (e.FundingSource ?? e.Group!.FundingSource) == funding);
        }

        if (!string.IsNullOrWhiteSpace(f.Certificate))
        {
            var c = Norm(f.Certificate);
            q = q.Where(e => e.CertificateNumber != null && e.CertificateNumber.ToLower().Contains(c));
        }
        if (f.OnlyWithCertificate) q = q.Where(e => e.CertificateNumber != null && e.CertificateNumber != "");

        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = Norm(f.Search);
            var digits = Snils.Digits(f.Search);
            q = q.Where(e =>
                e.Person!.LastName.ToLower().Contains(s) ||
                e.Person.FirstName.ToLower().Contains(s) ||
                (e.Person.MiddleName != null && e.Person.MiddleName.ToLower().Contains(s)) ||
                (digits.Length >= 4 && e.Person.Snils != null && e.Person.Snils.Replace("-", "").Replace(" ", "").Contains(digits)));
        }
        return q;
    }

    public async Task<List<ReportRow>> LoadAsync(ReportFilter f, bool maskPersonal)
    {
        var rows = await Query(f)
            .OrderBy(e => e.Group!.StartDate).ThenBy(e => e.Group!.Name)
            .ThenBy(e => e.Person!.LastName).ThenBy(e => e.Person!.FirstName)
            .Select(e => new
            {
                e.Id, e.PersonId, e.Person!.LastName, e.Person.FirstName, e.Person.MiddleName, e.Person.BirthDate,
                e.Person.Snils, e.Person.Phone, e.Person.Email,
                Org = e.Person.Organization != null ? (e.Person.Organization.ShortName ?? e.Person.Organization.FullName) : null,
                e.Person.Position,
                Program = e.Group!.Program!.Name, Group = e.Group.Name, e.Group.RegistrationNumber,
                e.Group.StartDate, e.Group.EndDate, e.Status, GroupFunding = e.Group.FundingSource, e.FundingSource,
                e.Grade, e.CertificateNumber, e.CompletedDate, e.ExpelledDate, e.ExpelReason, e.NextTrainingDate
            })
            .Take(50000)
            .ToListAsync();

        return rows.Select(r => new ReportRow(r.Id, r.PersonId, r.LastName, r.FirstName, r.MiddleName,
            maskPersonal ? null : r.BirthDate,
            maskPersonal ? ReportFields.MaskSnils(r.Snils) : r.Snils,
            maskPersonal ? null : r.Phone, maskPersonal ? null : r.Email, r.Org, r.Position,
            r.Program, r.Group, r.RegistrationNumber, r.StartDate, r.EndDate, r.Status,
            FundingRules.Effective(r.GroupFunding, r.FundingSource), r.Grade, r.CertificateNumber, r.CompletedDate,
            r.ExpelledDate, r.ExpelReason, r.NextTrainingDate)).ToList();
    }

    // ---- файлы ----

    private static object? Cell(object? v) => v is DateOnly d ? d.ToString("dd.MM.yyyy") : v;

    public GeneratedFile Xlsx(IReadOnlyList<ReportRow> rows, IReadOnlyList<ReportField> fields, string name, bool numbered)
    {
        var headers = (numbered ? new[] { "№ п/п" } : Array.Empty<string>()).Concat(fields.Select(f => f.Title)).ToList();
        var data = rows.Select((r, i) =>
        {
            var cells = fields.Select(f => f.Value(r)).ToList();
            if (numbered) cells.Insert(0, i + 1);
            return cells.ToArray();
        });
        return new GeneratedFile(XlsxWriter.Create("Отчёт", headers, data), $"{name}_{Fmt.Today():yyyy-MM-dd}.xlsx",
            XlsxWriter.ContentType);
    }

    public GeneratedFile Docx(IReadOnlyList<ReportRow> rows, IReadOnlyList<ReportField> fields, string title, string? subtitle, string name)
    {
        var header = new[] { "№" }.Concat(fields.Select(f => f.Title)).ToArray();
        var body = rows.Select((r, i) => new[] { (i + 1).ToString() }
            .Concat(fields.Select(f => Convert.ToString(Cell(f.Value(r)) ?? "") ?? "")).ToArray()).ToList();
        // ширину делим поровну, номеру отдаём поменьше
        var widths = new[] { 0.6 }.Concat(fields.Select(_ => 2.0)).ToArray();
        var blocks = new List<Block> { new TitleBlock(title, subtitle), new TableBlock(header, body, widths) };
        return new GeneratedFile(DocxRenderer.Render(blocks), $"{name}_{Fmt.Today():yyyy-MM-dd}.docx", GroupDocumentService.DocxType);
    }

    public GeneratedFile SummaryXlsx(IReadOnlyList<ReportRow> rows)
    {
        // три сводки на одном листе: по программам, организациям и периодам
        var headers = new[] { "Разрез", "Значение", "Всего", "Обучаются", "Прошли обучение", "Отчислены", "ГЗ", "ПД", "Не указано" };
        var data = new List<object?[]>();
        void Add(string kind, List<SummaryLine> lines)
        {
            foreach (var l in lines)
                data.Add(new object?[] { kind, l.Key, l.Total, l.Studying, l.Completed, l.Expelled, l.StateTask, l.Business, l.NoFunding });
        }
        Add("Программа", ReportSummary.ByProgram(rows));
        Add("Организация", ReportSummary.ByOrganization(rows));
        Add("Период", ReportSummary.ByMonth(rows));
        return new GeneratedFile(XlsxWriter.Create("Сводка", headers, data), $"Сводный_отчёт_{Fmt.Today():yyyy-MM-dd}.xlsx",
            XlsxWriter.ContentType);
    }
}
