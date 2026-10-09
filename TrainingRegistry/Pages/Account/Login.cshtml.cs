using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;
using TrainingRegistry.Security;

namespace TrainingRegistry.Pages.Account;

public class LoginModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<AppUser> _hasher;

    public LoginModel(AppDbContext db, IPasswordHasher<AppUser> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    [BindProperty] public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Введите логин")]
        [Display(Name = "Логин")]
        public string Login { get; set; } = "";

        [Required(ErrorMessage = "Введите пароль")]
        [DataType(DataType.Password)]
        [Display(Name = "Пароль")]
        public string Password { get; set; } = "";
    }

    public IActionResult OnGet(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return LocalRedirect("/");
        ReturnUrl = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        if (!ModelState.IsValid) return Page();

        var login = Input.Login.Trim().ToLower();
        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.Login.ToLower() == login);

        // на неверный логин и неверный пароль одно и то же сообщение
        if (user == null ||
            _hasher.VerifyHashedPassword(user, user.PasswordHash, Input.Password) == PasswordVerificationResult.Failed)
        {
            await WriteJournalAsync(Input.Login.Trim(), user?.Id ?? 0, "Неудачный вход");
            ModelState.AddModelError(string.Empty, "Неверный логин или пароль");
            return Page();
        }

        if (user.IsBlocked)
        {
            await WriteJournalAsync(user.Login, user.Id, "Вход заблокирован");
            ModelState.AddModelError(string.Empty, "Учётная запись заблокирована. Обратитесь к администратору");
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Login),
            new(ClaimTypes.Role, user.Role == UserRole.Admin ? Roles.Admin : Roles.Methodist),
            new(AppClaims.DisplayName, NameHelper.Display(user.FullName, user.Role)),
            new(AppClaims.Stamp, AuthStamp.Compute(user.Role, user.CanEditOrganizations, user.CanEditPeople, user.PasswordHash))
        };
        // права методиста кладём в claims, потом по ним проверяем доступ
        if (user.CanEditOrganizations) claims.Add(new Claim(AppClaims.Permission, AppClaims.PermOrganizations));
        if (user.CanEditPeople) claims.Add(new Claim(AppClaims.Permission, AppClaims.PermPeople));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false });

        var now = DateTime.UtcNow;
        await _db.AppUsers.Where(u => u.Id == user.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastLoginAt, now));

        _db.AuditLogs.Add(new AuditLog
        {
            At = now,
            UserName = NameHelper.Display(user.FullName, user.Role),
            Action = "Вход",
            EntityType = nameof(AppUser),
            EntityId = user.Id
        });
        await _db.SaveChangesAsync();

        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }

    private async Task WriteJournalAsync(string who, int userId, string action)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            At = DateTime.UtcNow,
            UserName = who.Length > 100 ? who[..100] : who,
            Action = action,
            EntityType = nameof(AppUser),
            EntityId = userId
        });
        await _db.SaveChangesAsync();
    }
}
