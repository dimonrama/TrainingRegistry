using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;
using TrainingRegistry.Infrastructure;
using TrainingRegistry.Security;

namespace TrainingRegistry.Pages.Admin;

public class ToolsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly TestDataService _data;

    public ToolsModel(AppDbContext db, TestDataService data)
    {
        _db = db;
        _data = data;
    }

    public int LoadPeople { get; private set; }
    public int PeopleTotal { get; private set; }
    public int EnrollmentsTotal { get; private set; }
    public bool HasData { get; private set; }

    public async Task OnGetAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        LoadPeople = await _data.CountLoadPeopleAsync();
        PeopleTotal = await _db.People.CountAsync();
        EnrollmentsTotal = await _db.Enrollments.CountAsync();
        HasData = await _db.Organizations.AnyAsync() || await _db.Programs.AnyAsync();
    }

    public async Task<IActionResult> OnPostDemoAsync()
    {
        var done = await _data.SeedDemoAsync();
        TempData[done ? "Success" : "Error"] = done
            ? "Демо-данные добавлены: организации, сотрудники, программы, группы и записи об обучении."
            : "Демо-данные не добавлены: в системе уже есть организации или программы.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostLoadAsync(int count)
    {
        count = Math.Clamp(count, 100, 20000);
        var sw = Stopwatch.StartNew();

        // на время загрузки отключаем журнал, потом запишем одну строку
        _db.SuppressAudit = true;
        await _data.AddLoadAsync(count);
        _db.SuppressAudit = false;
        await WriteJournalAsync($"Загрузка тестовых данных ({count} сотрудников)");

        TempData["Success"] = $"Добавлено {count} тестовых сотрудников с записями об обучении за {sw.Elapsed.TotalSeconds:F1} с.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveAsync()
    {
        var sw = Stopwatch.StartNew();
        var removed = await _data.RemoveLoadAsync();
        await WriteJournalAsync($"Удаление тестовых данных ({removed} сотрудников)");
        TempData["Success"] = $"Тестовые данные удалены ({removed} сотрудников) за {sw.Elapsed.TotalSeconds:F1} с.";
        return RedirectToPage();
    }

    private async Task WriteJournalAsync(string action)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            At = DateTime.UtcNow,
            UserName = User.FindFirst(AppClaims.DisplayName)?.Value ?? "Система",
            Action = action,
            EntityType = "TestData",
            EntityId = 0
        });
        await _db.SaveChangesAsync();
    }
}
