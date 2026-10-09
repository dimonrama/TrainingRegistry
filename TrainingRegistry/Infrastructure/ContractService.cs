using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;
using TrainingRegistry.Infrastructure.Documents;

namespace TrainingRegistry.Infrastructure;

public class ContractService
{
    private const string Folder = "contracts";
    private readonly AppDbContext _db;
    private readonly IFileStorage _storage;
    private readonly ContractSettings _settings;

    public ContractService(AppDbContext db, IFileStorage storage, IOptions<ContractSettings> settings)
    {
        _db = db;
        _storage = storage;
        _settings = settings.Value;
    }
    public record CustomerOption(int? OrganizationId, int? PersonId, string Name, int Students, int? ContractId, string? ContractNumber);
    public async Task<List<CustomerOption>> GetCustomersAsync(int groupId)
    {
        var members = await _db.Enrollments.AsNoTracking()
            .Where(e => e.GroupId == groupId && e.Status != EnrollmentStatus.Expelled)
            .Select(e => new
            {
                e.PersonId,
                e.Person!.LastName, e.Person.FirstName, e.Person.MiddleName,
                e.Person.OrganizationId,
                OrgName = e.Person.Organization != null ? (e.Person.Organization.ShortName ?? e.Person.Organization.FullName) : null
            })
            .ToListAsync();
        var contracts = await _db.Contracts.AsNoTracking()
            .Where(c => c.GroupId == groupId)
            .Select(c => new { c.Id, c.Number, c.CustomerOrganizationId, c.CustomerPersonId })
            .ToListAsync();
        var result = new List<CustomerOption>();
        foreach (var org in members.Where(m => m.OrganizationId != null).GroupBy(m => m.OrganizationId))
        {
            var contract = contracts.FirstOrDefault(c => c.CustomerOrganizationId == org.Key);
            result.Add(new CustomerOption(org.Key, null, org.First().OrgName ?? "Организация", org.Count(),
                contract?.Id, contract?.Number));
        }
        foreach (var person in members.Where(m => m.OrganizationId == null).OrderBy(m => m.LastName))
        {
            var contract = contracts.FirstOrDefault(c => c.CustomerPersonId == person.PersonId);
            result.Add(new CustomerOption(null, person.PersonId,
                Fmt.FullName(person.LastName, person.FirstName, person.MiddleName) + " (физическое лицо)", 1,
                contract?.Id, contract?.Number));
        }
        return result;
    }
    public async Task<(Contract? Contract, string? Error)> CreateAsync(int groupId, int? organizationId, int? personId)
    {
        if ((organizationId == null) == (personId == null))
            return (null, "Укажите заказчика: организацию или физическое лицо");
        var group = await _db.Groups.AsNoTracking().Include(g => g.Program).FirstOrDefaultAsync(g => g.Id == groupId);
        if (group == null) return (null, "Группа не найдена");
        if (group.Status == GroupStatus.Cancelled) return (null, "Для отменённой группы договор не формируется");

        var existing = await _db.Contracts.AsNoTracking()
            .Where(c => c.GroupId == groupId
                        && (organizationId != null ? c.CustomerOrganizationId == organizationId : c.CustomerPersonId == personId))
            .Select(c => c.Number)
            .FirstOrDefaultAsync();
        // на одного заказчика в группе один договор, дубли не делаем
        if (existing != null) return (null, $"Договор для этого заказчика уже сформирован: {existing}");
        var customer = await LoadCustomerAsync(organizationId, personId);
        if (customer == null) return (null, "Заказчик не найден");
        var students = await LoadStudentsAsync(groupId, organizationId, personId);
        if (students.Count == 0) return (null, "У этого заказчика нет обучающихся в группе");
        var program = group.Program!;
        var today = Fmt.Today();
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var year = today.Year;
            // номер Д-год-0001: берём последний за год, вместе с удалёнными, чтобы номера не повторялись
            var last = await _db.Contracts.IgnoreQueryFilters()
                .Where(c => c.Year == year)
                .MaxAsync(c => (int?)c.Sequence) ?? 0;
            var sequence = last + 1;
            var number = $"Д-{year}-{sequence:0000}";
            var contract = new Contract
            {
                Number = number,
                Year = year,
                Sequence = sequence,
                GroupId = groupId,
                CustomerOrganizationId = organizationId,
                CustomerPersonId = personId,
                PricePerPerson = program.Price,
                StudentsCount = students.Count,
                TotalPrice = program.Price * students.Count
            };
            var (docx, pdf) = BuildFiles(contract, group, program, customer, students, today);
            contract.DocxPath = await _storage.SaveBytesAsync(docx, Folder, ".docx");
            contract.PdfPath = await _storage.SaveBytesAsync(pdf, Folder, ".pdf");

            _db.Contracts.Add(contract);
            try
            {
                await _db.SaveChangesAsync();
                return (contract, null);
            }
            catch (DbUpdateException)
            {
                _storage.Delete(contract.DocxPath);
                _storage.Delete(contract.PdfPath);
                _db.ChangeTracker.Clear();
            }
        }
        return (null, "Не удалось получить номер договора, повторите попытку");
    }
    // пересобираем файлы, если поменялся состав группы
    public async Task<string?> RegenerateAsync(int contractId)
    {
        var contract = await _db.Contracts.FirstOrDefaultAsync(c => c.Id == contractId);
        if (contract == null) return "Договор не найден";
        var group = await _db.Groups.AsNoTracking().Include(g => g.Program).FirstOrDefaultAsync(g => g.Id == contract.GroupId);
        if (group == null) return "Группа договора не найдена";
        var customer = await LoadCustomerAsync(contract.CustomerOrganizationId, contract.CustomerPersonId);
        if (customer == null) return "Заказчик не найден";
        var students = await LoadStudentsAsync(contract.GroupId, contract.CustomerOrganizationId, contract.CustomerPersonId);
        if (students.Count == 0) return "У заказчика не осталось обучающихся в группе";
        contract.StudentsCount = students.Count;
        contract.TotalPrice = contract.PricePerPerson * students.Count;
        var (docx, pdf) = BuildFiles(contract, group, group.Program!, customer, students, Fmt.ToLocalDate(contract.CreatedAt));
        var oldDocx = contract.DocxPath;
        var oldPdf = contract.PdfPath;
        contract.DocxPath = await _storage.SaveBytesAsync(docx, Folder, ".docx");
        contract.PdfPath = await _storage.SaveBytesAsync(pdf, Folder, ".pdf");
        await _db.SaveChangesAsync();
        _storage.Delete(oldDocx);
        _storage.Delete(oldPdf);
        return null;
    }

    public async Task<List<StudentInfo>> LoadStudentsAsync(int groupId, int? organizationId, int? personId)
    {
        var query = _db.Enrollments.AsNoTracking()
            .Where(e => e.GroupId == groupId && e.Status != EnrollmentStatus.Expelled);
        query = organizationId != null
            ? query.Where(e => e.Person!.OrganizationId == organizationId)
            : query.Where(e => e.PersonId == personId);
        var rows = await query
            .OrderBy(e => e.Person!.LastName).ThenBy(e => e.Person!.FirstName)
            .Select(e => new { e.Person!.LastName, e.Person.FirstName, e.Person.MiddleName, e.Person.Position })
            .ToListAsync();
        return rows.Select(r => new StudentInfo(Fmt.FullName(r.LastName, r.FirstName, r.MiddleName), r.Position)).ToList();
    }
    private async Task<CustomerInfo?> LoadCustomerAsync(int? organizationId, int? personId)
    {
        if (organizationId != null)
        {
            var o = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == organizationId);
            if (o == null) return null;
            var address = !string.IsNullOrWhiteSpace(o.LegalAddress) ? o.LegalAddress : o.ActualAddress;
            return new CustomerInfo(o.FullName, true, o.Inn, o.Kpp, o.Ogrn, address, o.Phone, o.Email,
                o.ContactPosition, o.ContactName);
        }
        var p = await _db.People.AsNoTracking().FirstOrDefaultAsync(x => x.Id == personId);
        if (p == null) return null;
        return new CustomerInfo(p.FullName, false, null, null, null, null, p.Phone, p.Email, null, p.FullName);
    }
    // docx и pdf делаем из одних и тех же блоков, чтобы тексты совпадали
    private (byte[] Docx, byte[] Pdf) BuildFiles(Contract contract, TrainingGroup group, TrainingProgram program,
        CustomerInfo customer, List<StudentInfo> students, DateOnly date)
    {
        var duration = program.Duration == null ? "по программе" : $"{program.Duration} {program.DurationUnit.GetDisplayName()}";
        var model = new ContractDocModel(contract.Number, date, program.Name, duration, group.Name, group.StartDate,
            group.EndDate, contract.PricePerPerson, contract.StudentsCount, contract.TotalPrice, customer, students);
        var blocks = ContractBlocks.Build(model, _settings);
        return (DocxRenderer.Render(blocks), PdfRenderer.Render(blocks, $"Договор № {contract.Number}"));
    }
}