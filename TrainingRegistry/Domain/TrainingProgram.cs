using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TrainingRegistry.Domain;

public class TrainingProgram : AuditableEntity
{
    // по ТЗ если не задано - 60 дней
    public const int DefaultNotifyDaysBefore = 60;
    [Required(ErrorMessage = "Укажите наименование программы")]
    [StringLength(300)]
    [Display(Name = "Наименование программы")]
    public string Name { get; set; } = "";
    [Display(Name = "Описание")]
    public string? Description { get; set; }

    [Range(0, 100000000, ErrorMessage = "Стоимость не может быть отрицательной")]
    [Display(Name = "Стоимость за одного обучающегося, руб.")]
    public decimal Price { get; set; }
    [Display(Name = "Требования к образованию")]
    public RequiredEducation RequiredEducation { get; set; } = RequiredEducation.None;

    [Display(Name = "Периодичность повторного обучения")]
    public Recurrence Recurrence { get; set; } = Recurrence.None;

    [Range(1, 600, ErrorMessage = "Укажите срок от 1 до 600 месяцев")]
    [Display(Name = "Срок повторного обучения, месяцев")]
    public int? CustomMonths { get; set; }
    [Range(1, 10000)]
    [Display(Name = "Срок обучения")]
    public int? Duration { get; set; }
    [Display(Name = "Единица срока")]
    public DurationUnit DurationUnit { get; set; } = DurationUnit.Hours;

    [Display(Name = "Статус программы")]
    public ProgramStatus Status { get; set; } = ProgramStatus.Active;

    [Range(1, 3650, ErrorMessage = "Укажите от 1 до 3650 дней")]
    [Display(Name = "Заблаговременность уведомления, дней")]
    public int? NotifyDaysBefore { get; set; }
    // какие поля карточки должны быть заполнены до зачисления (набор флагов)
    [Display(Name = "Обязательные данные для зачисления")]
    public PersonField RequiredFields { get; set; } = PersonField.None;
    public ICollection<TrainingGroup> Groups { get; set; } = new List<TrainingGroup>();
    public override string? AuditTitle => Name;
    // академические часы известны, только если срок задан в часах
    [NotMapped]
    public int? AcademicHours => DurationUnit == DurationUnit.Hours ? Duration : null;
    [NotMapped]
    public int EffectiveNotifyDays => NotifyDaysBefore ?? DefaultNotifyDaysBefore;
    [NotMapped]
    public string RecurrenceText => Infrastructure.Fmt.RecurrenceLabel(Recurrence, CustomMonths);
    // переводим выбранную периодичность в месяцы
    public int? GetRecurrenceMonths() => Recurrence switch
    {
        Recurrence.OneYear => 12,
        Recurrence.ThreeYears => 36,
        Recurrence.FiveYears => 60,
        Recurrence.Custom => CustomMonths,
        _ => null
    };
    // дата прохождения + периодичность, а если повторное обучение не нужно - null
    public DateOnly? CalculateNextTrainingDate(DateOnly completedOn)
    {
        var months = GetRecurrenceMonths();
        return months is > 0 ? completedOn.AddMonths(months.Value) : null;
    }
}