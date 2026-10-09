namespace TrainingRegistry.Infrastructure.Documents;

public class ContractSettings
{
    public const string SectionName = "Contract";

    public string City { get; set; } = "Санкт-Петербург";
    public string ExecutorName { get; set; } = "Учебный центр";
    public string ExecutorInn { get; set; } = "";
    public string ExecutorAddress { get; set; } = "";
    public string ExecutorPhone { get; set; } = "";
    public string ExecutorSignerPosition { get; set; } = "директора";
    public string ExecutorSignerName { get; set; } = "";
    public string ExecutorBasis { get; set; } = "Устава";
}

public record CustomerInfo(string Name, bool IsOrganization, string? Inn, string? Kpp, string? Ogrn, string? Address,
    string? Phone, string? Email, string? SignerPosition, string? SignerName);

public record StudentInfo(string FullName, string? Position);

public record ContractDocModel(string Number, DateOnly Date, string ProgramName, string DurationText, string GroupName,
    DateOnly StartDate, DateOnly? EndDate, decimal PricePerPerson, int StudentsCount, decimal TotalPrice,
    CustomerInfo Customer, IReadOnlyList<StudentInfo> Students);
