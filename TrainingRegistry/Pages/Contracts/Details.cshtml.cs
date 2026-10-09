using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainingRegistry.Data;
using TrainingRegistry.Domain;
using TrainingRegistry.Infrastructure;
using TrainingRegistry.Infrastructure.Documents;

namespace TrainingRegistry.Pages.Contracts;

public class DetailsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ContractService _contracts;

    public DetailsModel(AppDbContext db, ContractService contracts)
    {
        _db = db;
        _contracts = contracts;
    }

    public Contract Item { get; private set; } = null!;
    public string GroupName { get; private set; } = "";
    public string ProgramName { get; private set; } = "";
    public string CustomerName { get; private set; } = "";
    public int? CustomerOrganizationId => Item.CustomerOrganizationId;
    public List<StudentInfo> Students { get; private set; } = new();

    public bool OutOfDate => Students.Count != Item.StudentsCount;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        return await LoadAsync(id) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnPostRegenerateAsync(int id)
    {
        var error = await _contracts.RegenerateAsync(id);
        if (error != null) TempData["Error"] = error;
        else TempData["Success"] = "Документы обновлены";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var contract = await _db.Contracts.FirstOrDefaultAsync(c => c.Id == id);
        if (contract == null) return NotFound();

        var number = contract.Number;
        _db.Contracts.Remove(contract);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Договор {number} удалён";
        return RedirectToPage("./Index");
    }

    private async Task<bool> LoadAsync(int id)
    {
        var contract = await _db.Contracts.AsNoTracking()
            .Include(c => c.Group).ThenInclude(g => g!.Program)
            .Include(c => c.CustomerOrganization)
            .Include(c => c.CustomerPerson)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (contract == null) return false;

        Item = contract;
        GroupName = contract.Group!.Name;
        ProgramName = contract.Group.Program!.Name;
        CustomerName = contract.CustomerOrganization?.FullName ?? contract.CustomerPerson?.FullName ?? "—";
        Students = await _contracts.LoadStudentsAsync(contract.GroupId, contract.CustomerOrganizationId, contract.CustomerPersonId);
        return true;
    }
}
