using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;
using TrainingRegistry.Security;

namespace TrainingRegistry.Pages.Account;

public class LogoutModel : PageModel
{
    private readonly AppDbContext _db;

    public LogoutModel(AppDbContext db) => _db = db;

    public IActionResult OnGet() => RedirectToPage("/Index");

    public async Task<IActionResult> OnPostAsync()
    {
        if (int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId))
        {
            _db.AuditLogs.Add(new AuditLog
            {
                At = DateTime.UtcNow,
                UserName = User.FindFirst(AppClaims.DisplayName)?.Value ?? "",
                Action = "Выход",
                EntityType = nameof(AppUser),
                EntityId = userId
            });
            await _db.SaveChangesAsync();
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Account/Login");
    }
}
