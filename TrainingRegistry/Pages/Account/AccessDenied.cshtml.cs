using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TrainingRegistry.Pages.Account;

public class AccessDeniedModel : PageModel
{
    public void OnGet()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
    }
}
