namespace TrainingRegistry.Infrastructure;

public record StoredFile(string RelativePath, string OriginalName);

public interface IFileStorage
{
    string? Validate(IFormFile file);
    Task<StoredFile> SaveAsync(IFormFile file, string folder, CancellationToken ct = default);
    Task<string> SaveBytesAsync(byte[] data, string folder, string extension, CancellationToken ct = default);

    (Stream Stream, string ContentType)? Open(string relativePath);

    void Delete(string? relativePath);
}

public class LocalFileStorage : IFileStorage
{
    public const long MaxBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> UploadExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png"
    };
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    };
    private readonly string _root;
    public LocalFileStorage(IConfiguration config, IWebHostEnvironment env)
    {
        var configured = config["Storage:Root"];
        var root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(env.ContentRootPath, "App_Data", "uploads")
            : configured;
        _root = Path.GetFullPath(root);
        Directory.CreateDirectory(_root);
    }
    // проверяем размер и расширение до сохранения
    public string? Validate(IFormFile file)
    {
        if (file.Length == 0) return "Файл пустой";
        if (file.Length > MaxBytes) return "Размер файла не должен превышать 10 МБ";
        var ext = Path.GetExtension(file.FileName);
        if (!UploadExtensions.Contains(ext)) return "Допустимые форматы: PDF, JPG, PNG";
        return null;
    }
    public async Task<StoredFile> SaveAsync(IFormFile file, string folder, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        // имя файла генерируем сами, чтобы не было совпадений и подмены пути
        var relative = $"{folder}/{Guid.NewGuid():N}{ext}";
        var full = Resolve(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var stream = File.Create(full);
        await file.CopyToAsync(stream, ct);

        return new StoredFile(relative, Path.GetFileName(file.FileName));
    }

    public async Task<string> SaveBytesAsync(byte[] data, string folder, string extension, CancellationToken ct = default)
    {
        var relative = $"{folder}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var full = Resolve(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, data, ct);
        return relative;
    }
    public (Stream Stream, string ContentType)? Open(string relativePath)
    {
        var full = Resolve(relativePath);
        if (!File.Exists(full)) return null;
        var type = ContentTypes.GetValueOrDefault(Path.GetExtension(full), "application/octet-stream");
        return (File.OpenRead(full), type);
    }
    public void Delete(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;
        var full = Resolve(relativePath);
        if (File.Exists(full)) File.Delete(full);
    }
    private string Resolve(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relativePath));
        // не даём выйти за пределы папки хранилища (../)
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Недопустимый путь к файлу");
        return full;
    }
}