using System.Security.Claims;

namespace TrainingRegistry.Security;

public interface ICurrentUser
{
    string DisplayName { get; }
}

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    // берём имя из куки, а вне запроса (например при старте) будет "Система"
    public string DisplayName
    {
        get
        {
            var user = _accessor.HttpContext?.User;
            return user?.FindFirstValue(AppClaims.DisplayName) ?? "Система";
        }
    }
}
