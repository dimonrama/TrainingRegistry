using System.Security.Cryptography;
using System.Text;
using TrainingRegistry.Domain;

namespace TrainingRegistry.Security;

public static class AuthStamp
{
    // отпечаток учётки: если у пользователя изменились роль, права или пароль - он станет другим, и старая сессия перестанет работать
    public static string Compute(UserRole role, bool canEditOrganizations, bool canEditPeople, string passwordHash)
    {
        var raw = $"{role}|{canEditOrganizations}|{canEditPeople}|{passwordHash}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..24];
    }
}
