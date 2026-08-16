namespace yurt_lojman_yonetim_sistemi.Services;

public interface IFileStorageService
{
    Task<string?> SaveAsync(IFormFile? file, string folder, CancellationToken cancellationToken);
}

public class FileStorageService(IWebHostEnvironment environment) : IFileStorageService
{
    public async Task<string?> SaveAsync(IFormFile? file, string folder, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return null;
        }

        var safeFolder = string.Join(string.Empty, folder.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'));
        var uploadsRoot = Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "uploads", safeFolder);
        Directory.CreateDirectory(uploadsRoot);

        var extension = Path.GetExtension(file.FileName);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(uploadsRoot, fileName);

        await using var stream = File.Create(fullPath);
        await file.CopyToAsync(stream, cancellationToken);

        return $"/uploads/{safeFolder}/{fileName}";
    }
}
