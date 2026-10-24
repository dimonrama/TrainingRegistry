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

public enum DocumentType
{
    [Display(Name = "Паспорт")] Passport,
    [Display(Name = "СНИЛС")] Snils,
    [Display(Name = "Диплом об образовании")] Diploma,
    [Display(Name = "Другой документ")] Other
}

public enum FundingSource
{
    [Display(Name = "Государственное задание (ГЗ)")] StateTask,
    [Display(Name = "Предпринимательская деятельность (ПД)")] Business
}

public enum AttestationResult
{
    [Display(Name = "отлично")] Excellent,
    [Display(Name = "хорошо")] Good,
    [Display(Name = "удовлетворительно")] Satisfactory,
    [Display(Name = "зачтено")] Passed,
    [Display(Name = "неудовлетворительно")] Unsatisfactory,
    [Display(Name = "не зачтено")] Failed,
    [Display(Name = "не явился")] Absent
}

public enum GeneratedDocKind
{
    [Display(Name = "Приказ о зачислении")] EnrollmentOrder,
    [Display(Name = "Приказ об отчислении в связи с окончанием обучения")] CompletionOrder,
    [Display(Name = "Приказ об отчислении по иным основаниям")] OtherExpulsionOrder,
    [Display(Name = "Ведомость итоговой аттестации")] AttestationSheet,
    [Display(Name = "Протокол аттестационной комиссии")] Protocol
}
