namespace TrainingRegistry.Domain;

public abstract class AuditableEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    // имя сохраняем текстом, как оно было в момент действия
    public string CreatedBy { get; set; } = "";
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    // логическое удаление: запись остаётся в базе, просто не показывается
    public bool IsDeleted { get; set; }

    // как назвать объект в журнале; по умолчанию без названия
    public virtual string? AuditTitle => null;
}
