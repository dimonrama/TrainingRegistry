using System.ComponentModel.DataAnnotations;

namespace TrainingRegistry.Domain;

public class Enrollment : AuditableEntity
{
    public int PersonId { get; set; }
    public Person? Person { get; set; }
    public int GroupId { get; set; }
    public TrainingGroup? Group { get; set; }

    [Display(Name = "Статус")]
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Enrolled;

    [Display(Name = "Дата прохождения")]
    public DateOnly? CompletedDate { get; set; }

    [Display(Name = "Дата следующего обучения")]
    public DateOnly? NextTrainingDate { get; set; }

    [StringLength(100)]
    [Display(Name = "Номер удостоверения / сертификата")]
    public string? CertificateNumber { get; set; }
    [Display(Name = "Примечание")]
    public string? Note { get; set; }

    // когда ставим "Прошел обучение" - сразу считаем дату следующего обучения
    public void MarkCompleted(DateOnly completedOn, TrainingProgram program)
    {
        Status = EnrollmentStatus.Completed;
        CompletedDate = completedOn;
        NextTrainingDate = program.CalculateNextTrainingDate(completedOn);
    }
}