using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using TrainingRegistry.Domain;
using TrainingRegistry.Infrastructure;
using TrainingRegistry.Security;

namespace TrainingRegistry.Data;

public class AppDbContext : DbContext
{
    private readonly ICurrentUser _currentUser;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser)
        : base(options)
    {
        _currentUser = currentUser;
    }

    // нужно только для массовой загрузки тестовых данных, иначе журнал забьётся
    public bool SuppressAudit { get; set; }
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<Education> Educations => Set<Education>();
    public DbSet<TrainingProgram> Programs => Set<TrainingProgram>();
    public DbSet<TrainingGroup> Groups => Set<TrainingGroup>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<PersonDocument> PersonDocuments => Set<PersonDocument>();
    public DbSet<GeneratedDocument> GeneratedDocuments => Set<GeneratedDocument>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        // на все сущности вешаем фильтр, чтобы удалённые записи нигде не показывались
        foreach (var entityType in b.Model.GetEntityTypes()
                     .Where(t => typeof(AuditableEntity).IsAssignableFrom(t.ClrType)))
        {
            var param = Expression.Parameter(entityType.ClrType, "e");
            var notDeleted = Expression.Equal(
                Expression.Property(param, nameof(AuditableEntity.IsDeleted)),
                Expression.Constant(false));
            entityType.SetQueryFilter(Expression.Lambda(notDeleted, param));
        }
        b.Entity<Organization>(e =>
        {
            e.HasIndex(x => x.FullName);
            e.HasIndex(x => x.Inn);
            e.Ignore(x => x.DisplayName);
        });
        b.Entity<Person>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.LastName);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.Snils);
            e.Ignore(x => x.AuditTitle);
            e.HasOne(x => x.Organization).WithMany(o => o.Employees)
                .HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Education>(e =>
        {
            e.Property(x => x.Level).HasConversion<string>().HasMaxLength(30);
            e.HasOne(x => x.Person).WithMany(p => p.Educations)
                .HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<PersonDocument>(e =>
        {
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Person).WithMany(p => p.Documents)
                .HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.PersonId, x.Type });
        });
        b.Entity<GeneratedDocument>(e =>
        {
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Number).HasMaxLength(50);
            e.HasOne(x => x.Group).WithMany(g => g.Documents)
                .HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Person).WithMany()
                .HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<TrainingProgram>(e =>
        {
            e.ToTable("Programs");
            e.Ignore(x => x.AcademicHours);
            e.Property(x => x.Price).HasPrecision(12, 2);
            e.Property(x => x.RequiredEducation).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Recurrence).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.DurationUnit).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Ignore(x => x.EffectiveNotifyDays);
            e.Ignore(x => x.RecurrenceText);
            e.HasIndex(x => x.Name);
        });
        b.Entity<TrainingGroup>(e =>
        {
            e.ToTable("Groups");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.FundingSource).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.RegistrationNumber);
            e.HasOne(x => x.Program).WithMany(p => p.Groups)
                .HasForeignKey(x => x.ProgramId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Enrollment>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.FundingSource).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Grade).HasConversion<string>().HasMaxLength(30);
            e.HasIndex(x => x.CertificateNumber);
            e.HasOne(x => x.Person).WithMany(p => p.Enrollments)
                .HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Group).WithMany(g => g.Enrollments)
                .HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
            // один обучающийся - одна запись в группе, но только среди неудалённых, иначе после удаления нельзя зачислить заново
            e.HasIndex(x => new { x.PersonId, x.GroupId })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");
            // по этим полям строится список уведомлений
            e.HasIndex(x => new { x.Status, x.NextTrainingDate });
        });
        b.Entity<Contract>(e =>
        {
            e.Property(x => x.PricePerPerson).HasPrecision(12, 2);
            e.Property(x => x.TotalPrice).HasPrecision(14, 2);
            e.HasIndex(x => x.Number).IsUnique();
            e.HasOne(x => x.Group).WithMany(g => g.Contracts)
                .HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CustomerOrganization).WithMany()
                .HasForeignKey(x => x.CustomerOrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CustomerPerson).WithMany()
                .HasForeignKey(x => x.CustomerPersonId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<AppUser>(e =>
        {
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Login).IsUnique().HasFilter("\"IsDeleted\" = false");
        });
        b.Entity<AuditLog>(e =>
        {
            e.HasIndex(x => x.At);
            e.Property(x => x.EntityTitle).HasMaxLength(300);
            e.Property(x => x.Details).HasMaxLength(1000);
        });
    }
    // поля, изменения которых в журнал не пишем
    private static readonly HashSet<string> AuditSkip = new()
    {
        nameof(AuditableEntity.Id), nameof(AuditableEntity.CreatedAt), nameof(AuditableEntity.CreatedBy),
        nameof(AuditableEntity.UpdatedAt), nameof(AuditableEntity.UpdatedBy), nameof(AuditableEntity.IsDeleted),
        "PasswordHash", "FilePath", "DocxPath", "PdfPath"
    };

    private record JournalItem(AuditableEntity Entity, string Type, string Action, string? Title, string? Details);

    // все сохранения проходят тут: ставим кто и когда изменил, удаление делаем логическим и пишем журнал
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var who = _currentUser.DisplayName;
        var journal = new List<JournalItem>();
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>().ToList())
        {
            var entity = entry.Entity;
            var type = entry.Metadata.ClrType.Name;
            switch (entry.State)
            {
                case EntityState.Added:
                    entity.CreatedAt = now;
                    entity.CreatedBy = who;
                    entity.UpdatedAt = now;
                    entity.UpdatedBy = who;
                    journal.Add(new JournalItem(entity, type, type == nameof(Enrollment) ? "Зачисление" : "Создание",
                        entity.AuditTitle, null));
                    break;
                case EntityState.Modified:
                    // что изменилось, считаем до того, как служебные поля поменяются
                    var details = DescribeChanges(entry);
                    entry.Property(e => e.CreatedAt).IsModified = false;
                    entry.Property(e => e.CreatedBy).IsModified = false;
                    entity.UpdatedAt = now;
                    entity.UpdatedBy = who;
                    var becameDeleted = entity.IsDeleted && !entry.Property(e => e.IsDeleted).OriginalValue;
                    var action = becameDeleted
                        ? (type == nameof(Enrollment) ? "Исключение из группы" : "Удаление")
                        : (type == nameof(Enrollment) && ExpelledNow(entry) ? "Отчисление" : "Изменение");
                    journal.Add(new JournalItem(entity, type, action, entity.AuditTitle, becameDeleted ? null : details));
                    break;
                // физически ничего не удаляем, только помечаем
                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entity.IsDeleted = true;
                    entity.UpdatedAt = now;
                    entity.UpdatedBy = who;
                    journal.Add(new JournalItem(entity, type, type == nameof(Enrollment) ? "Исключение из группы" : "Удаление",
                        entity.AuditTitle, null));
                    break;
            }
        }
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        // id у новых записей появляется только после сохранения, поэтому журнал пишем вторым шагом
        if (journal.Count > 0 && !SuppressAudit)
        {
            var enrollmentTitles = await EnrollmentTitlesAsync(
                journal.Where(j => j.Entity is Enrollment && j.Title == null).Select(j => j.Entity.Id).ToList(),
                cancellationToken);
            foreach (var item in journal)
            {
                var title = item.Title;
                if (title == null && item.Entity is Enrollment) enrollmentTitles.TryGetValue(item.Entity.Id, out title);
                AuditLogs.Add(new AuditLog
                {
                    At = now,
                    UserName = who,
                    Action = item.Action,
                    EntityType = item.Type,
                    EntityId = item.Entity.Id,
                    EntityTitle = Cut(title, 300),
                    Details = Cut(item.Details, 1000)
                });
            }
            await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        return result;
    }

    // отдельная запись в журнал для действий, которые не меняют данные (формирование документа, выгрузка, импорт)
    public async Task LogAsync(string action, string entityType, int entityId, string? title = null, string? details = null)
    {
        AuditLogs.Add(new AuditLog
        {
            At = DateTime.UtcNow,
            UserName = _currentUser.DisplayName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            EntityTitle = Cut(title, 300),
            Details = Cut(details, 1000)
        });
        await SaveChangesAsync();
    }

    private static string? Cut(string? text, int max)
        => text == null || text.Length <= max ? text : text[..(max - 1)] + "…";

    private static bool ExpelledNow(EntityEntry entry)
    {
        var status = entry.Property(nameof(Enrollment.Status));
        return status.IsModified && Equals(status.CurrentValue, EnrollmentStatus.Expelled)
                                 && !Equals(status.OriginalValue, EnrollmentStatus.Expelled);
    }

    // для записи об обучении название собираем из ФИО и группы
    private async Task<Dictionary<int, string>> EnrollmentTitlesAsync(List<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new Dictionary<int, string>();
        var rows = await Enrollments.IgnoreQueryFilters().AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .Select(e => new { e.Id, e.Person!.LastName, e.Person.FirstName, e.Person.MiddleName, Group = e.Group!.Name })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.Id, r => $"{Fmt.FullName(r.LastName, r.FirstName, r.MiddleName)} — {r.Group}");
    }

    // «Поле: было → стало» для всех изменённых полей
    private static string? DescribeChanges(EntityEntry entry)
    {
        var parts = new List<string>();
        foreach (var prop in entry.Properties.Where(p => p.IsModified))
        {
            var name = prop.Metadata.Name;
            if (AuditSkip.Contains(name)) continue;
            if (Equals(prop.OriginalValue, prop.CurrentValue)) continue;
            var label = entry.Metadata.ClrType.GetProperty(name)?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? name;
            parts.Add($"{label}: {Show(prop.OriginalValue)} → {Show(prop.CurrentValue)}");
        }
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    private static string Show(object? value) => value switch
    {
        null => "—",
        Enum e => e.GetDisplayName(),
        DateOnly d => d.ToString("dd.MM.yyyy"),
        bool flag => flag ? "да" : "нет",
        string text when text.Length == 0 => "—",
        string text => text.Length > 80 ? text[..79] + "…" : text,
        _ => value.ToString() ?? "—"
    };
}
