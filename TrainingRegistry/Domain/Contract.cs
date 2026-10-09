namespace TrainingRegistry.Domain;

public class Contract : AuditableEntity
{
    public string Number { get; set; } = "";
    public int Year { get; set; }
    public int Sequence { get; set; }

    public int GroupId { get; set; }
    public TrainingGroup? Group { get; set; }

    public int? CustomerOrganizationId { get; set; }
    public Organization? CustomerOrganization { get; set; }

    public int? CustomerPersonId { get; set; }
    public Person? CustomerPerson { get; set; }

    public decimal PricePerPerson { get; set; }
    public int StudentsCount { get; set; }
    public decimal TotalPrice { get; set; }
    // в базе только пути к файлам, сами файлы лежат на диске
    public string? DocxPath { get; set; }
    public string? PdfPath { get; set; }
}