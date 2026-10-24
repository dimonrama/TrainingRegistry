namespace TrainingRegistry.Domain;

public class AuditLog
{
    public long Id { get; set; }
    public DateTime At { get; set; }
    public string UserName { get; set; } = "";

    public string Action { get; set; } = "";

    public string EntityType { get; set; } = "";
    public int EntityId { get; set; }

    // название объекта на момент действия, например ФИО обучающегося
    public string? EntityTitle { get; set; }

    // что именно изменилось (старое → новое)
    public string? Details { get; set; }
}
