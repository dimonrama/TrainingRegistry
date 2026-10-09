using System.ComponentModel.DataAnnotations;

namespace TrainingRegistry.Domain;

public class Education : AuditableEntity
{
    public int PersonId { get; set; }
    public Person? Person { get; set; }

    [Display(Name = "Уровень образования")]
    public EducationLevel Level { get; set; }

    [Required(ErrorMessage = "Укажите учебное заведение")]
    [StringLength(300)]
    [Display(Name = "Учебное заведение")]
    public string InstitutionName { get; set; } = "";

    [Range(1950, 2100, ErrorMessage = "Некорректный год окончания")]
    [Display(Name = "Год окончания")]
    public int? GraduationYear { get; set; }

    [StringLength(300)]
    [Display(Name = "Специальность / направление подготовки")]
    public string? Specialty { get; set; }

    public string? FilePath { get; set; }

    public string? OriginalFileName { get; set; }
}
