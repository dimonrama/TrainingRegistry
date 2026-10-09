namespace TrainingRegistry.Domain;

public static class AuditNames
{
    // названия объектов для журнала по-русски
    private static readonly Dictionary<string, string> Types = new()
    {
        ["Organization"] = "Организация",
        ["Person"] = "Сотрудник",
        ["Education"] = "Образование",
        ["TrainingProgram"] = "Программа обучения",
        ["TrainingGroup"] = "Группа обучения",
        ["Enrollment"] = "Запись об обучении",
        ["Contract"] = "Договор",
        ["AppUser"] = "Пользователь",
        ["TestData"] = "Тестовые данные"
    };

    public static string EntityType(string clrName) => Types.GetValueOrDefault(clrName, clrName);
}
