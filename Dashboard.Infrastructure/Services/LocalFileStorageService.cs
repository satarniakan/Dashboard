using Dashboard.Domain.Interfaces;

namespace Dashboard.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _webRootPath;

    public LocalFileStorageService(string webRootPath)
    {
        _webRootPath = webRootPath;
    }

    public async Task<string> SaveFileAsync(Stream content, string fileName)
    {
        var uploadsFolder = Path.Combine(_webRootPath, "uploads", "products");
        Directory.CreateDirectory(uploadsFolder);

        var safeFileName = $"{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
        var fullPath = Path.Combine(uploadsFolder, safeFileName);

        await using var fileStream = new FileStream(fullPath, FileMode.Create);
        await content.CopyToAsync(fileStream);

        return $"/uploads/products/{safeFileName}";
    }
}