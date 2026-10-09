using System.ComponentModel.DataAnnotations;

namespace TrainingRegistry.Domain;

public enum UserRole
{
    [Display(Name = "Администратор")] Admin,
    [Display(Name = "Методист")] Methodist
}

public enum PersonStatus
{
    [Display(Name = "Активен")] Active,
    [Display(Name = "Уволен")] Dismissed
}

public enum EducationLevel
{
    [Display(Name = "Среднее общее")] SecondaryGeneral,
    [Display(Name = "Средне-профессиональное")] SecondaryVocational,
    [Display(Name = "Высшее")] Higher,
    [Display(Name = "Иное")] Other
}

public enum RequiredEducation
{
    [Display(Name = "Не требуется")] None,
    [Display(Name = "Средне-профессиональное")] SecondaryVocational,
    [Display(Name = "Высшее")] Higher
}

public enum Recurrence
{
    [Display(Name = "Не требуется")] None,
    [Display(Name = "1 год")] OneYear,
    [Display(Name = "3 года")] ThreeYears,
    [Display(Name = "5 лет")] FiveYears,
    [Display(Name = "Произвольный срок (в месяцах)")] Custom
}

public enum DurationUnit
{
    [Display(Name = "часов")] Hours,
    [Display(Name = "дней")] Days
}

public enum ProgramStatus
{
    [Display(Name = "Активна")] Active,
    [Display(Name = "Архив")] Archived
}

public enum GroupStatus
{
    [Display(Name = "Набор")] Recruiting,
    [Display(Name = "Идет обучение")] InProgress,
    [Display(Name = "Завершена")] Completed,
    [Display(Name = "Отменена")] Cancelled
}

public enum EnrollmentStatus
{
    [Display(Name = "Зачислен")] Enrolled,
    [Display(Name = "Обучается")] Studying,
    [Display(Name = "Прошел обучение")] Completed,
    [Display(Name = "Отчислен")] Expelled
}
