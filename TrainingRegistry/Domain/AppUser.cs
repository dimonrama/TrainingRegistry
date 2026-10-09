using System.ComponentModel.DataAnnotations;

namespace TrainingRegistry.Domain;

public class AppUser : AuditableEntity
{
    [Required(ErrorMessage = "Укажите ФИО")]
    [StringLength(200)]
    [Display(Name = "ФИО")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Укажите логин")]
    [StringLength(100)]
    [Display(Name = "Логин")]
    public string Login { get; set; } = "";

    public string PasswordHash { get; set; } = "";

    [Display(Name = "Роль")]
    public UserRole Role { get; set; } = UserRole.Methodist;

    [Display(Name = "Заблокирован")]
    public bool IsBlocked { get; set; }

    [Display(Name = "Последняя авторизация")]
    public DateTime? LastLoginAt { get; set; }

    [Display(Name = "Может изменять организации")]
    public bool CanEditOrganizations { get; set; }

    [Display(Name = "Может изменять сотрудников")]
    public bool CanEditPeople { get; set; }
}
