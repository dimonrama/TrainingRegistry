using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;

namespace TrainingRegistry.Infrastructure;

public static class RetrainingQuery
{
    public static IQueryable<Enrollment> Pending(AppDbContext db)
        => db.Enrollments.AsNoTracking()
            .Where(e => e.Status == EnrollmentStatus.Completed
                        && e.NextTrainingDate != null
                        && e.Person!.Status == PersonStatus.Active)
            // берём только последнее прохождение по паре сотрудник + программа
            .Where(e => !db.Enrollments.Any(o =>
                o.PersonId == e.PersonId
                && o.Group!.ProgramId == e.Group!.ProgramId
                && o.Status == EnrollmentStatus.Completed
                && (o.CompletedDate > e.CompletedDate || (o.CompletedDate == e.CompletedDate && o.Id > e.Id))))
            // если человек уже записан на эту программу заново - напоминать не нужно
            .Where(e => !db.Enrollments.Any(o =>
                o.PersonId == e.PersonId
                && o.Group!.ProgramId == e.Group!.ProgramId
                && (o.Status == EnrollmentStatus.Enrolled || o.Status == EnrollmentStatus.Studying)));
}