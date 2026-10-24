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

    [StringLength(50)]
    [Display(Name = "Регистрационный номер группы")]
    public string? RegistrationNumber { get; set; }

    [Display(Name = "Направление финансирования")]
    public FundingSource? FundingSource { get; set; }

    [StringLength(200)]
    [Display(Name = "Куратор группы")]
    public string? CuratorName { get; set; }

    [Display(Name = "Члены аттестационной комиссии (по одному в строке)")]
    public string? CommissionMembers { get; set; }

    [StringLength(200)]
    [Display(Name = "Секретарь комиссии")]
    public string? CommissionSecretary { get; set; }

    [Display(Name = "Примечание")]
    public string? Note { get; set; }

    public override string? AuditTitle => Name;

    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
    public ICollection<GeneratedDocument> Documents { get; set; } = new List<GeneratedDocument>();
}
