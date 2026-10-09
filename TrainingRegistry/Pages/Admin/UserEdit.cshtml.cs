using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;

namespace TrainingRegistry.Pages.Admin;

public class UserEditModel : PageModel
{
    // логин: латиница, цифры и несколько символов, 3-50 знаков
    private static readonly Regex LoginRegex = new(@"^[A-Za-z0-9._-]{3,50}$", RegexOptions.Compiled);
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<AppUser> _hasher;
    public UserEditModel(AppDbContext db, IPasswordHasher<AppUser> hasher)
    {
        _db = db;
        _hasher = hasher;
    }
    public AppUser Item { get; set; } = new();
    public bool IsNew { get; set; }
    public bool IsSelf { get; set; }

    [BindProperty] public string? NewPassword { get; set; }
    [BindProperty] public string? ConfirmPassword { get; set; }
    private int? CurrentUserId
        => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public async Task<IActionResult> OnGetAsync(int? id)
    {
        IsNew = id == null;
        if (id == null)
        {
            Item = new AppUser { Role = UserRole.Methodist, CanEditOrganizations = true, CanEditPeople = true };
            return Page();
        }
        var user = await _db.AppUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();
        Item = user;
        IsSelf = user.Id == CurrentUserId;
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(int? id)
    {
        IsNew = id == null;
        AppUser entity;
        if (id == null)
        {
            entity = new AppUser();
        }
        else
        {
            var existing = await _db.AppUsers.FirstOrDefaultAsync(u => u.Id == id);
            if (existing == null) return NotFound();
            entity = existing;
        }
        Item = entity;
        IsSelf = !IsNew && entity.Id == CurrentUserId;
        var wasActiveAdmin = !IsNew && entity.Role == UserRole.Admin && !entity.IsBlocked;

        await TryUpdateModelAsync(entity, nameof(Item),
            x => x.FullName, x => x.Login, x => x.Role, x => x.IsBlocked,
            x => x.CanEditOrganizations, x => x.CanEditPeople);
        entity.FullName = (entity.FullName ?? "").Trim();
        entity.Login = (entity.Login ?? "").Trim();
        if (!string.IsNullOrEmpty(entity.Login))
        {
            if (!LoginRegex.IsMatch(entity.Login))
            {
                ModelState.AddModelError("Item.Login", "Логин: 3–50 символов, латинские буквы, цифры, точка, дефис и подчёркивание");
            }
            else
            {
                var lower = entity.Login.ToLower();
                var taken = await _db.AppUsers.AnyAsync(u => u.Login.ToLower() == lower && u.Id != entity.Id);
                if (taken) ModelState.AddModelError("Item.Login", "Пользователь с таким логином уже есть");
            }
        }
        var passwordGiven = !string.IsNullOrEmpty(NewPassword);
        if (IsNew && !passwordGiven)
            ModelState.AddModelError(nameof(NewPassword), "Задайте пароль");
        if (passwordGiven)
        {
            var passwordError = CheckPassword(NewPassword!);
            if (passwordError != null) ModelState.AddModelError(nameof(NewPassword), passwordError);
            else if (NewPassword != ConfirmPassword)
                ModelState.AddModelError(nameof(ConfirmPassword), "Пароли не совпадают");
        }
        // самого себя блокировать и понижать нельзя
        if (IsSelf)
        {
            if (entity.Role != UserRole.Admin)
                ModelState.AddModelError("Item.Role", "Нельзя снять с себя роль администратора");
            if (entity.IsBlocked)
                ModelState.AddModelError("Item.IsBlocked", "Нельзя заблокировать самого себя");
        }
        // в системе всегда должен остаться хотя бы один активный админ
        if (wasActiveAdmin && (entity.Role != UserRole.Admin || entity.IsBlocked))
        {
            var otherAdmin = await _db.AppUsers.AnyAsync(u => u.Id != entity.Id && u.Role == UserRole.Admin && !u.IsBlocked);
            if (!otherAdmin)
                ModelState.AddModelError(string.Empty, "Нельзя оставить систему без активного администратора");
        }
        if (!ModelState.IsValid) return Page();
        // пароль сохраняем только хэшем
        if (passwordGiven) entity.PasswordHash = _hasher.HashPassword(entity, NewPassword!);
        if (IsNew) _db.AppUsers.Add(entity);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("Item.Login", "Пользователь с таким логином уже есть");
            return Page();
        }
        TempData["Success"] = IsNew ? "Пользователь создан" : "Изменения сохранены";
        return RedirectToPage("./Users");
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();
        if (user.Id == CurrentUserId)
        {
            TempData["Error"] = "Нельзя удалить самого себя";
            return RedirectToPage(new { id });
        }
        if (user.Role == UserRole.Admin && !user.IsBlocked &&
            !await _db.AppUsers.AnyAsync(u => u.Id != id && u.Role == UserRole.Admin && !u.IsBlocked))
        {
            TempData["Error"] = "Нельзя удалить последнего активного администратора";
            return RedirectToPage(new { id });
        }
        _db.AppUsers.Remove(user);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Пользователь {user.Login} удалён";
        return RedirectToPage("./Users");
    }

    private static string? CheckPassword(string password)
    {
        if (password.Length < 8) return "Пароль должен быть не короче 8 символов";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit)) return "В пароле нужны буквы и цифры";
        return null;
    }
}