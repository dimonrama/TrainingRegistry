namespace TrainingRegistry.Domain;

public static class AuditNames
{
    // названия объектов для журнала по-русски
    private static readonly Dictionary<string, string> Types = new()
    {
        ["Organization"] = "Организация",
        ["Person"] = "Обучающийся",
        ["PersonDocument"] = "Документ обучающегося",
        ["GeneratedDocument"] = "Приказ / протокол / ведомость",
        ["Education"] = "Образование",
        ["TrainingProgram"] = "Программа обучения",
        ["TrainingGroup"] = "Группа обучения",
        ["Enrollment"] = "Запись об обучении",
        ["Contract"] = "Договор",
        ["AppUser"] = "Пользователь",
        ["TestData"] = "Тестовые данные",
        ["Report"] = "Отчёт"
    };

    public static string EntityType(string clrName) => Types.GetValueOrDefault(clrName, clrName);
}
