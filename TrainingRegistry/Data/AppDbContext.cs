using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Domain;
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
            e.HasOne(x => x.Organization).WithMany(o => o.Employees)
                .HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Education>(e =>
        {
            e.Property(x => x.Level).HasConversion<string>().HasMaxLength(30);
            e.HasOne(x => x.Person).WithMany(p => p.Educations)
                .HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<TrainingProgram>(e =>
        {
            e.ToTable("Programs");
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
            e.HasOne(x => x.Program).WithMany(p => p.Groups)
                .HasForeignKey(x => x.ProgramId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Enrollment>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Person).WithMany(p => p.Enrollments)
                .HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Group).WithMany(g => g.Enrollments)
                .HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
            // один сотрудник - одна запись в группе, но только среди неудалённых, иначе после удаления нельзя зачислить заново
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
        });
    }
    // все сохранения проходят тут: ставим кто и когда изменил, удаление делаем логическим и пишем журнал
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var who = _currentUser.DisplayName;
        var journal = new List<(AuditableEntity Entity, string Type, string Action)>();
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
                    journal.Add((entity, type, "Создание"));
                    break;
                case EntityState.Modified:
                    entry.Property(e => e.CreatedAt).IsModified = false;
                    entry.Property(e => e.CreatedBy).IsModified = false;
                    entity.UpdatedAt = now;
                    entity.UpdatedBy = who;
                    var becameDeleted = entity.IsDeleted && !entry.Property(e => e.IsDeleted).OriginalValue;
                    journal.Add((entity, type, becameDeleted ? "Удаление" : "Изменение"));
                    break;
                // физически ничего не удаляем, только помечаем
                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entity.IsDeleted = true;
                    entity.UpdatedAt = now;
                    entity.UpdatedBy = who;
                    journal.Add((entity, type, "Удаление"));
                    break;
            }
        }
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        // id у новых записей появляется только после сохранения, поэтому журнал пишем вторым шагом
        if (journal.Count > 0 && !SuppressAudit)
        {
            foreach (var (entity, type, action) in journal)
            {
                AuditLogs.Add(new AuditLog
                {
                    At = now,
                    UserName = who,
                    Action = action,
                    EntityType = type,
                    EntityId = entity.Id
                });
            }
            await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        return result;
    }
}