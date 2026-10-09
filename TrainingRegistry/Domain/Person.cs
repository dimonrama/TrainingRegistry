using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TrainingRegistry.Domain;

public class Person : AuditableEntity
{
    [Required(ErrorMessage = "Укажите фамилию")]
    [StringLength(100)]
    [Display(Name = "Фамилия")]
    public string LastName { get; set; } = "";
    [Required(ErrorMessage = "Укажите имя")]
    [StringLength(100)]
    [Display(Name = "Имя")]
    public string FirstName { get; set; } = "";
    [StringLength(100)]
    [Display(Name = "Отчество")]
    public string? MiddleName { get; set; }
    [Display(Name = "Дата рождения")]
    public DateOnly? BirthDate { get; set; }
    [StringLength(50)]
    [Display(Name = "Телефон")]
    public string? Phone { get; set; }
    [EmailAddress(ErrorMessage = "Некорректный адрес электронной почты")]
    [StringLength(200)]
    [Display(Name = "Электронная почта")]
    public string? Email { get; set; }
    [Display(Name = "Организация")]
    public int? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    [StringLength(200)]
    [Display(Name = "Должность")]
    public string? Position { get; set; }
    [Display(Name = "Статус")]
    public PersonStatus Status { get; set; } = PersonStatus.Active;
    [Display(Name = "Дата увольнения")]
    public DateOnly? DismissalDate { get; set; }
    public ICollection<Education> Educations { get; set; } = new List<Education>();
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    [NotMapped]
    public string FullName => string.Join(" ", new[] { LastName, FirstName, MiddleName }
        .Where(s => !string.IsNullOrWhiteSpace(s)));
    [NotMapped]
    public bool IsDismissed => Status == PersonStatus.Dismissed;
}