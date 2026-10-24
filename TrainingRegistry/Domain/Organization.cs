using System.ComponentModel.DataAnnotations;

namespace TrainingRegistry.Domain;

public class Organization : AuditableEntity
{
    [Required(ErrorMessage = "Укажите полное наименование организации")]
    [StringLength(500)]
    [Display(Name = "Полное наименование")]
    public string FullName { get; set; } = "";

    [StringLength(200)]
    [Display(Name = "Краткое наименование")]
    public string? ShortName { get; set; }

    [RegularExpression(@"^(\d{10}|\d{12})$", ErrorMessage = "ИНН должен состоять из 10 или 12 цифр")]
    [Display(Name = "ИНН")]
    public string? Inn { get; set; }

    [RegularExpression(@"^\d{9}$", ErrorMessage = "КПП должен состоять из 9 цифр")]
    [Display(Name = "КПП")]
    public string? Kpp { get; set; }

    [RegularExpression(@"^(\d{13}|\d{15})$", ErrorMessage = "ОГРН должен состоять из 13 или 15 цифр")]
    [Display(Name = "ОГРН")]
    public string? Ogrn { get; set; }

    [StringLength(500)]
    [Display(Name = "Юридический адрес")]
    public string? LegalAddress { get; set; }

    [StringLength(500)]
    [Display(Name = "Фактический адрес")]
    public string? ActualAddress { get; set; }

    [StringLength(50)]
    [Display(Name = "Контактный телефон")]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "Некорректный адрес электронной почты")]
    [StringLength(200)]
    [Display(Name = "Электронная почта")]
    public string? Email { get; set; }

    [StringLength(200)]
    [Display(Name = "ФИО контактного лица")]
    public string? ContactName { get; set; }

    [StringLength(200)]
    [Display(Name = "Должность контактного лица")]
    public string? ContactPosition { get; set; }

    [Display(Name = "Примечание")]
    public string? Note { get; set; }

    public ICollection<Person> Employees { get; set; } = new List<Person>();

    // для списков и договора берём краткое название, если оно есть
    public string DisplayName => string.IsNullOrWhiteSpace(ShortName) ? FullName : ShortName;
    public override string? AuditTitle => DisplayName;
}
