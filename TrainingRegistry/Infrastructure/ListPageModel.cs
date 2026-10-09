using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TrainingRegistry.Infrastructure;

public abstract class ListPageModel : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Sort { get; set; }
    [BindProperty(SupportsGet = true)] public bool Desc { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNo { get; set; } = 1;

    public virtual int PageSize => 20;
    // фильтры, которые нужно сохранять в ссылках сортировки и страниц
    protected virtual Dictionary<string, string> BaseRouteValues()
        => new() { ["Search"] = Search ?? "" };
    // в Excel идут те же фильтры, но без номера страницы
    public IDictionary<string, string> ExportLinkValues()
    {
        var values = BaseRouteValues();
        if (!string.IsNullOrEmpty(Sort))
        {
            values["Sort"] = Sort;
            values["Desc"] = Desc.ToString().ToLowerInvariant();
        }
        return values;
    }
    protected IActionResult XlsxFile(string fileName, string sheetName, IReadOnlyList<string> headers, IEnumerable<object?[]> rows)
        => File(XlsxWriter.Create(sheetName, headers, rows), XlsxWriter.ContentType,
            $"{fileName}_{Fmt.Today():yyyy-MM-dd}.xlsx");
    public IDictionary<string, string> SortLinkValues(string column)
    {
        var values = BaseRouteValues();
        values["Sort"] = column;
        values["Desc"] = (Sort == column && !Desc).ToString().ToLowerInvariant();
        return values;
    }
}