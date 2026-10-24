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

    [Display(Name = "Направление финансирования")]
    public FundingSource? FundingSource { get; set; }

    [Display(Name = "Результат итоговой аттестации")]
    public AttestationResult? Grade { get; set; }

    [Display(Name = "Дата отчисления")]
    public DateOnly? ExpelledDate { get; set; }

    [StringLength(500)]
    [Display(Name = "Причина отчисления")]
    public string? ExpelReason { get; set; }

    // когда ставим "Прошел обучение" - сразу считаем дату следующего обучения
    public void MarkCompleted(DateOnly completedOn, TrainingProgram program)
    {
        Status = EnrollmentStatus.Completed;
        CompletedDate = completedOn;
        NextTrainingDate = program.CalculateNextTrainingDate(completedOn);
    }
}