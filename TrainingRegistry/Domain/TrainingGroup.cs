using System.ComponentModel.DataAnnotations;

namespace TrainingRegistry.Domain;

public class TrainingGroup : AuditableEntity
{
    [Required(ErrorMessage = "Укажите название группы")]
    [StringLength(300)]
    [Display(Name = "Название группы")]
    public string Name { get; set; } = "";

    [Display(Name = "Программа обучения")]
    public int ProgramId { get; set; }
    public TrainingProgram? Program { get; set; }

    [Display(Name = "Дата начала обучения")]
    public DateOnly StartDate { get; set; }

    [Display(Name = "Дата окончания обучения")]
    public DateOnly? EndDate { get; set; }

    [Display(Name = "Статус группы")]
    public GroupStatus Status { get; set; } = GroupStatus.Recruiting;

    [Display(Name = "Примечание")]
    public string? Note { get; set; }

    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
}
