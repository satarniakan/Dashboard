using Dashboard.Domain.Exceptions;
using Dashboard.Domain.Interfaces;

namespace Dashboard.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _webRootPath;

    // فایل‌ها زیر wwwroot به‌صورت static سرو می‌شوند — پسوند خطرناک (.html/.svg/.js و…)
    // بردار Stored XSS است، بنابراین فقط پسوندهای تصویری مجازند
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".avif"
    };

    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB

    public LocalFileStorageService(string webRootPath)
    {
        _webRootPath = webRootPath;
    }

    public async Task<string> SaveFileAsync(Stream content, string fileName)
    {
        var extension = Path.GetExtension(fileName);
        if (!AllowedExtensions.Contains(extension))
            throw new BusinessRuleException(
                $"فرمت فایل «{extension}» مجاز نیست؛ فقط تصویر (jpg, png, webp, gif, avif) بارگذاری کنید.");

        var uploadsFolder = Path.Combine(_webRootPath, "uploads", "products");
        Directory.CreateDirectory(uploadsFolder);

        var safeFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var fullPath = Path.Combine(uploadsFolder, safeFileName);

        await using (var fileStream = new FileStream(fullPath, FileMode.Create))
        {
            await CopyWithSizeLimitAsync(content, fileStream);
        }

        return $"/uploads/products/{safeFileName}";
    }

    private static async Task CopyWithSizeLimitAsync(Stream source, Stream destination)
    {
        if (source.CanSeek && source.Length > MaxFileSizeBytes)
            throw new BusinessRuleException($"حجم فایل بیش از حد مجاز ({MaxFileSizeBytes / (1024 * 1024)} مگابایت) است.");

        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer)) > 0)
        {
            total += read;
            if (total > MaxFileSizeBytes)
                throw new BusinessRuleException($"حجم فایل بیش از حد مجاز ({MaxFileSizeBytes / (1024 * 1024)} مگابایت) است.");
            await destination.WriteAsync(buffer.AsMemory(0, read));
        }
    }
}
