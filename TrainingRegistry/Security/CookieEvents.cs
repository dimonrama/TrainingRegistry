using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;

namespace TrainingRegistry.Security;

public static class CookieEvents
{
    // вызывается на каждом запросе: если пользователя заблокировали, удалили или поменяли права - выкидываем из системы
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var valid = false;
        if (int.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
        {
            var stamp = principal!.FindFirstValue(AppClaims.Stamp);
            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var user = await db.AppUsers.AsNoTracking()
                .Where(u => u.Id == id)
                .Select(u => new { u.Role, u.IsBlocked, u.CanEditOrganizations, u.CanEditPeople, u.PasswordHash })
                .FirstOrDefaultAsync();
            valid = user != null
                    && !user.IsBlocked
                    && stamp == AuthStamp.Compute(user.Role, user.CanEditOrganizations, user.CanEditPeople, user.PasswordHash);
        }
        if (!valid)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}