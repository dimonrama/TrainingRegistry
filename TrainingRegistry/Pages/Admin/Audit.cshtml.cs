using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TrainingRegistry.Pages.Admin;

public class AuditModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/History/Index");
}
