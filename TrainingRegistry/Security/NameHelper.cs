using TrainingRegistry.Domain;

namespace TrainingRegistry.Security;

public static class NameHelper
{
    // в журнал имя пишем коротко, например "Иванов И.И."
    public static string Short(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return fullName;
        var initials = string.Concat(parts.Skip(1).Select(p => char.ToUpper(p[0]) + "."));
        return initials.Length == 0 ? parts[0] : $"{parts[0]} {initials}";
    }

    public static string Display(string fullName, UserRole role)
        => $"{Short(fullName)} ({role.GetDisplayName().ToLowerInvariant()})";
}
