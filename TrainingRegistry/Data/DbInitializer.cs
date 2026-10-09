using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Domain;

namespace TrainingRegistry.Data;

public class DbInitializer : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<DbInitializer> _logger;
    public DbInitializer(IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<DbInitializer> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>();
        // если миграции есть - применяем, если нет - создаём таблицы сразу, чтобы проект хотя бы запускался
        if (db.Database.GetMigrations().Any())
        {
            await db.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            _logger.LogWarning("Миграции не найдены, схема создаётся через EnsureCreated. " +
                               "Выполните: dotnet ef migrations add Initial");
            await db.Database.EnsureCreatedAsync(cancellationToken);
        }
        // первый запуск: создаём двух пользователей
        if (!await db.AppUsers.AnyAsync(cancellationToken))
        {
            // пароли можно поменять в appsettings, иначе будут стандартные
            var adminPassword = _config["Seed:AdminPassword"] ?? "Admin123!";
            var methodistPassword = _config["Seed:MethodistPassword"] ?? "Metod123!";
            var admin = new AppUser
            {
                FullName = "Администратор Системы",
                Login = "admin",
                Role = UserRole.Admin,
                CanEditOrganizations = true,
                CanEditPeople = true
            };
            admin.PasswordHash = hasher.HashPassword(admin, adminPassword);
            var methodist = new AppUser
            {
                FullName = "Методистов Иван Петрович",
                Login = "metodist",
                Role = UserRole.Methodist,
                CanEditOrganizations = true,
                CanEditPeople = true
            };
            methodist.PasswordHash = hasher.HashPassword(methodist, methodistPassword);
            db.AppUsers.AddRange(admin, methodist);
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Созданы начальные пользователи: admin и metodist.");
        }
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}