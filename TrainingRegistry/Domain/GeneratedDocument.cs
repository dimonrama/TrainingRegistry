namespace TrainingRegistry.Domain;

// сформированный приказ, протокол или ведомость; сам файл лежит в хранилище
public class GeneratedDocument : AuditableEntity
{
    public GeneratedDocKind Kind { get; set; }

    public int GroupId { get; set; }
    public TrainingGroup? Group { get; set; }

    // для приказа по иным основаниям: кого отчислили
    public int? PersonId { get; set; }
    public Person? Person { get; set; }

    public string? Number { get; set; }
    public DateOnly? DocDate { get; set; }

    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string? Note { get; set; }

    public override string? AuditTitle => string.IsNullOrWhiteSpace(Number)
        ? Kind.GetDisplayName()
        : $"{Kind.GetDisplayName()} № {Number}";
}
