using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;
using QuestPDF.Infrastructure;
using TrainingRegistry.Infrastructure;
using TrainingRegistry.Infrastructure.Documents;
using TrainingRegistry.Security;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

// подключаем базу и свои сервисы
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// пароли храним только в виде хэша
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
// при старте применяет миграции и создаёт admin и metodist, если пользователей ещё нет
builder.Services.AddHostedService<DbInitializer>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();

// лицензия community, для учебного проекта её хватает
QuestPDF.Settings.License = LicenseType.Community;
builder.Services.Configure<ContractSettings>(builder.Configuration.GetSection(ContractSettings.SectionName));
builder.Services.AddScoped<ContractService>();
builder.Services.AddScoped<TestDataService>();
builder.Services.AddScoped<PersonDuplicateService>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<GroupDocumentService>();
builder.Services.AddScoped<ReportService>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.Name = "TrainingRegistry.Auth";
        // на каждом запросе сверяем куку с базой, чтобы блокировка сработала сразу
        options.Events.OnValidatePrincipal = CookieEvents.ValidateAsync;
    });

// политики: только админ + отдельные права методиста (организации, обучающиеся, зачисление, документы, отчёты, история)
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.AdminOnly, p => p.RequireRole(Roles.Admin));

    options.AddPolicy(Policies.CanEditOrganizations, p => p.RequireAssertion(ctx =>
        ctx.User.IsInRole(Roles.Admin) ||
        ctx.User.HasClaim(AppClaims.Permission, AppClaims.PermOrganizations)));

    options.AddPolicy(Policies.CanEditPeople, p => p.RequireAssertion(ctx =>
        ctx.User.IsInRole(Roles.Admin) ||
        ctx.User.HasClaim(AppClaims.Permission, AppClaims.PermPeople)));

    // остальные права проверяем теми же методами, что и в разметке страниц
    options.AddPolicy(Policies.CanEnroll, p => p.RequireAssertion(ctx => ctx.User.CanEnroll()));
    options.AddPolicy(Policies.CanViewDocuments, p => p.RequireAssertion(ctx => ctx.User.CanViewDocuments()));
    options.AddPolicy(Policies.CanEditDocuments, p => p.RequireAssertion(ctx => ctx.User.CanEditDocuments()));
    options.AddPolicy(Policies.CanReports, p => p.RequireAssertion(ctx => ctx.User.CanReports()));
    options.AddPolicy(Policies.CanViewHistory, p => p.RequireAssertion(ctx => ctx.User.CanViewHistory()));
});

builder.Services.AddRazorPages(options =>
{
    // закрываем всё, открыта только страница входа
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
    options.Conventions.AllowAnonymousToPage("/Error");

    // администрирование только для роли Admin
    options.Conventions.AuthorizeFolder("/Admin", Policies.AdminOnly);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// считаем время формирования страницы (показываем внизу) и ставим защитные заголовки
app.Use(async (ctx, next) =>
{
    ctx.Items["RequestTimer"] = System.Diagnostics.Stopwatch.StartNew();
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "same-origin";
    await next();
});

app.UseStaticFiles();
app.UseRouting();
// порядок важен: сначала аутентификация, потом авторизация
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
