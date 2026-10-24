using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TrainingRegistry.Infrastructure;
using TrainingRegistry.Security;

namespace TrainingRegistry.Pages.People;

// массовая загрузка из Excel: файл -> предпросмотр -> подтверждение -> отчёт
[Authorize(Policy = Policies.CanEditPeople)]
[RequestFormLimits(ValueLengthLimit = 8_000_000)]
public class ImportModel : PageModel
{
    private const long MaxBytes = 5 * 1024 * 1024;

    private readonly ImportService _import;

    public ImportModel(ImportService import) => _import = import;

    [BindProperty] public IFormFile? Upload { get; set; }
    // файл между предпросмотром и подтверждением держим в самой странице
    [BindProperty] public string? FileData { get; set; }
    [BindProperty] public string? FileName { get; set; }
    [BindProperty] public bool IncludeDuplicates { get; set; }

    public ImportPreview? Preview { get; private set; }
    public ImportResult? Result { get; private set; }

    public void OnGet() { }

    public IActionResult OnGetTemplate()
        => File(ImportParser.BuildTemplate(), XlsxWriter.ContentType, "Шаблон_импорта_обучающихся.xlsx");

    public async Task<IActionResult> OnPostPreviewAsync()
    {
        if (Upload == null || Upload.Length == 0)
        {
            ModelState.AddModelError(nameof(Upload), "Выберите файл Excel (.xlsx)");
            return Page();
        }
        if (!Upload.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(Upload), "Нужен файл в формате .xlsx. Старые файлы .xls сохраните как «Книга Excel».");
            return Page();
        }
        if (Upload.Length > MaxBytes)
        {
            ModelState.AddModelError(nameof(Upload), "Файл больше 5 МБ");
            return Page();
        }

        using var ms = new MemoryStream();
        await Upload.CopyToAsync(ms);
        var bytes = ms.ToArray();

        Preview = await _import.BuildPreviewAsync(bytes);
        FileName = Path.GetFileName(Upload.FileName);
        if (Preview.StructureErrors.Count == 0) FileData = Convert.ToBase64String(bytes);
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmAsync()
    {
        if (string.IsNullOrEmpty(FileData))
        {
            ModelState.AddModelError(nameof(Upload), "Файл потерян, загрузите его заново");
            return Page();
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(FileData);
        }
        catch (FormatException)
        {
            ModelState.AddModelError(nameof(Upload), "Файл повреждён, загрузите его заново");
            return Page();
        }

        // перед записью проверка выполняется заново: за время просмотра в базе могли появиться записи
        Result = await _import.ImportAsync(bytes, IncludeDuplicates, FileName ?? "файл");
        return Page();
    }
}
