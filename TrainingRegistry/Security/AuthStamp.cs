using System.Security.Cryptography;
using System.Text;
using TrainingRegistry.Domain;

namespace TrainingRegistry.Security;

public static class AuthStamp
{
    // отпечаток учётки: если у пользователя изменились роль, права или пароль - он станет другим, и старая сессия перестанет работать
    public static string Compute(UserRole role, bool canEditOrganizations, bool canEditPeople, string passwordHash,
        string extraPermissions = "")
    {
        var raw = $"{role}|{canEditOrganizations}|{canEditPeople}|{passwordHash}|{extraPermissions}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..24];
    }

    // остальные права одной строкой, чтобы они тоже попадали в отпечаток
    public static string ExtraPermissions(bool enroll, bool viewDocuments, bool editDocuments, bool reports, bool history)
        => $"{(enroll ? 1 : 0)}{(viewDocuments ? 1 : 0)}{(editDocuments ? 1 : 0)}{(reports ? 1 : 0)}{(history ? 1 : 0)}";
}
