using System.ComponentModel.DataAnnotations;

namespace TrainingRegistry.Domain;

// файл, прикреплённый к карточке обучающегося (паспорт, СНИЛС, диплом, прочее)
public class PersonDocument : AuditableEntity
{
    public int PersonId { get; set; }
    public Person? Person { get; set; }

    [Display(Name = "Тип документа")]
    public DocumentType Type { get; set; }

    // для типа «Другой документ» это название, для остальных можно оставить пустым
    [StringLength(200)]
    [Display(Name = "Название")]
    public string? Title { get; set; }

    public string FilePath { get; set; } = "";
    public string OriginalFileName { get; set; } = "";
    public long FileSize { get; set; }

    public override string? AuditTitle => string.IsNullOrWhiteSpace(Title)
        ? Type.GetDisplayName()
        : $"{Type.GetDisplayName()}: {Title}";
}
